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
    public string? Text {get;init;}
}
internal sealed class MacroDocument
{
    internal const int MaxFileBytes=64*1024*1024;
    public int Version {get;set;}=1;
    public string Platform {get;set;}="windows";
    public string Name {get;set;}="Untitled macro";
    public string Coordinates {get;set;}="screen";
    public string? TargetProcess {get;set;}
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
        if(Coordinates is not ("screen" or "client"))throw new ArgumentException("Unknown coordinate format.");
        foreach(var a in Actions)
        {
            if(a==null || !double.IsFinite(a.Delay) || a.Delay<0 || a.Delay>86400)throw new ArgumentException("Invalid action delay.");
            if(a.Text?.Length>16)throw new ArgumentException("A recorded text event is too long.");
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
    internal event Action? Disarmed;
    internal event Action<string>? Failed;
    internal Func<int,bool> IsControlKey {get;set;}=key=>key is 119 or 120 or 121;
    internal Func<int,bool> IsStopKey {get;set;}=key=>false;
    internal Func<IReadOnlyList<MacroAction>,bool>? ContainsControlSequence {get;set;}
    private readonly HashSet<int> ignoredControlKeys=new();
    private readonly Hook mouseCallback,keyboardCallback;
    private nint mouseHook,keyboardHook;
    private readonly Stopwatch recordClock=new(),playClock=new();
    private double lastRecord,lastMove;
    private CancellationTokenSource? playback;
    private readonly Dictionary<string,MacroAction> held=new();
    private bool disposed;
    private Target? recordingTarget;
    private WindowPlayback? windowPlayback;
    private MacroDocument? preparedSource;
    private double preparedSpeed;
    private (int,int,int,int) preparedDisplay;
    private static (int,int,int,int) DisplayMetrics()=>(Native.GetSystemMetrics(76),Native.GetSystemMetrics(77),Native.GetSystemMetrics(78),Native.GetSystemMetrics(79));
    private MacroAction[]? preparedActions;
    private Input[][]? preparedInputs;
    private PlaybackTimeline? preparedTimeline;
    internal bool Armed {get;private set;}
    internal void Prepare(MacroDocument macro,double speed,bool arm=false)
    {
        if(IsBusy)throw new InvalidOperationException("Stop playback before preparing another task.");
        macro.Validate();
        if(macro.Coordinates!="screen")throw new ArgumentException("Session playback currently supports Classic screen recordings.");
        if(macro.Actions.Count==0)throw new ArgumentException("Record or open a macro first.");
        if(ContainsControlSequence?.Invoke(macro.Actions)??macro.Actions.Any(a=>a.Type.StartsWith("key")&&IsControlKey(a.Key)))throw new ArgumentException("This macro contains a playback control shortcut.");
        var actions=macro.Actions.Select(a=>a with{}).ToArray();
        var timeline=new PlaybackTimeline(actions.Select(a=>a.Delay).ToArray(),speed);
        var inputs=actions.Select(a=>PrepareInput(a,true)).ToArray();
        preparedSource=macro;preparedSpeed=speed;preparedDisplay=DisplayMetrics();preparedActions=actions;preparedTimeline=timeline;preparedInputs=inputs;
        if(arm&&keyboardHook==0){keyboardHook=SetWindowsHookEx(13,keyboardCallback,GetModuleHandle(null),0);if(keyboardHook==0)throw new Win32Exception(Marshal.GetLastWin32Error(),"Emergency stop could not be armed.");}
        Armed=arm;
    }
    internal bool IsBusy=>State!="Ready";
    internal bool AcceptInjectedForTest {get;set;}
    internal double MaximumLatenessSeconds {get;set;}=0.5;
    internal double LastLatenessMilliseconds {get;private set;}
    internal double PeakLatenessMilliseconds {get;private set;}
    internal List<object> TestMousePackets {get;}=new();
    internal ClassicEngine(){mouseCallback=MouseHook;keyboardCallback=KeyboardHook;}
    private void SetState(string state){State=state;Changed?.Invoke();}
    internal void Record(Target? target=null)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(IsBusy)throw new InvalidOperationException("Stop the current task first.");
        target?.Validate();recordingTarget=target;
        Recording=new(){Name="Macro "+DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"),Coordinates=target==null?"screen":"client",TargetProcess=target?.Process};lastRecord=lastMove=0;
        mouseHook=SetWindowsHookEx(14,mouseCallback,GetModuleHandle(null),0);
        keyboardHook=SetWindowsHookEx(13,keyboardCallback,GetModuleHandle(null),0);
        if(mouseHook==0 || keyboardHook==0){int error=Marshal.GetLastWin32Error();Unhook();throw new Win32Exception(error,"Recording hooks could not be installed.");}
        ignoredControlKeys.Clear();recordClock.Restart();SetState("Recording");
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
                    if(recordingTarget!=null)
                    {
                        recordingTarget.Validate();
                        if(Native.GetAncestor(Native.WindowFromPoint(m.Point),2)!=recordingTarget.Handle)return CallNextHookEx(0,code,message,data);
                        Native.ScreenToClient(recordingTarget.Handle,ref m.Point);
                    }
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
                if((k.Key==121||IsStopKey((int)k.Key)) && (State!="Ready"||Armed) && ((k.Flags&0x10)==0 || (AcceptInjectedForTest && k.Extra==Marker)))
                {
                    if((int)message is 0x100 or 0x104)System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(Stop);
                    return 1;
                }
                bool control=IsControlKey((int)k.Key);
                if(control&&(int)message is 0x100 or 0x104)ignoredControlKeys.Add((int)k.Key);
                bool ignored=ignoredControlKeys.Contains((int)k.Key);
                if((int)message is 0x101 or 0x105)ignoredControlKeys.Remove((int)k.Key);
                if(State=="Recording" && (AcceptInjectedForTest ? k.Extra==Marker : (k.Flags&0x10)==0) && !control && !ignored && !OwnWindow(Native.GetForegroundWindow()) && (recordingTarget==null || Native.GetForegroundWindow()==recordingTarget.Handle))
                    Append(new(){Type=((int)message is 0x100 or 0x104)?"keyDown":"keyUp",Key=(int)k.Key,Scan=(int)k.Scan,Extended=(k.Flags&1)!=0,Text=recordingTarget!=null && (int)message==0x100?WindowPlayback.RecordText(k.Key,k.Scan):null});
            }
        }catch(Exception e){Unhook();SetState("Ready");Failed?.Invoke("Recording stopped: "+e.Message);}
        return CallNextHookEx(0,code,message,data);
    }
    internal async Task Play(MacroDocument macro,double speed,int loops,bool continuous,double startDelaySeconds=0,Target? backgroundTarget=null,double? synchronizedStart=null)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(IsBusy)throw new InvalidOperationException("Stop the current task first.");
        macro.Validate();
        if(backgroundTarget==null && macro.Coordinates!="screen")throw new ArgumentException("Play this window recording in Advanced Mode with its target app selected.");
        var background=backgroundTarget==null?null:new WindowPlayback(backgroundTarget,macro);
        if(macro.Actions.Count==0)throw new ArgumentException("Record or open a macro first.");
        if(ContainsControlSequence?.Invoke(macro.Actions)??macro.Actions.Any(a=>a.Type.StartsWith("key") && IsControlKey(a.Key)))throw new ArgumentException("This macro contains a configured control hotkey. Change the recording/playback hotkeys before replaying it.");
        if(!double.IsFinite(speed) || speed<0.01 || speed>1000 || loops<1 || loops>1000000)throw new ArgumentException("Speed must be 0.01–1000x and loops 1–1,000,000.");
        if(new[]{0x10,0x11,0x12,0x5B,0x5C}.Any(k=>(Native.GetAsyncKeyState(k)&0x8000)!=0))throw new InvalidOperationException("Release modifier keys before playback.");
        if(!double.IsFinite(startDelaySeconds) || startDelaySeconds<0)throw new ArgumentException("Invalid scheduled start delay.");
        if(background==null&&(preparedSource!=macro||preparedSpeed!=speed||preparedDisplay!=DisplayMetrics()||!macro.Actions.SequenceEqual(preparedActions!)))Prepare(macro,speed,Armed);
        var actions=background==null?preparedActions!:macro.Actions.Select(a=>a with{}).ToArray();
        var timeline=background==null?preparedTimeline!:new PlaybackTimeline(actions.Select(a=>a.Delay).ToArray(),speed);
        var prepared=background==null?preparedInputs:null;
        if(synchronizedStart!=null&&!double.IsFinite(synchronizedStart.Value))throw new ArgumentException("Invalid synchronized start.");
        if(!double.IsFinite(MaximumLatenessSeconds)||MaximumLatenessSeconds<=0)throw new ArgumentException("Invalid lateness safety limit.");
        using var waiter=new PrecisionWait();
        if(keyboardHook==0)keyboardHook=SetWindowsHookEx(13,keyboardCallback,GetModuleHandle(null),0);
        if(keyboardHook==0)throw new Win32Exception(Marshal.GetLastWin32Error(),"The modifier-independent F10 stop hook could not be installed.");
        windowPlayback=background;
        using var cancel=new CancellationTokenSource();playback=cancel;PeakLatenessMilliseconds=0;
        EventHandler displayChanged=(_,_)=>cancel.Cancel();
        try
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=displayChanged;
            playClock.Restart();
            if(synchronizedStart!=null)startDelaySeconds=synchronizedStart.Value-SessionConnection.Now;
            Armed=false;SetState("Playing");
            for(long loop=0;continuous || loop<loops;loop++)
            {
                int burst=0;
                for(int index=0;index<actions.Length;index++)
                {
                    var action=actions[index];
                    double due=timeline.Due(loop,index)+startDelaySeconds;
                    while(State=="Paused" || playClock.Elapsed.TotalSeconds<due)
                        await waiter.Delay(State=="Paused"?0.02:Math.Min(due-playClock.Elapsed.TotalSeconds,0.02),cancel.Token);
                    cancel.Token.ThrowIfCancellationRequested();
                    LastLatenessMilliseconds=Math.Max(0,(playClock.Elapsed.TotalSeconds-due)*1000);
                    PeakLatenessMilliseconds=Math.Max(PeakLatenessMilliseconds,LastLatenessMilliseconds);
                    if(LastLatenessMilliseconds>MaximumLatenessSeconds*1000)throw new InvalidOperationException("Playback stopped because input fell too far behind its original timeline. No overdue backlog was replayed.");
                    // Track down events before injection so partial SendInput failure still releases them.
                    if(action.Type is "keyDown" or "mouseDown")Track(action);
                    if(prepared!=null)InjectPrepared(prepared[index]);else Dispatch(action);
                    Track(action);
                    // Yield even for zero-delay/100x macros so Stop and hotkeys stay responsive.
                    if(++burst%64==0)await Task.Delay(1,cancel.Token);
                }
                ReleaseHeld(true);
                if(continuous)await Task.Delay(1,cancel.Token);
            }
        }
        catch(OperationCanceledException) when(cancel.IsCancellationRequested){}
        finally {Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=displayChanged;playClock.Stop();playback=null;ReleaseHeld(true);windowPlayback=null;Unhook();SetState("Ready");}
    }
    private static string HeldId(MacroAction a)=>a.Type.StartsWith("key")?"key:"+a.Key:"mouse:"+a.Button;
    private void Track(MacroAction a){if(a.Type is "keyDown" or "mouseDown")held[HeldId(a)]=a;else if(a.Type is "keyUp" or "mouseUp")held.Remove(HeldId(a));}
    internal void PauseResume()
    {
        if(State=="Playing"){playClock.Stop();SetState("Paused");ReleaseHeld(false);}
        else if(State=="Paused"){foreach(var a in held.Values)Dispatch(a,windowPlayback==null);playClock.Start();SetState("Playing");}
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
        bool wasArmed=Armed;Armed=false;playback?.Cancel();ReleaseHeld(true);if(State=="Ready")Unhook();if(wasArmed)Disarmed?.Invoke();
    }
    private void ReleaseHeld(bool clear)
    {
        foreach(var a in held.Values){try{Dispatch(a with{Type=a.Type=="keyDown"?"keyUp":"mouseUp"},positionMouse:false);}catch(Exception e){Failed?.Invoke("Could not release input: "+e.Message);}}
        if(clear)held.Clear();
    }
    private void Unhook(){if(mouseHook!=0){UnhookWindowsHookEx(mouseHook);mouseHook=0;}if(keyboardHook!=0){UnhookWindowsHookEx(keyboardHook);keyboardHook=0;}}
    public void Dispose(){if(disposed)return;disposed=true;Stop();Unhook();}
    private void Dispatch(MacroAction action,bool positionMouse=true){if(windowPlayback!=null)windowPlayback.Send(action,positionMouse);else Send(action,positionMouse);}
    internal static void Send(MacroAction a,bool positionMouse=true)=>InjectPrepared(PrepareInput(a,positionMouse));
    private static Input[] PrepareInput(MacroAction a,bool positionMouse)
    {
        var inputs=new List<Input>(2);
        if(a.Type=="delay")return Array.Empty<Input>();
        if(a.Type.StartsWith("key"))
        {
            bool scan=a.Scan!=0;
            inputs.Add(new Input{Type=1,Data=new(){Keyboard=new(){Key=scan?(ushort)0:(ushort)a.Key,Scan=(ushort)a.Scan,Flags=(scan?8u:0u)|(a.Extended?1u:0u)|(a.Type=="keyUp"?2u:0u),Extra=Marker}}});return inputs.ToArray();
        }
        if(positionMouse)
        {
            int left=Native.GetSystemMetrics(76),top=Native.GetSystemMetrics(77),width=Native.GetSystemMetrics(78),height=Native.GetSystemMetrics(79);
            if(a.X<left || a.Y<top || a.X>=left+width || a.Y>=top+height)throw new InvalidOperationException("A macro position is outside the current desktop. Restore the original display layout.");
            inputs.Add(new Input{Data=new(){Mouse=new(){X=(int)((long)(a.X-left)*65535/Math.Max(1,width-1)),Y=(int)((long)(a.Y-top)*65535/Math.Max(1,height-1)),Flags=0xC001,Extra=Marker}}});
        }
        if(a.Type=="move")return inputs.ToArray();
        uint flags=a.Type=="scroll"?(a.Horizontal?0x1000u:0x800u):a.Button switch{"left"=>a.Type=="mouseDown"?2u:4u,"right"=>a.Type=="mouseDown"?8u:16u,"middle"=>a.Type=="mouseDown"?32u:64u,_=>a.Type=="mouseDown"?128u:256u};
        uint data=a.Type=="scroll"?unchecked((uint)a.Delta):a.Button=="x1"?1u:a.Button=="x2"?2u:0;
        inputs.Add(new Input{Data=new(){Mouse=new(){Flags=flags,Data=data,Extra=Marker}}});
        return inputs.ToArray();
    }
    private const nuint Marker=0x545432;
    private static void InjectPrepared(Input[] inputs){if(inputs.Length>0 && SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<Input>())!=(uint)inputs.Length)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows rejected playback input. Elevated or protected apps may not accept it.");}
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
