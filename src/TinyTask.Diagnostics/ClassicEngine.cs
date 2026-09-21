using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TinyTask;

internal sealed record MacroAction
{
    public string Type {get;init;}="delay";
    public double Delay {get;set;}
    public int X {get;init;}
    public int Y {get;init;}
    public string Button {get;init;}="left";
    public int Key {get;init;}
    public int Scan {get;init;}
    public bool Extended {get;init;}
    public int Delta {get;init;}
    public bool Horizontal {get;init;}
}
internal sealed class MacroDocument
{
    internal const int MaxFileBytes=64*1024*1024;
    public int Version {get;set;}=1;
    public string Platform {get;set;}="windows";
    public string Name {get;set;}="Untitled macro";
    public List<MacroAction> Actions {get;set;}=new();
    internal static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true,WriteIndented=true};
    internal static MacroDocument Parse(string json)
    {
        var macro=JsonSerializer.Deserialize<MacroDocument>(json,Json)??throw new ArgumentException("Empty macro file.");
        macro.Validate();return macro;
    }
    internal void Validate()
    {
        if(Version!=1 || Platform!="windows")throw new ArgumentException("This macro needs its original platform. Windows Classic supports version 1 Windows macros.");
        if(Actions==null || Actions.Count>100000)throw new ArgumentException("Macro is too large (maximum 100,000 actions).");
        foreach(var a in Actions)
        {
            if(a==null || !double.IsFinite(a.Delay) || a.Delay<0 || a.Delay>86400)throw new ArgumentException("Invalid action delay.");
            if(!new[]{"move","mouseDown","mouseUp","scroll","keyDown","keyUp","delay"}.Contains(a.Type))throw new ArgumentException("Unsupported action: "+a.Type);
            if(a.Type.StartsWith("mouse") && !new[]{"left","right","middle","x1","x2"}.Contains(a.Button))throw new ArgumentException("Unknown mouse button.");
            if(a.Type.StartsWith("key") && (a.Key<1 || a.Key>254 || a.Scan<0 || a.Scan>255))throw new ArgumentException("Invalid key code.");
            if(a.Type=="scroll" && (a.Delta < -32768 || a.Delta>32767))throw new ArgumentException("Invalid scroll amount.");
        }
    }
}

