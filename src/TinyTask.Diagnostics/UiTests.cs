using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace TinyTask;
internal static class UiTests
{
    internal static async Task<int> Run(string report)
    {
        var results=new List<object>();bool passed=true;
        void Check(string name,bool success){results.Add(new{name,passed=success});passed&=success;}
        var home=new HomeWindow();
        try
        {
            home.Show();home.ConfigureSnapshot("light");await Task.Delay(100);
            string json="{\"actions\":[{\"type\":\"move\",\"x\":10,\"y\":20,\"futureField\":true}],\"metadata\":\"preserve me\"}";
            home.LoadDocument(json,"Round trip");string copy=Path.ChangeExtension(report,"macro.json");home.SaveCopy(copy);
            Check("Macro preview/save preserves all JSON",File.ReadAllText(copy)==json);
            bool rejected=false;try{home.LoadDocument("[]","Invalid");}catch(InvalidDataException){rejected=true;}
            home.SaveCopy(copy);Check("Invalid document cannot replace open macro",rejected && File.ReadAllText(copy)==json);
            home.ConfigureSnapshot("advanced-dark");Check("Advanced and dark theme render",home.IsVisible && home.Title=="TinyTask 2.0");
            var wizard=new SetupWizard(null){Owner=home};
            try {wizard.Show();Check("Only assigned mouse affects test cursor",wizard.TestDeviceFilter());}finally{wizard.Close();}
            var expected=new Target((nint)123456789,4294967294,"closed target","test");
            var diagnostics=new MainWindow(hosted:true,initialTarget:expected){Owner=home};
            try {diagnostics.Show();Check("Closed target does not select a different app",diagnostics.SelectedTarget==null);string lifecycle=Path.ChangeExtension(report,"diagnostics.json");await diagnostics.LifecycleTest(lifecycle);using var data=JsonDocument.Parse(File.ReadAllText(lifecycle));foreach(var result in data.RootElement.GetProperty("results").EnumerateArray())Check(result.GetProperty("name").GetString()!,result.GetProperty("passed").GetBoolean());}
            finally{diagnostics.Close();}
        }
        catch(Exception e){passed=false;results.Add(new{error=e.ToString()});}
        finally {home.Close();}
        File.WriteAllText(report,JsonSerializer.Serialize(new{passed,results,scope="UI and simulated device filtering; not physical isolation or virtual HID"},new JsonSerializerOptions{WriteIndented=true}));
        return passed?0:1;
    }
}
