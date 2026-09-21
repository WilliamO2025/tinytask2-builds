using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Forms=System.Windows.Forms;
using Point=System.Drawing.Point;
using Size=System.Drawing.Size;

namespace TinyTask;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        AppIdentity.Register();
        if(args.FirstOrDefault()=="--restore-roblox")
        {
            var targets=Native.Windows(false).Where(t=>t.Process.Equals("RobloxPlayerBeta",StringComparison.OrdinalIgnoreCase)).ToList();
            if(targets.Count!=1)return 1;
            targets[0].Validate();
            return Native.SetWindowPos(targets[0].Handle,0,0,0,0,0,0x57)?0:1;
        }
        if(args.FirstOrDefault()=="--install")return Setup.Install(args.Contains("--quiet"));
        if(args.FirstOrDefault()=="--finish-uninstall" && args.Length==2)return Setup.FinishUninstall(args[1]);
        if(args.FirstOrDefault()=="--uninstall")return Setup.Uninstall();
        if(Path.GetFileNameWithoutExtension(AppIdentity.ExecutablePath).EndsWith(".Setup",StringComparison.OrdinalIgnoreCase))return Setup.Install();
        if(args.FirstOrDefault()=="--uia") return Routing.UiaWorker(args);
        if(args.FirstOrDefault()=="--browser-test")return BrowserTest.Run(args).GetAwaiter().GetResult();
        if(args.FirstOrDefault()=="--fixture") {Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);Forms.Application.Run(new Fixture(args.Length>1?args[1]:"manual"));return 0;}
        if(args.FirstOrDefault()=="--work-fixture")
        {
            Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
            using var form=new Forms.Form{Text="TinyTask work area",TopMost=true,StartPosition=Forms.FormStartPosition.Manual,Location=new Point(1250,650),Size=new Size(450,280)};
            form.Controls.Add(new Forms.TextBox{Multiline=true,Dock=Forms.DockStyle.Fill,Text="Work window: this must keep focus during background probes."});
            form.Shown+=(_,_)=>{form.WindowState=Forms.FormWindowState.Normal;form.BringToFront();form.Activate();};
            Forms.Application.Run(form);return 0;
        }
        if(args.FirstOrDefault()=="--self-test")
        {
            try {return SelfTest.Run(args.Length>1?args[1]:Path.Combine(AppContext.BaseDirectory,"self-test.json")).GetAwaiter().GetResult();}
            catch(Exception e) {File.WriteAllText(args.Length>1?args[1]:"self-test.json",JsonSerializer.Serialize(new {failure=e.ToString()}));return 1;}
        }
        var app=new Application {ShutdownMode=ShutdownMode.OnMainWindowClose};
        if(args.FirstOrDefault()=="--classic-test")
        {
            app.ShutdownMode=ShutdownMode.OnExplicitShutdown;app.Startup+=async(_,_)=>{try{app.Shutdown(await ClassicTests.Run(args[1]));}catch(Exception e){try{File.WriteAllText(args[1],JsonSerializer.Serialize(new{passed=false,error=e.ToString()}));}finally{app.Shutdown(1);}}};return app.Run();
        }
        if(args.FirstOrDefault()=="--ui-test")
        {
            Environment.SetEnvironmentVariable("TINYTASK_LAB_DATA",Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!,"ui-test-settings"));
            app.ShutdownMode=ShutdownMode.OnExplicitShutdown;app.Startup+=async(_,_)=>app.Shutdown(await UiTests.Run(args[1]));return app.Run();
        }
        if(args.FirstOrDefault()=="--dual-test" || args.FirstOrDefault()=="--roblox-test")
        {
            app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
            app.Startup+=async(_,_)=>{
                try {int code=args[0]=="--dual-test"?await DualTest.Run(args):await RobloxTest.Run(args);app.Shutdown(code);}
                catch(Exception e){try {int index=args[0]=="--dual-test"?2:3;if(args.Length>index)File.WriteAllText(args[index],JsonSerializer.Serialize(new{valid=false,error=e.ToString()}));}catch(Exception reportError){System.Diagnostics.Debug.WriteLine(reportError);}finally{app.Shutdown(1);}}
            };
            return app.Run();
        }
        app.DispatcherUnhandledException+=(_,e)=> {MessageBox.Show(e.Exception.Message,"TinyTask Input Lab"); e.Handled=true;};
        if(args.FirstOrDefault()=="--home-snapshot")
        {
            Environment.SetEnvironmentVariable("TINYTASK_LAB_DATA",Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!,"home-snapshot-settings"));
            var window=new HomeWindow();window.ConfigureSnapshot(args.Length>2?args[2]:"light");
            window.Loaded+=async(_,_)=>{
                try {await Task.Delay(500);window.UpdateLayout();var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(window);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using var stream=File.Create(args[1]);encoder.Save(stream);}
                finally {window.Close();}
            };
            app.Run(window);return 0;
        }
        if(args.FirstOrDefault()=="--snapshot" || args.FirstOrDefault()=="--lifecycle-test")
        {
            Environment.SetEnvironmentVariable("TINYTASK_LAB_DATA",Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!,"snapshot-settings"));
            Directory.CreateDirectory(MainWindow.DataDirectory);
            File.WriteAllText(Path.Combine(MainWindow.DataDirectory,"settings.json"),"{\"SetupSeen\":true}");
            var window=new MainWindow();
            window.Loaded+=async(_,_)=> {
                if(args[0]=="--lifecycle-test") {try{await window.LifecycleTest(args[1]);}catch(Exception e){File.WriteAllText(args[1],JsonSerializer.Serialize(new {error=e.ToString()}));}finally{window.Close();}return;}
                await Task.Delay(500);window.UpdateLayout();
                var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
                image.Render(window);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                using(var stream=File.Create(args[1]))encoder.Save(stream);window.Close();
            };
            app.Run(window);return 0;
        }
        Window mainWindow=AppIdentity.IsLab || args.FirstOrDefault()=="--diagnostics"?new MainWindow():new HomeWindow();
        string? launchReport=Environment.GetEnvironmentVariable("TINYTASK_LAB_LAUNCH_REPORT");
        if(!string.IsNullOrWhiteSpace(launchReport))mainWindow.Loaded+=(_,_)=>File.WriteAllText(Path.GetFullPath(launchReport),JsonSerializer.Serialize(new{pid=Environment.ProcessId,executable=AppIdentity.ExecutablePath,appId=AppIdentity.AppId,productGuid=AppIdentity.ProductGuid,title=mainWindow.Title,visible=mainWindow.IsVisible},new JsonSerializerOptions{WriteIndented=true}));
        app.Run(mainWindow);return 0;
    }
}

