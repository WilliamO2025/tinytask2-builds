using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace TinyTask;
internal static class UiTests
{
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++){var child=System.Windows.Media.VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    private static void Snapshot(Window window,string report,string name)
    {
        window.UpdateLayout();var content=(FrameworkElement)window.Content;
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(window);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(Path.GetDirectoryName(report)!,name+".png"));encoder.Save(stream);
    }
    internal static async Task<int> Run(string report)
    {
        var results=new List<object>();bool passed=true;
        void Check(string name,bool success){results.Add(new{name,passed=success});passed&=success;}
        var home=new HomeWindow();
        try
        {
            home.Show();home.ConfigureSnapshot("light");await Task.Delay(100);
            Check("Empty Play shows friendly guidance",home.TestEmptyPlay());
            Snapshot(home,report,"home-light");
            string json="{\"actions\":[{\"type\":\"move\",\"x\":10,\"y\":20,\"futureField\":true}],\"metadata\":\"preserve me\"}";
            home.LoadDocument(json,"Round trip");string copy=Path.ChangeExtension(report,"macro.json");home.SaveCopy(copy);
            Check("Macro preview/save preserves all JSON",File.ReadAllText(copy)==json);
            bool rejected=false;try{home.LoadDocument("[]","Invalid");}catch(InvalidDataException){rejected=true;}
            home.SaveCopy(copy);Check("Invalid document cannot replace open macro",rejected && File.ReadAllText(copy)==json);
            var savedProfile=new PlaybackSettings(1.5,20,true).Write(json,"Fishing");
            Check("Task settings round trip",PlaybackSettings.Read(savedProfile)==new PlaybackSettings(1.5,20,true));
            using(var profileDoc=JsonDocument.Parse(savedProfile))Check("Task profile preserves unrelated JSON",profileDoc.RootElement.GetProperty("metadata").GetString()=="preserve me"&&profileDoc.RootElement.GetProperty("actions")[0].GetProperty("futureField").GetBoolean());
            Check("Speed entry rejects letters symbols and exponent",!PlaybackSettings.IsNumericText("abc")&&!PlaybackSettings.IsNumericText("1e2")&&!PlaybackSettings.IsNumericText("-1")&&!PlaybackSettings.IsNumericText("1.2.3")&&PlaybackSettings.ParseSpeed("1.5")==1.5);
            bool speedRejected=false;try{PlaybackSettings.ParseSpeed("1001");}catch(ArgumentException){speedRejected=true;}Check("Out of range speed rejected",speedRejected);
            var chord=new Shortcut(0x50,6);
            Check("Combination distinguishes plain key from shortcut",!Shortcut.Contains(new[]{new MacroAction{Type="keyDown",Key=0x50}},new[]{chord})&&Shortcut.Contains(new[]{new MacroAction{Type="keyDown",Key=0x11},new MacroAction{Type="keyDown",Key=0x10},new MacroAction{Type="keyDown",Key=0x50}},new[]{chord}));
            home.ConfigureSnapshot("advanced-dark");Check("Advanced and dark theme render",home.IsVisible && home.Title=="TinyTask 2.0");
            Snapshot(home,report,"home-dark");
            var settings=home.CreateSettingsWindow();try{settings.Show();foreach(var expander in Descendants<System.Windows.Controls.Expander>(settings))expander.IsExpanded=true;Snapshot(settings,report,"settings-dark");Check("Preferences inherit dark palette",UiTheme.IsDark(settings));}finally{settings.Close();}
            var session=new SessionWindow(home,()=>{},()=>"Test task",_=>Task.CompletedTask);
            try{session.Show();Snapshot(session,report,"sessions-dark");Check("Sessions inherit dark palette without connecting",UiTheme.IsDark(session));}finally{session.Close();}
            var wizard=new SetupWizard(null){Owner=home};
            try {wizard.Show();Check("Repeated test clicks register without claiming isolation",await wizard.TestRepeatedClicks());Check("Only assigned mouse affects test cursor",wizard.TestDeviceFilter());Check("Cursor speed retains fractions and matching uses screen conversion",wizard.TestCursorScaling());wizard.ShowCursorSettingsForTest();Snapshot(wizard,report,"wizard-dark");}finally{wizard.Close();}
            var support=AdvancedInputSetup.Create(home);try{support.Show();Snapshot(support,report,"advanced-support-dark");Check("Unapproved component cannot be installed",System.Linq.Enumerable.Any(Descendants<System.Windows.Controls.Button>(support),b=>Equals(b.Content,"Install Advanced Input Support") && !b.IsEnabled));}finally{support.Close();}
            var disconnected=new SetupWizard(null,"missing-macro-device","missing-personal-device",true){Owner=home};try{disconnected.Show();Check("Disconnected assignments never select another mouse",disconnected.HasNoSelectedMice);}finally{disconnected.Close();}
            var expected=new Target((nint)123456789,4294967294,"closed target","test");
            var diagnostics=new MainWindow(hosted:true,initialTarget:expected){Owner=home};
            try {diagnostics.Show();Snapshot(diagnostics,report,"diagnostics-dark");Check("Diagnostics inherit dark palette",UiTheme.IsDark(diagnostics));Check("Closed target does not select a different app",diagnostics.SelectedTarget==null);string lifecycle=Path.ChangeExtension(report,"diagnostics.json");await diagnostics.LifecycleTest(lifecycle);using var data=JsonDocument.Parse(File.ReadAllText(lifecycle));foreach(var result in data.RootElement.GetProperty("results").EnumerateArray())Check(result.GetProperty("name").GetString()!,result.GetProperty("passed").GetBoolean());}
            finally{diagnostics.Close();}
        }
        catch(Exception e){passed=false;results.Add(new{error=e.ToString()});}
        finally {home.Close();}
        File.WriteAllText(report,JsonSerializer.Serialize(new{passed,results,scope="UI and simulated device filtering; not physical isolation or virtual HID"},new JsonSerializerOptions{WriteIndented=true}));
        return passed?0:1;
    }
}
