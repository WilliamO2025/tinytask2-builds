using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TinyTask;

// Explicit, one-action diagnostic. Never runs a macro or selects a game/place.
internal static class RobloxTest
{
    [StructLayout(LayoutKind.Sequential)] private struct Input {public uint Type;public InputUnion Data;}
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion {[FieldOffset(0)]public Mouse Mouse;[FieldOffset(0)]public Keyboard Keyboard;}
    [StructLayout(LayoutKind.Sequential)] private struct Mouse {public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard {public ushort Key,Scan;public uint Flags,Time;public nuint Extra;}
    [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint count,Input[] input,int size);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint first,uint second,bool attach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    private static Input Key(ushort key,bool up,bool scan)=>new(){Type=1,Data=new(){Keyboard=new(){Key=scan?(ushort)0:key,Scan=scan?(ushort)Native.MapVirtualKey(key,0):(ushort)0,Flags=(up?2u:0u)|(scan?8u:0u)}}};
    private static void Inject(params Input[] inputs)
    {
        uint sent=SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<Input>());
        if(sent!=inputs.Length)throw new Win32Exception(Marshal.GetLastWin32Error(),$"SendInput inserted {sent}/{inputs.Length} events.");
    }
    private static async Task InjectClick(Native.Point screen)
    {
        int left=Native.GetSystemMetrics(76),top=Native.GetSystemMetrics(77),width=Native.GetSystemMetrics(78),height=Native.GetSystemMetrics(79);
        int px=(int)((long)(screen.X-left)*65535/Math.Max(1,width-1)),py=(int)((long)(screen.Y-top)*65535/Math.Max(1,height-1));
        Inject(new Input{Data=new(){Mouse=new(){X=px,Y=py,Flags=0xC001}}});
        try {Inject(new Input{Data=new(){Mouse=new(){Flags=2}}});await Task.Delay(50);}
        finally {Inject(new Input{Data=new(){Mouse=new(){Flags=4}}});}
    }
    internal static async Task<int> Run(string[] args)
    {
        // --roblox-test METHOD foreground|background REPORT [X Y [VK]]
        string method=args[1],state=args[2],report=Path.GetFullPath(args[3]);
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        int x=args.Length>4?int.Parse(args[4]):35,y=args.Length>5?int.Parse(args[5]):35;ushort key=args.Length>6?ushort.Parse(args[6]):(ushort)27;
        var overlay=new CursorWindow();Process? workProcess=null;Target? work=null;var timeline=new List<object>();
        Target? detected=null; bool inputAttempted=false; nint foregroundBefore=0;
        try
        {
            if(state!="background" && state!="foreground")throw new ArgumentException("Unknown focus state.");
            if(!new[]{"inspect","post-click","post-key","isolated-click","isolated-key","sync-click","sync-key","send-click","send-key","scan-key","uia"}.Contains(method))throw new ArgumentException("Unsupported Roblox probe.");
            if(key!=27)throw new ArgumentException("Roblox diagnostics permit only Escape.");
            if(x!=35 || y!=35)throw new ArgumentException("This diagnostic is restricted to the visually verified Roblox menu at client (35,35). Reverify layout before changing the probe.");
            var found=Native.Windows().Where(t=>t.Process.Equals("RobloxPlayerBeta",StringComparison.OrdinalIgnoreCase)).ToList();
            if(found.Count!=1)throw new InvalidOperationException($"Expected one visible RobloxPlayerBeta window; found {found.Count}. Running processes: {Process.GetProcessesByName("RobloxPlayerBeta").Length}.");
            Target target=found[0];detected=target;target.Validate();Native.GetWindowRect(target.Handle,out var rect);
            if(state=="background")
            {
                workProcess=Process.Start(new ProcessStartInfo(AppIdentity.ExecutablePath){UseShellExecute=false,CreateNoWindow=true,ArgumentList={"--work-fixture"}})!;
                for(int i=0;i<100;i++){work=Native.Windows().FirstOrDefault(t=>t.Pid==workProcess.Id);if(work!=null)break;await Task.Delay(50);}
                if(work==null)throw new Exception("Work fixture not available.");
                // Keep the fixture away from the observed main-menu tab/search controls.
                await Task.Delay(500); // Let WinForms finish its initial layout before positioning.
                var area=System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
                Native.SetWindowPos(work.Handle,(nint)(-1),area.Right-470,area.Bottom-300,450,280,0x50);
            }
            nint expected=state=="background"?work!.Handle:target.Handle;
            // Both windows are already visible. Do not use ShowWindow here: its first
            // call may honor the runner's SW_HIDE STARTUPINFO instead of this argument.
            bool foregroundRequest=Native.SetForegroundWindow(expected);
            string setupActivation="SetForegroundWindow";
            await Task.Delay(300);
            if(Native.GetForegroundWindow()!=expected)
            {
                uint current=GetCurrentThreadId(),foregroundThread=Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out _);
                bool attached=foregroundThread!=current && AttachThreadInput(current,foregroundThread,true);
                try {Native.SetWindowPos(expected,state=="background"?(nint)(-1):0,0,0,0,0,0x43);Native.SetForegroundWindow(expected);}
                finally {if(attached)AttachThreadInput(current,foregroundThread,false);}
                setupActivation="Temporary AttachThreadInput during setup only; detached before probe";
                await Task.Delay(300);
            }
            if(state=="background" && Native.GetForegroundWindow()!=expected)
            {
                Native.GetWindowRect(expected,out var workRect);
                var titlePoint=new Native.Point(workRect.Left+100,workRect.Top+12);
                if(Native.GetAncestor(Native.WindowFromPoint(titlePoint),2)!=expected){Capture(target,Path.ChangeExtension(report,"setup.png"));File.WriteAllText(Path.ChangeExtension(report,"windows.json"),JsonSerializer.Serialize(Native.Windows().Select(t=>new{t.Pid,hwnd=t.Handle.ToString(),t.Process,t.Title})));throw new Exception($"Work title bar is occluded; refusing global setup click. Point={titlePoint.X},{titlePoint.Y};actual={Native.WindowFromPoint(titlePoint)};expected={expected};workTitle={Native.Title(expected)};workVisible={Native.IsWindowVisible(expected)}.");}
                // Establish focus through an ordinary click on our own fixture BEFORE measurement.
                await InjectClick(titlePoint);setupActivation="SendInput click on verified work-fixture title bar before measurement";
            }
            await Task.Delay(700);
            if(Native.GetForegroundWindow()!=expected)throw new Exception($"Foreground could not be established; no input sent. Requested={expected};actual={Native.GetForegroundWindow()};SetForegroundWindow={foregroundRequest}.");
            string beforeFile=Path.ChangeExtension(report,"before.png"),afterFile=Path.ChangeExtension(report,"after.png");
            Capture(target,beforeFile);Native.GetCursorPos(out var before);
            var screen=new Native.Point(x,y);Native.ClientToScreen(target.Handle,ref screen);
            if(method!="inspect")Routing.Receiver(target,x,y);
            if(method.StartsWith("isolated-"))overlay.Place(screen.X,screen.Y);
            foregroundBefore=Native.GetForegroundWindow();
            bool stable=true;string? error=null;var watch=Stopwatch.StartNew();
            using var cancel=new CancellationTokenSource();
            var sampler=Task.Run(async()=>{nint previous=expected;while(!cancel.IsCancellationRequested){nint f=Native.GetForegroundWindow();if(f!=expected)stable=false;if(f!=previous){timeline.Add(new {ms=watch.ElapsedMilliseconds,foreground=f.ToString()});previous=f;}try{await Task.Delay(5,cancel.Token);}catch(OperationCanceledException){break;}}});
            try
            {
                if(new[]{0x10,0x11,0x12,0x5B,0x5C}.Any(k=>(Native.GetAsyncKeyState(k)&0x8000)!=0))throw new Exception("Release modifier keys before testing.");
                target.Validate();
                if(!Native.IsWindowVisible(target.Handle))throw new Exception("Roblox became hidden; no probe sent.");
                if(Native.GetForegroundWindow()!=expected)throw new Exception("Focus changed before input; no probe sent.");
                if(method=="send-click" && Native.GetAncestor(Native.WindowFromPoint(screen),2)!=target.Handle)throw new Exception("Roblox click location is occluded; no system click sent.");
                inputAttempted=method!="inspect";
                if(method=="post-click" || method=="isolated-click")await Routing.Click(target,x,y,CancellationToken.None);
                else if(method=="post-key" || method=="isolated-key")Routing.Key(target,x,y,key);
                else if(method=="uia")await Routing.Invoke(target,x,y,CancellationToken.None);
                else if(method=="sync-click")
                {
                    var (receiver,p)=Routing.Receiver(target,x,y);var xy=Native.Coordinates(p.X,p.Y);
                    try {foreach(var item in new[]{(0x200u,(nint)0),(0x201u,(nint)1)})
                    {if(Native.SendMessageTimeout(receiver,item.Item1,item.Item2,xy,0x22,500,out _)==0)throw new Exception("Synchronous window message failed/timed out.");}await Task.Delay(50);}
                    finally {Native.Post(receiver,0x202,0,xy);}
                }
                else if(method=="sync-key")
                {
                    nint down=(nint)(1u | Native.MapVirtualKey(key,0)<<16);
                    try {if(Native.SendMessageTimeout(target.Handle,0x100,key,down,0x22,500,out _)==0)throw new Exception("Synchronous key message failed/timed out.");}
                    finally {Native.Post(target.Handle,0x101,key,unchecked((nint)((long)down|0xC0000000L)));}
                }
                else if(method=="send-key" || method=="scan-key")
                {try {Inject(Key(key,false,method=="scan-key"));await Task.Delay(50);}finally {Inject(Key(key,true,method=="scan-key"));}}
                else if(method=="send-click")
                {
                    await InjectClick(screen);
                }
                else if(method!="inspect")throw new ArgumentException("Unknown probe method.");
                await Task.Delay(1200);
            }
            catch(Exception e){error=e.Message;}
            finally{cancel.Cancel();await sampler;Routing.ReleaseAll();overlay.Hide();}
            Native.GetCursorPos(out var after);
            string? captureError=null,versionError=null,version=null;
            try {Capture(target,afterFile);}catch(Exception e){captureError=e.Message;}
            try {using var process=Process.GetProcessById((int)target.Pid);version=process.MainModule?.FileVersionInfo.FileVersion;}catch(Exception e){versionError=e.Message;}
            File.WriteAllText(report,JsonSerializer.Serialize(new {time=DateTimeOffset.UtcNow,method,state,setupActivation,target=new{pid=target.Pid,hwnd=target.Handle.ToString(),visible=Native.IsWindowVisible(target.Handle),title=target.Title,clientSize=ClientSize(target),version},x,y,key,valid=captureError==null && ((method=="inspect" && error==null) || inputAttempted),inputAttempted,apiAccepted=inputAttempted && error==null,foregroundBefore=foregroundBefore.ToString(),robloxWasForeground=foregroundBefore==target.Handle,expectedForeground=expected.ToString(),foregroundAfter=Native.GetForegroundWindow().ToString(),focusStable=stable&&Native.GetForegroundWindow()==expected,cursorBefore=new[]{before.X,before.Y},cursorAfter=new[]{after.X,after.Y},cursorStable=before.X==after.X&&before.Y==after.Y,timeline,error,captureError,versionError,beforeFile,afterFile,reaction="Requires image inspection; API return is not proof of effect",deliveryToRoblox="Not instrumented inside Roblox; use the observed reaction, not API acceptance",isolation=method.StartsWith("send-") || method=="scan-key" ? "Global SendInput; no isolation" : method.StartsWith("isolated-") ? "Visual second cursor plus window messages; no physical-device isolation" : "Targeted window API; no physical-device isolation"},new JsonSerializerOptions{WriteIndented=true}));
            return error==null && captureError==null?0:1;
        }
        catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{time=DateTimeOffset.UtcNow,method,state,valid=false,inputAttempted,apiAccepted=false,target=detected==null?null:new{pid=detected.Pid,hwnd=detected.Handle.ToString(),visible=Native.IsWindowVisible(detected.Handle)},foregroundBefore=foregroundBefore.ToString(),foregroundAfter=Native.GetForegroundWindow().ToString(),reaction="Not tested: invalid setup",error=e.ToString()},new JsonSerializerOptions{WriteIndented=true}));return 1;}
        finally
        {
            overlay.Close();Routing.ReleaseAll();
            if(workProcess!=null){if(!workProcess.HasExited){workProcess.CloseMainWindow();using var timeout=new CancellationTokenSource(1500);try{await workProcess.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){workProcess.Kill();await workProcess.WaitForExitAsync();}}workProcess.Dispose();}
        }
    }
    private static int[] ClientSize(Target target){Native.GetClientRect(target.Handle,out var r);return new[]{r.Right,r.Bottom};}
    private static void Capture(Target target,string path)
    {
        Native.GetWindowRect(target.Handle,out var r);int width=r.Right-r.Left,height=r.Bottom-r.Top;
        if(width<=0||height<=0||width>10000||height>10000)throw new Exception("Invalid capture bounds.");
        using var bitmap=new Bitmap(width,height);using var g=Graphics.FromImage(bitmap);
        // Desktop capture intentionally includes occlusion; no claim to capture hidden game content.
        g.CopyFromScreen(r.Left,r.Top,0,0,new Size(width,height));bitmap.Save(path,ImageFormat.Png);
    }
}