internal sealed class Fixture : Forms.Form
{
    private int clicks,keys,wheels;
    private readonly string identity;
    private readonly Forms.TextBox input=new(){Location=new Point(20,120),Size=new Size(390,28)};
    protected override bool ShowWithoutActivation=>identity!="work";
    protected override Forms.CreateParams CreateParams {get{var p=base.CreateParams;if(identity!="work")p.ExStyle|=0x08000000;return p;}}
    internal Fixture(string id)
    {
        identity=id;ClientSize=new Size(450,250);StartPosition=Forms.FormStartPosition.Manual;Location=new Point(80,80);AutoScaleMode=Forms.AutoScaleMode.None;
        Icon=System.Drawing.Icon.ExtractAssociatedIcon(AppIdentity.ExecutablePath);
        var label=new Forms.Label {Text="Safe target: button (80,80), text (80,135).\nCounters confirm effects, not just API return values.",AutoSize=true,Location=new Point(20,12)};
        var button=new ProbeButton(()=>{keys++;UpdateTitle();},()=>{wheels++;UpdateTitle();}){Text="Count click",Location=new Point(20,60),Size=new Size(180,38)};
        button.Click+=(_,_)=>{clicks++;UpdateTitle();};input.TextChanged+=(_,_)=>UpdateTitle();
        Controls.Add(label);Controls.Add(button);Controls.Add(input);UpdateTitle();
    }
    private void UpdateTitle()=>Text=$"TinyTask fixture {identity} | clicks={clicks};keys={keys};wheels={wheels};text={input.Text}";
    private sealed class ProbeButton(Action key,Action wheel) : Forms.Button
    {
        protected override void WndProc(ref Forms.Message m)
        {if(m.Msg==0x100 && m.WParam==0x75)key();if(m.Msg==0x20A)wheel();base.WndProc(ref m);}
    }
}

