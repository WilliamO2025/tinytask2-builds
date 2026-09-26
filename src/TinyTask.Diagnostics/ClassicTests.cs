using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TinyTask;
internal static class ClassicTests
{
    internal static async Task<int> Run(string report)
    {
        var results=new List<object>();bool passed=true;
        void Check(string name,bool ok,string evidence=""){results.Add(new{name,passed=ok,evidence});passed&=ok;}
        using var engine=new ClassicEngine{AcceptInjectedForTest=true};
        using var fixture=Process.Start(new ProcessStartInfo(AppIdentity.ExecutablePath){UseShellExecute=false,ArgumentList={"--fixture","work"}})!;
        try
        {
            Target? target=null;for(int i=0;i<100 && target==null;i++){target=Native.Windows().FirstOrDefault(w=>w.Pid==fixture.Id);await Task.Delay(50);}
            if(target==null)throw new Exception("Test window did not open.");
            Native.SetForegroundWindow(target.Handle);await Task.Delay(250);
            if(Native.GetForegroundWindow()!=target.Handle)
            {
                Native.SetWindowPos(target.Handle,(nint)(-1),0,0,0,0,0x53);
                Native.GetWindowRect(target.Handle,out var rect);var point=new Native.Point(rect.Left+100,rect.Top+12);
                if(Native.GetAncestor(Native.WindowFromPoint(point),2)!=target.Handle)throw new Exception("Fixture title bar is occluded; no setup click sent.");
                ClassicEngine.Send(new(){Type="mouseDown",X=point.X,Y=point.Y});ClassicEngine.Send(new(){Type="mouseUp",X=point.X,Y=point.Y});await Task.Delay(200);
            }
            if(Native.GetForegroundWindow()!=target.Handle)throw new Exception("Safe fixture is not focused; macro test not started.");
            var button=new Native.Point(80,80);Native.ClientToScreen(target.Handle,ref button);
            var edit=new Native.Point(80,135);Native.ClientToScreen(target.Handle,ref edit);
            string expectedText=System.Windows.Forms.Control.IsKeyLocked(System.Windows.Forms.Keys.CapsLock)?"AAA":"aaa";
            engine.Record();
            var actions=new[]{
                new MacroAction{Type="move",X=button.X,Y=button.Y},
                new MacroAction{Type="mouseDown",X=button.X,Y=button.Y},new MacroAction{Type="mouseUp",X=button.X,Y=button.Y},
                new MacroAction{Type="keyDown",Key=0x75,Scan=0x40},new MacroAction{Type="keyUp",Key=0x75,Scan=0x40},
                new MacroAction{Type="scroll",X=button.X,Y=button.Y,Delta=120},
                new MacroAction{Type="mouseDown",X=edit.X,Y=edit.Y},new MacroAction{Type="mouseUp",X=edit.X,Y=edit.Y},
                new MacroAction{Type="keyDown",Key=0x41,Scan=0x1E},new MacroAction{Type="keyUp",Key=0x41,Scan=0x1E}};
            foreach(var action in actions){if(Native.GetForegroundWindow()!=target.Handle)throw new Exception("Fixture lost focus; test stopped.");await Task.Run(()=>ClassicEngine.Send(action));await Task.Delay(40);}
            await Task.Delay(600);engine.Stop();var macro=MacroDocument.Parse(JsonSerializer.Serialize(engine.Recording,MacroDocument.Json));
            File.WriteAllText(Path.ChangeExtension(report,"packets.json"),JsonSerializer.Serialize(engine.TestMousePackets,MacroDocument.Json));
            File.WriteAllText(Path.ChangeExtension(report,"recorded.json"),JsonSerializer.Serialize(macro,MacroDocument.Json));
            Check("Low-level hook records movement/buttons/scroll/keyboard",new[]{"move","mouseDown","mouseUp","scroll","keyDown","keyUp"}.All(t=>macro.Actions.Any(a=>a.Type==t)),string.Join(",",macro.Actions.Select(a=>a.Type)));
            Check("Recorded timing retained",macro.Actions.Sum(a=>a.Delay)>0.2);
            await engine.Play(macro,1,2,false,0);await Task.Delay(150);
            string title=Native.Title(target.Handle);
            Check("Recorded macro replays twice with real system input",title.Contains("clicks=3;") && title.Contains("keys=3;") && title.Contains("wheels=3;") && title.Contains("text="+expectedText),title);
            var hold=new MacroDocument{Actions=new(){new(){Type="keyDown",Key=0xA0,Scan=0x2A},new(){Type="keyUp",Key=0xA0,Scan=0x2A,Delay=1}}};
            var playing=engine.Play(hold,1,1,false,0);await Task.Delay(50);engine.PauseResume();await Task.Delay(50);
            Check("Pause releases held key",engine.State=="Paused" && (Native.GetAsyncKeyState(0xA0)&0x8000)==0);
            await Task.Delay(100);Check("Pause freezes playback",engine.State=="Paused");engine.PauseResume();await Task.Delay(30);engine.Stop();await playing;await Task.Delay(50);
            Check("Stop releases held key",engine.State=="Ready" && (Native.GetAsyncKeyState(0xA0)&0x8000)==0);
            playing=engine.Play(hold,1,1,false,0);await Task.Delay(50);
            await Task.Run(()=>{ClassicEngine.Send(new(){Type="keyDown",Key=121,Scan=0x44});ClassicEngine.Send(new(){Type="keyUp",Key=121,Scan=0x44});});
            await playing.WaitAsync(TimeSpan.FromSeconds(2));await Task.Delay(50);
            Check("F10 stops while a modifier is held",engine.State=="Ready" && (Native.GetAsyncKeyState(0xA0)&0x8000)==0);
            playing=engine.Play(new(){Actions=new(){new(){Type="delay"}}},100,1,true,0);await Task.Delay(50);engine.Stop();await playing;
            Check("Continuous zero-delay loop is cancellable",engine.State=="Ready");
            int disarmed=0;engine.Disarmed+=()=>disarmed++;
            engine.Prepare(hold,1,true);Check("Preparation arms emergency stop without pressing keys",engine.Armed&&(Native.GetAsyncKeyState(0xA0)&0x8000)==0);
            await Task.Run(()=>{ClassicEngine.Send(new(){Type="keyDown",Key=121,Scan=0x44});ClassicEngine.Send(new(){Type="keyUp",Key=121,Scan=0x44});});await Task.Delay(50);
            Check("F10 cancels an armed task and notifies session readiness",!engine.Armed&&disarmed==1);
            var editable=new MacroDocument{Actions=new(){new(){Type="delay",Delay=0.01}}};engine.Prepare(editable,1);editable.Actions[0].Delay=0.08;var editedClock=Stopwatch.StartNew();await engine.Play(editable,1,1,false);
            Check("Edited macro invalidates prepared native timeline",editedClock.Elapsed.TotalSeconds>=0.075);
            var lateness=new MacroDocument{Actions=new(){new(){Type="delay",Delay=0.05}}};
            playing=engine.Play(lateness,1,1,false);Thread.Sleep(650);bool lateStopped=false;try{await playing;}catch(InvalidOperationException){lateStopped=true;}
            Check("Severe lateness stops instead of replaying backlog",lateStopped&&engine.State=="Ready"&&engine.PeakLatenessMilliseconds>=500);
            var clock=Stopwatch.StartNew();await engine.Play(new(){Actions=new(){new(){Type="delay",Delay=0.02}}},1,1,false,synchronizedStart:SessionConnection.Now+0.08);
            Check("Synchronized start anchors original delay",clock.Elapsed.TotalSeconds>=0.095);
            var home=new HomeWindow();try{home.Show();await Task.Delay(100);Check("Toolbar Finish Play replays clicks, preserves empty recording recovery, and captures five one-pixel moves over app",await home.TestRecordFinishPlay(target));}finally{home.Close();}
            bool rejected=false;try{MacroDocument.Parse("{\"actions\":[{\"type\":\"delay\",\"delay\":-1}]}");}catch(ArgumentException){rejected=true;}Check("Invalid delay rejected",rejected);
        }
        catch(Exception e){passed=false;results.Add(new{error=e.ToString()});}
        finally
        {
            engine.Stop();
            try {if(!fixture.HasExited){fixture.CloseMainWindow();using var timeout=new CancellationTokenSource(1500);try{await fixture.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){fixture.Kill();using var killed=new CancellationTokenSource(3000);await fixture.WaitForExitAsync(killed.Token);}}}
            catch(Exception e){passed=false;results.Add(new{cleanupError=e.Message});}
        }
        File.WriteAllText(report,JsonSerializer.Serialize(new{passed,results,scope="Windows Classic with a controlled native fixture. Synthetic input explicitly allowed for recording test only; physical-device recording and macOS require separate acceptance tests."},new JsonSerializerOptions{WriteIndented=true}));return passed?0:1;
    }
}
