using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace TinyTask;

// Window messages only. Never calls SendInput, SetCursorPos, SetForegroundWindow or UIA.
internal sealed class WindowPlayback
{
    private readonly Target target;
    private readonly Dictionary<string,nint> mouseReceivers=new();
    private readonly HashSet<int> textKeys=new();
    private nint keyboardReceiver;
    private int x,y;
    private uint buttons;
    internal WindowPlayback(Target target,MacroDocument macro)
    {
        target.Validate();
        if(macro.Coordinates!="client")throw new ArgumentException("Record a new macro in Advanced Mode first. Classic screen coordinates cannot safely target another window.");
        if(!string.Equals(macro.TargetProcess,target.Process,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Select the same application used for this recording.");
        if(macro.Actions.Any(a=>a.Type.StartsWith("key") && a.Key is 17 or 18 or 91 or 92 or 162 or 163 or 164 or 165))throw new ArgumentException("Background shortcuts using Ctrl, Alt or Windows are not supported. Use a recording with clicks and ordinary text.");
        this.target=target;keyboardReceiver=target.Handle;
        foreach(var action in macro.Actions.Where(a=>a.Type is "move" or "mouseDown" or "mouseUp" or "scroll"))Routing.Receiver(target,action.X,action.Y);
    }
    internal void Send(MacroAction action,bool position=true)
    {
        if(action.Type=="delay")return;
        target.Validate();
        if(action.Type.StartsWith("key"))
        {
            ValidateReceiver(keyboardReceiver);
            if(action.Key is 16 or 160 or 161)return; // Case is captured as text; no global modifier state.
            if(action.Text!=null && action.Type=="keyDown")
            {
                textKeys.Add(action.Key);
                if(position)foreach(char c in action.Text)Native.Post(keyboardReceiver,0x102,c,1);
                return;
            }
            if(action.Type=="keyUp" && textKeys.Remove(action.Key))return;
            uint scan=action.Scan!=0?(uint)action.Scan:Native.MapVirtualKey((uint)action.Key,0);
            uint flags=1 | scan<<16 | (action.Extended?1u<<24:0) | (action.Type=="keyUp"?0xC0000000u:0);
            Native.Post(keyboardReceiver,action.Type=="keyDown"?0x100u:0x101u,(nint)action.Key,unchecked((nint)(int)flags));return;
        }
        if(position){x=action.X;y=action.Y;}
        var (receiver,point)=Routing.Receiver(target,x,y);
        uint mask=action.Button switch{"left"=>1u,"right"=>2u,"middle"=>16u,"x1"=>32u,_=>64u};
        if(action.Type is "mouseDown" or "mouseUp")
        {
            if(mouseReceivers.TryGetValue(action.Button,out nint held))
            {receiver=held;point=new Native.Point(x,y);Native.ClientToScreen(target.Handle,ref point);Native.ScreenToClient(receiver,ref point);}
            ValidateReceiver(receiver);
            bool down=action.Type=="mouseDown";
            buttons=down?buttons|mask:buttons&~mask;
            uint msg=action.Button switch{"left"=>0x201u,"right"=>0x204u,"middle"=>0x207u,_=>0x20Bu};
            uint keys=buttons | (action.Button is "x1" or "x2"?(action.Button=="x1"?1u:2u)<<16:0);
            Native.Post(receiver,down?msg:msg+1,(nint)keys,Native.Coordinates(point.X,point.Y));
            if(down){mouseReceivers[action.Button]=receiver;keyboardReceiver=receiver;}else mouseReceivers.Remove(action.Button);
        }
        else if(action.Type=="move")Native.Post(receiver,0x200,(nint)buttons,Native.Coordinates(point.X,point.Y));
        else if(action.Type=="scroll")
        {var p=new Native.Point(x,y);Native.ClientToScreen(target.Handle,ref p);Native.Post(receiver,action.Horizontal?0x20Eu:0x20Au,unchecked((nint)(int)((uint)action.Delta<<16|buttons)),Native.Coordinates(p.X,p.Y));}
    }
    private void ValidateReceiver(nint receiver)
    {Native.GetWindowThreadProcessId(receiver,out uint pid);if(!Native.IsWindow(receiver) || pid!=target.Pid || Native.GetAncestor(receiver,2)!=target.Handle)throw new InvalidOperationException("A recorded control closed. Playback stopped.");}
    internal static string? RecordText(uint key,uint scan)
    {
        if(key is 16 or 17 or 18 or 160 or 161 or 162 or 163 or 164 or 165)return null;
        var state=new byte[256];state[key]=128;
        foreach(int k in new[]{16,17,18,160,161,162,163,164,165})if(Native.GetAsyncKeyState(k)<0)state[k]=128;
        state[20]=(byte)(GetKeyState(20)&1);
        var text=new StringBuilder(16);
        uint thread=Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out _);
        int count=ToUnicodeEx(key,scan,state,text,16,4,GetKeyboardLayout(thread));
        if(count<0)throw new InvalidOperationException("Dead-key/IME text needs Classic Mode; this window recording was stopped.");
        return count>0?text.ToString(0,Math.Min(count,16)):null;
    }
    [DllImport("user32.dll")] private static extern short GetKeyState(int key);
    [DllImport("user32.dll")] private static extern nint GetKeyboardLayout(uint thread);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int ToUnicodeEx(uint key,uint scan,byte[] state,StringBuilder text,int capacity,uint flags,nint layout);
}
