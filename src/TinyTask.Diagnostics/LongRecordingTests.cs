using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;
namespace TinyTask;
internal static class LongRecordingTests
{
    internal static int Run(string report)
    {
        string directory=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!,"long-macro-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var clock=Stopwatch.StartNew();long baseline=GC.GetTotalMemory(true);var macro=new MacroDocument{Name="Seven minutes at 1000 Hz"};
            for(int i=0;i<420000;i++)macro.Actions.Add(new(){Type=i%1000==0?"mouseDown":i%1000==999?"mouseUp":"move",Button="left",X=100+i%100,Y=100+(i/100)%100,Delay=0.001});
            macro.Actions.Add(new(){Type="keyDown",Key=65,Scan=30});macro.Actions.Add(new(){Type="keyUp",Key=65,Scan=30});macro.Actions.Add(new(){Type="delay",Delay=0.5});macro.Validate();long captureManagedBytes=GC.GetTotalMemory(true)-baseline;
            string json=JsonSerializer.Serialize(macro,MacroDocument.Json),plain=Path.Combine(directory,"macro.json"),compressed=Path.Combine(directory,"macro.ttmacro");
            MacroFiles.Write(plain,json);MacroFiles.Write(compressed,json);
            if(MacroFiles.Read(compressed)!=json||MacroFiles.Read(plain)!=json)throw new Exception("Storage changed original JSON.");
            bool limited=false;try{MacroFiles.Read(compressed,1024);}catch(InvalidDataException){limited=true;}if(!limited)throw new Exception("Expansion budget ignored.");
            var decoded=MacroDocument.Parse(MacroFiles.Read(compressed));if(!macro.Actions.SequenceEqual(decoded.Actions))throw new Exception("An event or timestamp changed.");
            using var engine=new ClassicEngine();engine.Prepare(macro,1);if(engine.PreparedPacketCapacity!=512)throw new Exception("Native packet cache is not bounded.");engine.VerifyPreparedQueueForTest();
            File.WriteAllBytes(compressed,new byte[]{0x1f,0x8b,0,0,0,0,0,0,0,0});bool rejected=false;try{MacroDocument.Parse(MacroFiles.Read(compressed));}catch{rejected=true;}if(!rejected)throw new Exception("Corrupt macro accepted.");
            MacroFiles.Write(compressed,json);
            File.WriteAllText(report,JsonSerializer.Serialize(new{passed=true,actions=macro.Actions.Count,durationSeconds=macro.Actions.Sum(a=>a.Delay),plainBytes=new FileInfo(plain).Length,compressedBytes=new FileInfo(compressed).Length,nativePacketCapacity=engine.PreparedPacketCapacity,elapsedSeconds=clock.Elapsed.TotalSeconds,captureManagedBytes,peakProcessBytes=Process.GetCurrentProcess().PeakWorkingSet64,checks=new[]{"every event and timestamp preserved","legacy JSON","gzip JSON","420000 movement/button events","two full queue replays","corrupt file rejected","streaming expansion budget enforced"},scope="Synthetic data; no system input injected. Peak memory includes duplicate round-trip data and stress verification, not just capture."},new JsonSerializerOptions{WriteIndented=true}));return 0;
        }
        catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{passed=false,error=e.ToString()}));return 1;}
        finally {foreach(string file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);}
    }
}
