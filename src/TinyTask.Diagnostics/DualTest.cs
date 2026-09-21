using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace TinyTask;

internal static class DualTest
{
    internal static async Task<int> Run(string[] args)
    {
        string mode=args[1],report=Path.GetFullPath(args[2]);
        var results=new List<object>();var owned=new List<Process>();var overlay=new CursorWindow();Target? target=null,work=null;
        nint originalForeground=Native.GetForegroundWindow();
        try
        {
            int bx=80,by=80,tx=80,ty=135;
            if(mode=="native")
            {
                var p=Start("--fixture","dual-target");owned.Add(p);target=await Find(p.Id);
            }
            else if(mode=="browser")
            {
                string profile=Path.Combine(Path.GetDirectoryName(report)!,"browser-profile-"+Guid.NewGuid().ToString("N"));
                var start=new ProcessStartInfo(args[3]){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
                foreach(var a in new[]{"--no-first-run","--no-default-browser-check","--disable-background-mode","--force-renderer-accessibility","--user-data-dir="+profile,"--app="+new Uri(Path.GetFullPath(args[4])).AbsoluteUri})start.ArgumentList.Add(a);
                var p=Process.Start(start)!;owned.Add(p);
                for(int i=0;i<150;i++){target=Native.Windows().FirstOrDefault(t=>t.Pid==p.Id&&t.Title.StartsWith("TinyTask Browser Target"));if(target!=null)break;await Task.Delay(100);}
                if(target==null)throw new Exception("Browser test window unavailable.");
                // The outer runner bounds accessibility discovery to avoid a stuck provider.
                var root=AutomationElement.FromHandle(target.Handle);AutomationElement? button=null,input=null;
                for(int i=0;i<30;i++)
                {
                    button=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,"Count browser click"));
                    input=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,"Test text"));
                    if(button!=null&&input!=null)break;await Task.Delay(150);
                }
                if(button==null||input==null)throw new Exception("Browser controls not exposed.");
                (bx,by)=Center(target,button);(tx,ty)=Center(target,input);
            }
            else throw new ArgumentException("Expected native or browser.");
            var wp=Start("--fixture","work");owned.Add(wp);work=await Find(wp.Id);
            Native.SetWindowPos(work.Handle,0,850,120,450,300,0x14);
            var (receiver,_)=Routing.Receiver(target,bx,by);
            async Task Phase(string name,Func<Task> action)
            {
                if(!Native.SetForegroundWindow(work.Handle))throw new Exception("Cannot activate the work fixture; refusing an invalid background test.");
                await Task.Delay(200);
                if(Native.GetForegroundWindow()!=work.Handle)throw new Exception("Work app did not retain focus before "+name);
                var startTitle=Native.Title(target.Handle);var workTitle=Native.Title(work.Handle);
                Native.GetCursorPos(out var before);var watch=Stopwatch.StartNew();
                var samples=new List<object>();bool focusStable=true,cursorStable=true;
                using var stop=new CancellationTokenSource();
                Task sample=Task.Run(async()=>{
                    nint lastFocus=work.Handle;var lastCursor=before;
                    while(!stop.IsCancellationRequested)
                    {
                        nint f=Native.GetForegroundWindow();Native.GetCursorPos(out var c);
                        if(f!=work.Handle)focusStable=false;if(c.X!=before.X||c.Y!=before.Y)cursorStable=false;
                        if(f!=lastFocus||c.X!=lastCursor.X||c.Y!=lastCursor.Y){samples.Add(new {ms=watch.ElapsedMilliseconds,focus=f.ToString(),x=c.X,y=c.Y});lastFocus=f;lastCursor=c;}
                        try{await Task.Delay(5,stop.Token);}catch(OperationCanceledException){break;}
                    }
                });
                string? error=null;
                try{await action();await Task.Delay(500);}catch(Exception e){error=e.Message;}
                finally{stop.Cancel();await sample;}
                Native.GetCursorPos(out var after);
                focusStable &= Native.GetForegroundWindow()==work.Handle;cursorStable &= after.X==before.X&&after.Y==before.Y;
                results.Add(new {name,startedWithWorkFocused=true,focusStable,cursorStable,workContentUnchanged=workTitle==Native.Title(work.Handle),before=startTitle,after=Native.Title(target.Handle),cursorBefore=new[]{before.X,before.Y},cursorAfter=new[]{after.X,after.Y},changes=samples,error});
                File.WriteAllText(report,JsonSerializer.Serialize(new {mode,workHandle=work.Handle.ToString(),targetHandle=target.Handle.ToString(),receiver=receiver.ToString(),results},new JsonSerializerOptions{WriteIndented=true}));
            }
            await Phase("Baseline: no input, no second cursor",()=>Task.Delay(500));
            await Phase("Second cursor: visible movement only",async()=>{
                var point=new Native.Point(bx,by);Native.ClientToScreen(target.Handle,ref point);
                for(int i=0;i<15;i++){overlay.Place(point.X-30+i*2,point.Y);await Task.Delay(20);}
            });
            await Phase("Background click via window messages",()=>Routing.Click(target,bx,by,CancellationToken.None));
            await Phase("Background F6 down/up",()=>{Routing.Key(target,bx,by,0x75);return Task.CompletedTask;});
            await Phase("Background text field click",()=>Routing.Click(target,tx,ty,CancellationToken.None));
            await Phase("Background text via WM_CHAR",()=>{Routing.Text(target,tx,ty,"TT2");return Task.CompletedTask;});
            await Phase("Background UIA Invoke",()=>Routing.Invoke(target,bx,by,CancellationToken.None));
            return 0;
        }
        catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new {mode,error=e.Message,results},new JsonSerializerOptions{WriteIndented=true}));return 1;}
        finally
        {
            Routing.ReleaseAll();overlay.Close();
            var foreground=Native.GetForegroundWindow();bool restore=foreground==work?.Handle||foreground==target?.Handle;
            foreach(var p in owned){try{if(!p.HasExited){p.CloseMainWindow();using var timeout=new CancellationTokenSource(2000);try{await p.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){p.Kill(true);await p.WaitForExitAsync();}}}finally{p.Dispose();}}
            if(restore&&Native.IsWindow(originalForeground))Native.SetForegroundWindow(originalForeground);
        }
    }
    private static (int,int) Center(Target t,AutomationElement e){var b=e.Current.BoundingRectangle;var p=new Native.Point((int)(b.Left+b.Width/2),(int)(b.Top+b.Height/2));Native.ScreenToClient(t.Handle,ref p);return(p.X,p.Y);}
    private static Process Start(params string[] args){var s=new ProcessStartInfo(AppIdentity.ExecutablePath){UseShellExecute=false,CreateNoWindow=true};foreach(string a in args)s.ArgumentList.Add(a);return Process.Start(s)!;}
    private static async Task<Target> Find(int pid){for(int i=0;i<100;i++){var t=Native.Windows().FirstOrDefault(t=>t.Pid==pid);if(t!=null)return t;await Task.Delay(50);}throw new Exception("Fixture unavailable.");}
}