// All state lives on the WPF dispatcher. Hook callbacks only append bounded records.
internal sealed class ClassicEngine : IDisposable
{
    internal string State {get;private set;}="Ready";
    internal MacroDocument Recording {get;private set;}=new();
    internal event Action? Changed;
    internal event Action<string>? Failed;
    internal Func<int,bool> IsControlKey {get;set;}=key=>key is 119 or 120 or 121;
    private readonly Hook mouseCallback,keyboardCallback;
    private nint mouseHook,keyboardHook;
    private readonly Stopwatch recordClock=new(),playClock=new();
    private double lastRecord,lastMove;
    private CancellationTokenSource? playback;
    private readonly Dictionary<string,MacroAction> held=new();
    private bool disposed;
    internal bool IsBusy=>State!="Ready";
    internal bool AcceptInjectedForTest {get;set;}
    internal List<object> TestMousePackets {get;}=new();
    internal ClassicEngine(){mouseCallback=MouseHook;keyboardCallback=KeyboardHook;}
    private void SetState(string state){State=state;Changed?.Invoke();}
    internal void Record()
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(IsBusy)throw new InvalidOperationException("Stop the current task first.");
        Recording=new(){Name="Macro "+DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")};lastRecord=lastMove=0;
        mouseHook=SetWindowsHookEx(14,mouseCallback,GetModuleHandle(null),0);
        keyboardHook=SetWindowsHookEx(13,keyboardCallback,GetModuleHandle(null),0);
        if(mouseHook==0 || keyboardHook==0){int error=Marshal.GetLastWin32Error();Unhook();throw new Win32Exception(error,"Recording hooks could not be installed.");}
        recordClock.Restart();SetState("Recording");
    }
    private bool OwnWindow(nint window){Native.GetWindowThreadProcessId(window,out uint pid);return pid==Environment.ProcessId;}
    private void Append(MacroAction action)
    {
        double now=recordClock.Elapsed.TotalSeconds;
        if(action.Type=="move" && now-lastMove<0.008)return;
        if(action.Type=="move")lastMove=now;
        if(Recording.Actions.Count>=99700){System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(()=>{Stop();Failed?.Invoke("Recording stopped at the action limit. Save this macro before continuing.");});return;}
        action.Delay=Math.Max(0,now-lastRecord);lastRecord=now;Recording.Actions.Add(action);
    }
    private nint MouseHook(int code,nint message,nint data)
    {
        try
        {
            if(code>=0 && State=="Recording")
            {
                var m=Marshal.PtrToStructure<MouseData>(data);
                if(AcceptInjectedForTest)TestMousePackets.Add(new{message=(int)message,x=m.Point.X,y=m.Point.Y,m.Data,m.Flags,extra=m.Extra.ToString(),own=OwnWindow(Native.WindowFromPoint(m.Point))});
                if((AcceptInjectedForTest ? m.Extra==Marker : (m.Flags&1)==0) && !OwnWindow(Native.WindowFromPoint(m.Point)))
                {
                    int msg=(int)message;
                    string? type=msg switch{0x200=>"move",0x201 or 0x204 or 0x207 or 0x20B=>"mouseDown",0x202 or 0x205 or 0x208 or 0x20C=>"mouseUp",0x20A or 0x20E=>"scroll",_=>null};
                    if(type!=null)Append(new(){Type=type,X=m.Point.X,Y=m.Point.Y,Button=msg switch{0x204 or 0x205=>"right",0x207 or 0x208=>"middle",0x20B or 0x20C=>(m.Data>>16)==1?"x1":"x2",_=>"left"},Delta=unchecked((short)(m.Data>>16)),Horizontal=msg==0x20E});
                }
            }
        }catch(Exception e){Unhook();SetState("Ready");Failed?.Invoke("Recording stopped: "+e.Message);}
        return CallNextHookEx(0,code,message,data);
    }
    private nint KeyboardHook(int code,nint message,nint data)
    {
        try
        {
            if(code>=0)
            {
                var k=Marshal.PtrToStructure<KeyData>(data);
                if(k.Key==121 && State!="Ready" && ((k.Flags&0x10)==0 || (AcceptInjectedForTest && k.Extra==Marker)))
                {
                    if((int)message is 0x100 or 0x104)System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(Stop);
                    return 1;
                }
                if(State=="Recording" && (AcceptInjectedForTest ? k.Extra==Marker : (k.Flags&0x10)==0) && !IsControlKey((int)k.Key) && !OwnWindow(Native.GetForegroundWindow()))
                    Append(new(){Type=((int)message is 0x100 or 0x104)?"keyDown":"keyUp",Key=(int)k.Key,Scan=(int)k.Scan,Extended=(k.Flags&1)!=0});
            }
        }catch(Exception e){Unhook();SetState("Ready");Failed?.Invoke("Recording stopped: "+e.Message);}
        return CallNextHookEx(0,code,message,data);
    }
    internal async Task Play(MacroDocument macro,double speed,int loops,bool continuous,double startDelaySeconds=3)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(IsBusy)throw new InvalidOperationException("Stop the current task first.");
        macro.Validate();
        if(macro.Actions.Count==0)throw new ArgumentException("Record or open a macro first.");
        if(macro.Actions.Any(a=>a.Type.StartsWith("key") && IsControlKey(a.Key)))throw new ArgumentException("This macro contains a configured control hotkey. Change the recording/playback hotkeys before replaying it.");
        if(!double.IsFinite(speed) || speed<0.01 || speed>1000 || loops<1 || loops>1000000)throw new ArgumentException("Speed must be 0.01–1000x and loops 1–1,000,000.");
        if(new[]{0x10,0x11,0x12,0x5B,0x5C}.Any(k=>(Native.GetAsyncKeyState(k)&0x8000)!=0))throw new InvalidOperationException("Release modifier keys before playback.");
        keyboardHook=SetWindowsHookEx(13,keyboardCallback,GetModuleHandle(null),0);
        if(keyboardHook==0)throw new Win32Exception(Marshal.GetLastWin32Error(),"The modifier-independent F10 stop hook could not be installed.");
        using var cancel=new CancellationTokenSource();playback=cancel;playClock.Restart();SetState("Playing");
        double due=startDelaySeconds;
        try
        {
            for(int loop=0;continuous || loop<loops;loop++)
            {
                int burst=0;
                foreach(var action in macro.Actions)
                {
                    due+=action.Delay/speed;
                    while(State=="Paused" || playClock.Elapsed.TotalSeconds<due)
                        await Task.Delay(State=="Paused"?20:Math.Clamp((int)((due-playClock.Elapsed.TotalSeconds)*1000),1,20),cancel.Token);
                    cancel.Token.ThrowIfCancellationRequested();
                    Send(action);Track(action);
                    // Yield even for zero-delay/100x macros so Stop and hotkeys stay responsive.
                    if(++burst%64==0)await Task.Delay(1,cancel.Token);
                }
                ReleaseHeld(true);
                if(continuous)await Task.Delay(1,cancel.Token);
            }
        }
        catch(OperationCanceledException) when(cancel.IsCancellationRequested){}
        finally {playClock.Stop();playback=null;ReleaseHeld(true);Unhook();SetState("Ready");}
    }
    private static string HeldId(MacroAction a)=>a.Type.StartsWith("key")?"key:"+a.Key:"mouse:"+a.Button;
    private void Track(MacroAction a){if(a.Type is "keyDown" or "mouseDown")held[HeldId(a)]=a;else if(a.Type is "keyUp" or "mouseUp")held.Remove(HeldId(a));}
    internal void PauseResume()
    {
        if(State=="Playing"){playClock.Stop();SetState("Paused");ReleaseHeld(false);}
        else if(State=="Paused"){foreach(var a in held.Values)Send(a);playClock.Start();SetState("Playing");}
    }
    internal void Stop()
    {
        if(State=="Recording")
        {
            recordClock.Stop();Unhook();
            double tail=recordClock.Elapsed.TotalSeconds-lastRecord;
            if(tail>0)Recording.Actions.Add(new(){Type="delay",Delay=tail});
            // End incomplete presses in the saved recording; no system input is sent here.
            var open=new Dictionary<string,MacroAction>();
            foreach(var a in Recording.Actions){if(a.Type is "keyDown" or "mouseDown")open[HeldId(a)]=a;else if(a.Type is "keyUp" or "mouseUp")open.Remove(HeldId(a));}
            foreach(var a in open.Values)Recording.Actions.Add(a with{Type=a.Type=="keyDown"?"keyUp":"mouseUp",Delay=0});
            SetState("Ready");
        }
        playback?.Cancel();ReleaseHeld(true);
    }
    private void ReleaseHeld(bool clear)
    {
        foreach(var a in held.Values){try{Send(a with{Type=a.Type=="keyDown"?"keyUp":"mouseUp"},positionMouse:false);}catch(Exception e){Failed?.Invoke("Could not release input: "+e.Message);}}
        if(clear)held.Clear();
    }
    private void Unhook(){if(mouseHook!=0){UnhookWindowsHookEx(mouseHook);mouseHook=0;}if(keyboardHook!=0){UnhookWindowsHookEx(keyboardHook);keyboardHook=0;}}
    public void Dispose(){if(disposed)return;disposed=true;Stop();Unhook();}
    internal static void Send(MacroAction a,bool positionMouse=true)
    {
        if(a.Type=="delay")return;
        if(a.Type.StartsWith("key"))
        {
            bool scan=a.Scan!=0;
            Inject(new Input{Type=1,Data=new(){Keyboard=new(){Key=scan?(ushort)0:(ushort)a.Key,Scan=(ushort)a.Scan,Flags=(scan?8u:0u)|(a.Extended?1u:0u)|(a.Type=="keyUp"?2u:0u),Extra=Marker}}});return;
        }
        if(positionMouse)
        {
            int left=Native.GetSystemMetrics(76),top=Native.GetSystemMetrics(77),width=Native.GetSystemMetrics(78),height=Native.GetSystemMetrics(79);
            if(a.X<left || a.Y<top || a.X>=left+width || a.Y>=top+height)throw new InvalidOperationException("A macro position is outside the current desktop. Restore the original display layout.");
            Inject(new Input{Data=new(){Mouse=new(){X=(int)((long)(a.X-left)*65535/Math.Max(1,width-1)),Y=(int)((long)(a.Y-top)*65535/Math.Max(1,height-1)),Flags=0xC001,Extra=Marker}}});
        }
        if(a.Type=="move")return;
        uint flags=a.Type=="scroll"?(a.Horizontal?0x1000u:0x800u):a.Button switch{"left"=>a.Type=="mouseDown"?2u:4u,"right"=>a.Type=="mouseDown"?8u:16u,"middle"=>a.Type=="mouseDown"?32u:64u,_=>a.Type=="mouseDown"?128u:256u};
        uint data=a.Type=="scroll"?unchecked((uint)a.Delta):a.Button=="x1"?1u:a.Button=="x2"?2u:0;
        Inject(new Input{Data=new(){Mouse=new(){Flags=flags,Data=data,Extra=Marker}}});
    }
    private const nuint Marker=0x545432;
    private static void Inject(Input input){if(SendInput(1,new[]{input},Marshal.SizeOf<Input>())!=1)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows rejected playback input. Elevated or protected apps may not accept it.");}
    private delegate nint Hook(int code,nint message,nint data);
    [StructLayout(LayoutKind.Sequential)] private struct MouseData {public Native.Point Point;public uint Data,Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)] private struct KeyData {public uint Key,Scan,Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)] private struct Input {public uint Type;public InputUnion Data;}
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion {[FieldOffset(0)]public MouseInput Mouse;[FieldOffset(0)]public KeyInput Keyboard;}
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput {public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)] private struct KeyInput {public ushort Key,Scan;public uint Flags,Time;public nuint Extra;}
    [DllImport("user32.dll",SetLastError=true)] private static extern nint SetWindowsHookEx(int id,Hook callback,nint module,uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook,int code,nint message,nint data);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint count,Input[] inputs,int size);
}
