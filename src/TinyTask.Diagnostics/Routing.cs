using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace TinyTask;

internal static class Routing
{
    private static readonly ConcurrentDictionary<nint,(uint Pid,nint Position)> held=new();
    private static readonly ConcurrentDictionary<int,Process> workers=new();
    internal static void ReleaseAll()
    {
        foreach(var item in held)
        {
            Native.GetWindowThreadProcessId(item.Key,out uint pid);
            if(pid==item.Value.Pid)Native.PostMessage(item.Key,0x202,0,item.Value.Position);
            held.TryRemove(item.Key,out _);
        }
        foreach(var worker in workers.Values) {try{if(!worker.HasExited)worker.Kill();}catch(InvalidOperationException){} }
    }
    internal static (nint Window,Native.Point Point) Receiver(Target target,int x,int y)
    {
        target.Validate();
        if(!Native.GetClientRect(target.Handle,out var rect) || x<0 || y<0 || x>=rect.Right || y>=rect.Bottom)
            throw new ArgumentOutOfRangeException(nameof(x),"Coordinates must be inside the target client area (physical pixels).");
        nint hwnd=target.Handle; var point=new Native.Point(x,y);
        for(int depth=0;depth<20;depth++)
        {
            nint child=Native.ChildWindowFromPointEx(hwnd,point,7);
            if(child==0 || child==hwnd) break;
            Native.ClientToScreen(hwnd,ref point); Native.ScreenToClient(child,ref point); hwnd=child;
        }
        return (hwnd,point);
    }
    internal static async Task Click(Target target,int x,int y,CancellationToken token)
    {
        var (hwnd,point)=Receiver(target,x,y); nint xy=Native.Coordinates(point.X,point.Y);
        token.ThrowIfCancellationRequested();
        Native.Post(hwnd,0x200,0,xy);
        bool down=false;
        try { Native.Post(hwnd,0x201,1,xy); down=true;held[hwnd]=(target.Pid,xy); await Task.Delay(40,token); }
        finally { if(down && held.TryRemove(hwnd,out _) && Native.IsWindow(hwnd)) Native.Post(hwnd,0x202,0,xy); }
    }
    internal static void Key(Target target,int x,int y,uint key)
    {
        var (hwnd,_)=Receiver(target,x,y); uint scan=Native.MapVirtualKey(key,0);
        Native.Post(hwnd,0x100,(nint)key,(nint)(1 | scan<<16));
        Native.Post(hwnd,0x101,(nint)key,unchecked((nint)(long)(0xC0000001u | scan<<16)));
    }
    internal static void Text(Target target,int x,int y,string text)
    {
        if(text.Length>256) throw new ArgumentException("Diagnostic text is limited to 256 characters.");
        var (hwnd,_)=Receiver(target,x,y);
        foreach(char c in text) Native.Post(hwnd,0x102,(nint)c,1);
    }
    internal static void Scroll(Target target,int x,int y,int delta)
    {
        var (hwnd,_)=Receiver(target,x,y); var screen=new Native.Point(x,y); Native.ClientToScreen(target.Handle,ref screen);
        Native.Post(hwnd,0x20A,(nint)(delta<<16),Native.Coordinates(screen.X,screen.Y));
    }
    // UIA providers can hang. Run them in an expendable process, never the UI thread.
    internal static async Task Invoke(Target target,int x,int y,CancellationToken token)
    {
        Receiver(target,x,y);
        using var worker=Process.Start(new ProcessStartInfo(AppIdentity.ExecutablePath) { UseShellExecute=false,CreateNoWindow=true,
            ArgumentList={"--uia",target.Handle.ToString(),target.Pid.ToString(),x.ToString(),y.ToString()} }) ?? throw new InvalidOperationException("Cannot start UI Automation worker.");
        workers[worker.Id]=worker;
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(4000);
        try { await worker.WaitForExitAsync(timeout.Token); }
        catch(OperationCanceledException) { if(!worker.HasExited) worker.Kill(); throw; }
        finally {workers.TryRemove(worker.Id,out _);}
        if(worker.ExitCode!=0) throw new InvalidOperationException("UI Automation Invoke is unavailable or failed for this control.");
    }
    internal static int UiaWorker(string[] args)
    {
        try
        {
            var target=new Target((nint)long.Parse(args[1]),uint.Parse(args[2]),"",""); target.Validate();
            var point=new Native.Point(int.Parse(args[3]),int.Parse(args[4])); Native.ClientToScreen(target.Handle,ref point);
            var root=AutomationElement.FromHandle(target.Handle);
            var items=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.IsInvokePatternAvailableProperty,true));
            var candidates=items.Cast<AutomationElement>().Where(e=>e.Current.BoundingRectangle.Contains(point.X,point.Y)).OrderBy(e=>e.Current.BoundingRectangle.Width*e.Current.BoundingRectangle.Height);
            var element=candidates.FirstOrDefault();
            if(element==null || !element.TryGetCurrentPattern(InvokePattern.Pattern,out object pattern)) return 2;
            ((InvokePattern)pattern).Invoke(); return 0;
        }
        catch { return 3; }
    }
}