internal static class SelfTest
{
    internal static async Task<int> Run(string output)
    {
        var results=new System.Collections.Generic.List<object>();bool allPassed=true;
        void Check(string name,bool passed,string evidence) {results.Add(new {name,passed,evidence});allPassed&=passed;}
        Check("Signed coordinate packing",Native.Coordinates(-2,-3)==unchecked((nint)(int)0xFFFDFFFE),"Negative multi-monitor coordinate packing");
        bool rejected=false;try{Native.Coordinates(40000,0);}catch(ArgumentOutOfRangeException){rejected=true;}
        Check("Invalid coordinate rejection",rejected,"Out-of-range message coordinates rejected");
        var devices=RawInput.Devices();results.Add(new {name="Device enumeration",count=devices.Count,mouseCount=devices.Count(d=>d.Type==0),keyboardCount=devices.Count(d=>d.Type==1),result="Observed; physical two-device separation requires hardware test"});
        using var a=StartFixture("A");using var b=StartFixture("B");
        try
        {
            var targetA=await WaitForWindow(a);var targetB=await WaitForWindow(b);
            var foreground=Native.GetForegroundWindow();Native.GetCursorPos(out var cursorBefore);
            Check("Targets initially unfocused",foreground!=targetA.Handle&&foreground!=targetB.Handle,$"foreground={foreground};A={targetA.Handle};B={targetB.Handle}");
            await Task.WhenAll(Routing.Click(targetA,80,80,CancellationToken.None),Routing.Click(targetB,80,80,CancellationToken.None));
            await Task.Delay(200);
            Check("Two independent click targets",Native.Title(targetA.Handle).Contains("clicks=1;")&&Native.Title(targetB.Handle).Contains("clicks=1;"),Native.Title(targetA.Handle)+" / "+Native.Title(targetB.Handle));
            Routing.Key(targetA,80,80,0x75);Routing.Scroll(targetA,80,80,120);Routing.Text(targetA,80,135,"Probe Ω");await Task.Delay(200);
            Check("Targeted key down/up",Native.Title(targetA.Handle).Contains("keys=1;"),Native.Title(targetA.Handle));
            Check("Targeted scroll",Native.Title(targetA.Handle).Contains("wheels=1;"),Native.Title(targetA.Handle));
            Check("Unicode text to native edit",Native.Title(targetA.Handle).Contains("text=Probe Ω"),Native.Title(targetA.Handle));
            Check("Other target unchanged",Native.Title(targetB.Handle).Contains("keys=0;wheels=0;text="),Native.Title(targetB.Handle));
            try {int before=int.Parse(Native.Title(targetB.Handle).Split("clicks=")[1].Split(';')[0]);await Routing.Invoke(targetB,80,80,CancellationToken.None);await Task.Delay(200);Check("UIA Invoke",Native.Title(targetB.Handle).Contains($"clicks={before+1};"),Native.Title(targetB.Handle));}
            catch(Exception e) {Check("UIA Invoke",false,e.Message);}
            Native.GetCursorPos(out var cursorAfter);
            Check("System cursor unchanged",cursorBefore.X==cursorAfter.X&&cursorBefore.Y==cursorAfter.Y,$"before=({cursorBefore.X},{cursorBefore.Y});after=({cursorAfter.X},{cursorAfter.Y})");
            Check("OS focus unchanged",Native.GetForegroundWindow()==foreground,$"before={foreground};after={Native.GetForegroundWindow()}");
            using(var cancelled=new CancellationTokenSource()) {cancelled.Cancel();bool caught=false;try{await Routing.Click(targetA,80,80,cancelled.Token);}catch(OperationCanceledException){caught=true;}Check("Cancelled probe rejected",caught,"No down event posted for pre-cancelled token");}
            Native.ShowWindow(targetA.Handle,6);bool minimizedRejected=false;try{Routing.Receiver(targetA,80,80);}catch(InvalidOperationException){minimizedRejected=true;}
            Check("Minimized target rejected",minimizedRejected,"No silent foreground fallback");Native.ShowWindow(targetA.Handle,4);
            a.CloseMainWindow();await a.WaitForExitAsync();bool closedRejected=false;try{targetA.Validate();}catch(InvalidOperationException){closedRejected=true;}Check("Closed target rejected",closedRejected,"Target handle and PID checked");
        }
        finally {foreach(var process in new[]{a,b}) {if(!process.HasExited){process.CloseMainWindow();using var timeout=new CancellationTokenSource(2000);try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill();await process.WaitForExitAsync();}}}}
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output,JsonSerializer.Serialize(new {version="0.1.0",time=DateTimeOffset.UtcNow,os=Environment.OSVersion.ToString(),session=Process.GetCurrentProcess().SessionId,allPassed,scope="Controlled WinForms/native controls only. Not Roblox, browsers, macOS, or physical input suppression.",results},new JsonSerializerOptions {WriteIndented=true}));
        return allPassed?0:1;
    }
    private static Process StartFixture(string id)=>Process.Start(new ProcessStartInfo(AppIdentity.ExecutablePath){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--fixture",id}})!;
    private static async Task<Target> WaitForWindow(Process p)
    {
        for(int i=0;i<100;i++) {var target=Native.Windows().FirstOrDefault(t=>t.Pid==p.Id);if(target!=null)return target;if(p.HasExited)throw new Exception("Fixture exited before opening.");await Task.Delay(50);}
        throw new TimeoutException("Fixture window did not appear.");
    }
}
