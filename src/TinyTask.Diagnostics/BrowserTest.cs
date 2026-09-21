using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace TinyTask;

internal static class BrowserTest
{
    internal static async Task<int> Run(string[] args)
    {
        string browser=args[1],report=Path.GetFullPath(args[3]);
        Target? target=null;
        Process? work=null;
        Process? process=null;
        try
        {
            string page=Path.GetFullPath(args[2]);
            string profile=Path.Combine(Path.GetDirectoryName(report)!,"browser-profile-"+Guid.NewGuid().ToString("N"));
            process=Process.Start(new ProcessStartInfo(browser){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--no-first-run","--no-default-browser-check","--disable-background-mode","--force-renderer-accessibility","--user-data-dir="+profile,"--app="+new Uri(page).AbsoluteUri}})??throw new InvalidOperationException("Browser could not start.");
            for(int i=0;i<160;i++) {target=Native.Windows().FirstOrDefault(t=>t.Title.StartsWith("TinyTask Browser Target"));if(target!=null)break;await Task.Delay(100);}
            if(target==null)throw new InvalidOperationException("No browser test window appeared in the accessible desktop.");
            // This whole diagnostic runs as a bounded external test process.
            AutomationElement? element=null;
            for(int i=0;i<25 && element==null;i++) {element=AutomationElement.FromHandle(target.Handle).FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,"Count browser click"));if(element==null)await Task.Delay(200);}
            if(element==null)throw new InvalidOperationException("Browser did not expose the test button through UI Automation.");
            var bounds=element.Current.BoundingRectangle;var point=new Native.Point((int)(bounds.Left+bounds.Width/2),(int)(bounds.Top+bounds.Height/2));Native.ScreenToClient(target.Handle,ref point);
            // The focus fixture must be visible: hidden windows cannot hold interactive focus.
            work=Process.Start(new ProcessStartInfo(AppIdentity.ExecutablePath){UseShellExecute=false,CreateNoWindow=true,ArgumentList={"--fixture","work"}});
            for(int i=0;i<50;i++) {var workWindow=Native.Windows().FirstOrDefault(t=>t.Pid==work!.Id);if(workWindow!=null){Native.SetForegroundWindow(workWindow.Handle);break;}await Task.Delay(100);}
            await Task.Delay(200);
            Native.GetCursorPos(out var before);var foreground=Native.GetForegroundWindow();string initial=Native.Title(target.Handle);
            await Routing.Click(target,point.X,point.Y,CancellationToken.None);await Task.Delay(350);string afterMessages=Native.Title(target.Handle);
            string uiaResult;
            try{await Routing.Invoke(target,point.X,point.Y,CancellationToken.None);await Task.Delay(350);uiaResult=Native.Title(target.Handle);}catch(Exception e){uiaResult=e.Message;}
            Native.GetCursorPos(out var after);
            File.WriteAllText(report,JsonSerializer.Serialize(new {browser,version=FileVersionInfo.GetVersionInfo(browser).FileVersion,initial,afterMessages,afterUia=uiaResult,targetWasForeground=foreground==target.Handle,foregroundUnchanged=foreground==Native.GetForegroundWindow(),cursorUnchanged=before.X==after.X&&before.Y==after.Y,scope="Local static browser page only; not Roblox or general websites."},new JsonSerializerOptions {WriteIndented=true}));return 0;
        }
        catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new {browser,status="Not verified",error=e.Message},new JsonSerializerOptions{WriteIndented=true}));return 1;}
        finally {if(work!=null){if(!work.HasExited){work.CloseMainWindow();using var timeout=new CancellationTokenSource(1500);try{await work.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){work.Kill();await work.WaitForExitAsync();}}work.Dispose();}if(target!=null&&Native.IsWindow(target.Handle))Native.Post(target.Handle,0x10);if(process!=null) {try {if(!process.HasExited) {using var timeout=new CancellationTokenSource(3000);try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);await process.WaitForExitAsync();}}}finally{process.Dispose();}}}
    }
}
