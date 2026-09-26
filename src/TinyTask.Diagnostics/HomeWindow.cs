using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Interop;
using Forms=System.Windows.Forms;

namespace TinyTask;

internal sealed class HomePreferences
{
    public bool Dark {get;set;}
    public bool MinimizeToTray {get;set;}=true;
    public bool AlwaysOnTop {get;set;}
    public bool SetupSeen {get;set;}
    public string? MacroMousePath {get;set;}
    public string? UserMousePath {get;set;}
    public bool MatchWindowsPointer {get;set;}=true;
    public double CursorSensitivity {get;set;}=1;
    public double Speed {get;set;}=1;
    public int Loops {get;set;}=1;
    public bool Continuous {get;set;}
    public int RecordKey {get;set;}=119;
    public int PlayKey {get;set;}=120;
    public uint RecordModifiers {get;set;}
    public uint PlayModifiers {get;set;}
    public int StopKey {get;set;}=121;
    public uint StopModifiers {get;set;}

}

// Presentation only: diagnostics retain their existing engine and controls.
internal sealed class HomeWindow : Window
{
    private readonly HomePreferences prefs;
    private readonly FrameworkElement view;
    private readonly Forms.NotifyIcon tray;
    private MainWindow? diagnostics;
    private SessionWindow? sessionWindow;
    private MacroDocument? sessionMacro;
    private PlaybackSettings? sessionSettings;
    private string? macroJson;
    private readonly string settingsPath=Path.Combine(MainWindow.DataDirectory,"home-settings.json");
    private Target? selectedTarget;
    private DateTime nextDeviceCheck;
    private readonly ClassicEngine engine=new();
    private HwndSource? source;
    private nint hwnd;
    private bool hotkeysReady,wasRecording;
    private readonly System.Windows.Threading.DispatcherTimer captureTimer=new(){Interval=TimeSpan.FromMilliseconds(200)};
    private string LibraryDirectory=>Path.Combine(MainWindow.DataDirectory,"Macros");
    private readonly System.Windows.Threading.DispatcherTimer focusTimer=new(){Interval=TimeSpan.FromSeconds(1)};
    internal HomeWindow()
    {
        try {prefs=File.Exists(settingsPath)?JsonSerializer.Deserialize<HomePreferences>(File.ReadAllText(settingsPath))??new():new();}
        catch {prefs=new();}
        Title="TinyTask 2.0";Width=740;Height=485;MinWidth=680;MinHeight=460;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new FontFamily("Segoe UI");FontSize=14;Topmost=prefs.AlwaysOnTop;
        Resources=CreateStyles();
        view=(FrameworkElement)XamlReader.Parse(Layout);Content=view;
        Get<ComboBox>("Targets").ItemTemplate=FriendlyTarget.Template();
        Theme(prefs.Dark);
        Get<ComboBox>("Speed").Text=prefs.Speed.ToString("0.################",System.Globalization.CultureInfo.InvariantCulture);
        var speedBox=Get<ComboBox>("Speed");
        speedBox.AddHandler(System.Windows.Input.TextCompositionManager.PreviewTextInputEvent,new System.Windows.Input.TextCompositionEventHandler((_,e)=>{if(e.OriginalSource is TextBox box)e.Handled=!PlaybackSettings.IsNumericText(box.Text.Remove(box.SelectionStart,box.SelectionLength).Insert(box.SelectionStart,e.Text));}));
        DataObject.AddPastingHandler(speedBox,(_,e)=>{if(!e.DataObject.GetDataPresent(DataFormats.UnicodeText)||e.OriginalSource is not TextBox box||e.DataObject.GetData(DataFormats.UnicodeText) is not string text||!PlaybackSettings.IsNumericText(box.Text.Remove(box.SelectionStart,box.SelectionLength).Insert(box.SelectionStart,text)))e.CancelCommand();});
        Get<TextBox>("Loops").Text=prefs.Loops.ToString();Get<CheckBox>("Continuous").IsChecked=prefs.Continuous;
        Get<ComboBox>("Mode").SelectionChanged+=(_,_)=>ModeChanged();
        Get<Button>("Open").Click+=(_,_)=>OpenMacro();Get<Button>("Save").Click+=(_,_)=>SaveMacro();
        Get<Button>("Record").Click+=(_,_)=>ToggleRecord();Get<Button>("Play").Click+=(_,_)=>PlayMacro();Get<Button>("Pause").Click+=(_,_)=>PauseMacro();
        Get<Button>("Stop").Click+=(_,_)=>StopAll();
        engine.Disarmed+=()=>{if(sessionWindow!=null)_=sessionWindow.SetNotReady();};
        captureTimer.Tick+=(_,_)=>Status($"Recording: {engine.Recording.Actions.Count(a=>a.Type=="move")} moves, {engine.Recording.Actions.Count(a=>a.Type=="mouseDown")} clicks, {engine.Recording.Actions.Count(a=>a.Type=="keyDown")} keys. Use another app; F8 finishes.");
        engine.Changed+=EngineChanged;engine.Failed+=message=>Dispatcher.BeginInvoke(()=>Status(message));
        engine.IsControlKey=key=>key==121||Bindings.Any(b=>b.Matches(key,Shortcut.CurrentModifiers()));
        engine.IsStopKey=key=>new Shortcut(prefs.StopKey,prefs.StopModifiers).Matches(key,Shortcut.CurrentModifiers());
        engine.ContainsControlSequence=actions=>Shortcut.Contains(actions,Bindings);
        Get<Button>("Setup").Click+=(_,_)=>Setup();Get<Button>("Compatibility").Click+=(_,_)=>Setup();
        Get<Button>("Settings").Click+=(_,_)=>Settings();
        Get<Button>("Sessions").Click+=(_,_)=>{try{sessionWindow=new SessionWindow(this,StopAll,PrepareSessionTask,PlaySessionTask);try{sessionWindow.ShowDialog();}finally{sessionWindow=null;engine.Stop();}}catch(Exception e){Status(e.Message);}};
        Get<Button>("InputSupport").Click+=(_,_)=>AdvancedSupport();
        Get<Button>("ChooseMouse").Click+=(_,_)=>Setup(true);
        Get<Button>("TestCursor").Click+=(_,_)=>Setup(true,true);
        Get<Button>("KeyboardSetup").Click+=(_,_)=>UiTheme.Message(this,"Physical keys follow the foreground app. Raw Input observes their source but cannot give Roblox separate focus. Supported background messages remain target-dependent. No keyboard filtering is enabled; use Diagnostics for a controlled probe.","Keyboard routing - Experimental");
        Get<ComboBox>("Targets").SelectionChanged+=(_,_)=>{selectedTarget=(Get<ComboBox>("Targets").SelectedItem as FriendlyTarget)?.Target;UpdateDetails();};
        Get<Button>("Refresh").Click+=(_,_)=>RefreshTargets();
        tray=new Forms.NotifyIcon {Text="TinyTask 2.0",Icon=System.Drawing.Icon.ExtractAssociatedIcon(AppIdentity.ExecutablePath),Visible=true};
        var menu=new Forms.ContextMenuStrip();menu.Items.Add("Open TinyTask 2.0",null,(_,_)=>Dispatcher.Invoke(Restore));
        menu.Items.Add("Stop",null,(_,_)=>Dispatcher.Invoke(StopAll));
        menu.Items.Add("Exit",null,(_,_)=>Dispatcher.Invoke(Close));tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(Restore);
        StateChanged+=(_,_)=>{if(WindowState==WindowState.Minimized && prefs.MinimizeToTray)Hide();UpdateTimer();};
        IsVisibleChanged+=(_,_)=>UpdateTimer();
        focusTimer.Tick+=(_,_)=>UpdateDetails();
        SourceInitialized+=(_,_)=>{hwnd=new WindowInteropHelper(this).Handle;source=HwndSource.FromHwnd(hwnd);source.AddHook(WndProc);RegisterControls();UiTheme.Caption(this,prefs.Dark);};
        Loaded+=(_,_)=>{string recovery=Path.Combine(MainWindow.DataDirectory,"last-recording.json");if(File.Exists(recovery))try{LoadDocument(File.ReadAllText(recovery),"Last recording");}catch(Exception e){Status("Recovery could not open: "+e.Message);}};
        Closing+=(_,_)=>StopAll();
        Closed+=(_,_)=>{focusTimer.Stop();captureTimer.Stop();engine.Dispose();diagnostics?.Close();SavePreferences();UnregisterControls();source?.RemoveHook(WndProc);tray.Dispose();};
    }
    private Shortcut[] Bindings=>new[]{new Shortcut(prefs.RecordKey,prefs.RecordModifiers),new Shortcut(prefs.PlayKey,prefs.PlayModifiers),new Shortcut(prefs.StopKey,prefs.StopModifiers)};
    private void StopAll(){engine.Stop();diagnostics?.EmergencyStop();if(sessionWindow!=null)_=sessionWindow.SetNotReady();}
    private string PrepareSessionTask()
    {
        if(macroJson==null)throw new InvalidOperationException("Record or open a macro first.");
        
        sessionSettings=CurrentPlaybackSettings();sessionMacro=MacroDocument.Parse(macroJson);engine.Prepare(sessionMacro,sessionSettings.Speed,true);return sessionMacro.Name;
    }
    private async System.Threading.Tasks.Task PlaySessionTask(double target)
    {
        if(!engine.Armed||sessionMacro==null||sessionSettings==null)throw new InvalidOperationException("Prepared task was cancelled. Ready up again.");
        await engine.Play(sessionMacro,sessionSettings.Speed,sessionSettings.Loops,sessionSettings.Continuous,synchronizedStart:target);
    }
    private nint WndProc(nint window,int message,nint w,nint l,ref bool handled)
    {
        if(message==0x312){if(w==31)ToggleRecord();else if(w==32){if(engine.State is "Playing" or "Paused")PauseMacro();else PlayMacro();}else if(w==33||w==34)StopAll();else return 0;handled=true;}return 0;
    }
    private void UnregisterControls(){foreach(int id in new[]{31,32,33,34})Native.UnregisterHotKey(hwnd,id);}
    private void RegisterControls()
    {
        UnregisterControls();
        bool record=Native.RegisterHotKey(hwnd,31,0x4000|prefs.RecordModifiers,(uint)prefs.RecordKey),play=Native.RegisterHotKey(hwnd,32,0x4000|prefs.PlayModifiers,(uint)prefs.PlayKey),stop=Native.RegisterHotKey(hwnd,33,0x4000|prefs.StopModifiers,(uint)prefs.StopKey);
        if(prefs.StopKey!=121||prefs.StopModifiers!=0)stop=Native.RegisterHotKey(hwnd,34,0x4000,121)&&stop;
        hotkeysReady=record&&play&&stop;
        if(!hotkeysReady)Status("A shortcut is used by another app. Toolbar buttons still work; F10 stops through the input hook. Change shortcuts in Preferences.");
        EngineChanged();
    }
    private void ToggleRecord()
    {
        try {if(engine.State=="Recording"){engine.Stop();return;}if(Get<ComboBox>("Mode").SelectedIndex!=0)throw new InvalidOperationException("Switch to Classic to record. Advanced tools are experimental.");if(engine.IsBusy)return;if(engine.Armed)StopAll();SavePreferences();engine.Record();}
        catch(Exception e){Status(e.Message);}
    }
    private async void PlayMacro()
    {
        try
        {
            if(engine.State=="Paused"){engine.PauseResume();return;}
            if(engine.IsBusy)return;
            if(engine.Armed)StopAll();
            if(Get<ComboBox>("Mode").SelectedIndex!=0)throw new InvalidOperationException("Switch to Classic to play. Background playback is not available.");
            using var preview=macroJson==null?null:JsonDocument.Parse(macroJson);
            if(preview==null || preview.RootElement.GetProperty("actions").GetArrayLength()==0){Status("No recording available. Record or open a macro first.");return;}
            if(macroJson==null)throw new InvalidOperationException("Record or open a macro first.");
            if(!MacroDocument.Parse(macroJson).Actions.Any(a=>a.Type!="delay"))throw new InvalidOperationException("This recording contains only waiting time. Record actions in another application, then press F8 to finish.");
            var profile=CurrentPlaybackSettings();double rate=profile.Speed;int count=profile.Loops;
            SavePreferences();await engine.Play(MacroDocument.Parse(macroJson),rate,count,prefs.Continuous);
        }catch(Exception e){engine.Stop();Status(e.Message);}
    }
    private void PauseMacro(){try{engine.PauseResume();}catch(Exception e){engine.Stop();Status(e.Message);}}
    private void EngineChanged()
    {
        bool finished=wasRecording&&engine.State!="Recording";
        wasRecording=engine.State=="Recording";
        bool empty=finished&&!engine.Recording.Actions.Any(a=>a.Type!="delay");
        if(finished&&!empty)
        {
            string json=new PlaybackSettings(prefs.Speed,prefs.Loops,prefs.Continuous).Write(JsonSerializer.Serialize(engine.Recording,MacroDocument.Json));LoadDocument(json,engine.Recording.Name);
            try{Directory.CreateDirectory(LibraryDirectory);SaveCopy(Path.Combine(MainWindow.DataDirectory,"last-recording.json"));SaveCopy(Path.Combine(LibraryDirectory,engine.Recording.Name+"-"+DateTime.Now.ToString("fff")+".json"));}catch(Exception e){Dispatcher.BeginInvoke(()=>Status("Recording kept in memory; auto-save failed: "+e.Message));}
        }
        if(wasRecording)captureTimer.Start();else captureTimer.Stop();
        Status(empty?"No input captured. Click Record, use another application, then press F8 to finish. Your previous macro was kept.":engine.State);Get<Button>("Record").Content=wasRecording?"Finish":"●  Record";
        bool classic=Get<ComboBox>("Mode").SelectedIndex==0;
        Get<ComboBox>("Mode").IsEnabled=!engine.IsBusy;
        Get<Button>("Record").IsEnabled=classic && engine.State is "Ready" or "Recording";
        Get<Button>("Play").IsEnabled=classic && engine.State is "Ready" or "Paused";
        Get<Button>("Pause").IsEnabled=engine.State is "Playing" or "Paused";
        Get<Button>("Pause").Content=engine.State=="Paused"?"Resume":"Ⅱ  Pause";
        foreach(string name in new[]{"Open","Save","Settings","Sessions","Setup","Compatibility","InputSupport","ChooseMouse","TestCursor","KeyboardSetup"})Get<Button>(name).IsEnabled=!engine.IsBusy && (name!="Save" || macroJson!=null);
    }
    private T Get<T>(string name) where T:FrameworkElement=>(T)view.FindName(name);
    private void Status(string value)=>Get<TextBlock>("Status").Text=value;
    private void Restore(){Show();WindowState=WindowState.Normal;Activate();}
    private void ModeChanged()
    {
        bool advanced=Get<ComboBox>("Mode").SelectedIndex==1;
        Get<FrameworkElement>("AdvancedPanel").Visibility=advanced?Visibility.Visible:Visibility.Collapsed;
        Height=advanced?755:485;UpdateTimer();
        EngineChanged();
        if(advanced){RefreshTargets();if(!prefs.SetupSeen && IsLoaded)Setup();}
    }
    private void UpdateTimer(){if(IsVisible && WindowState!=WindowState.Minimized && Get<ComboBox>("Mode").SelectedIndex==1)focusTimer.Start();else focusTimer.Stop();}
    private void RefreshTargets()
    {
        try {var previous=selectedTarget;var list=FriendlyTarget.List();Get<ComboBox>("Targets").ItemsSource=list;Get<ComboBox>("Targets").SelectedItem=previous==null?list.FirstOrDefault():list.FirstOrDefault(t=>t.Target.Handle==previous.Handle && t.Target.Pid==previous.Pid);}
        catch(Exception e){Status(e.Message);}
    }
    private void UpdateDetails()
    {
        nint foreground=Native.GetForegroundWindow();Native.GetWindowThreadProcessId(foreground,out uint pid);
        string name="Desktop";try {using var process=System.Diagnostics.Process.GetProcessById((int)pid);name=FriendlyTarget.Name(process.ProcessName);}catch(ArgumentException){}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
        Get<TextBlock>("FocusedApp").Text="Focused app: "+name;
        if(Get<ComboBox>("Mode").SelectedIndex==1 && DateTime.UtcNow>=nextDeviceCheck)
        {
            nextDeviceCheck=DateTime.UtcNow.AddSeconds(5);
            try {var mice=RawInput.Devices().Where(d=>d.Type==0).ToList();int index=mice.FindIndex(d=>d.Path==prefs.MacroMousePath);Get<TextBlock>("MouseStatus").Text=string.IsNullOrEmpty(prefs.MacroMousePath)?"Not configured":index<0?"Saved mouse disconnected - choose or reconnect":AdvancedInputSetup.MouseName(mice[index],index)+" (connected; observation only)";}
            catch{Get<TextBlock>("MouseStatus").Text="Device check failed - open setup to retry";}
        }
        Get<TextBlock>("Technical").Text=selectedTarget==null?"No target selected.":$"Window: {selectedTarget.Title}\nHWND: 0x{selectedTarget.Handle:X} · PID: {selectedTarget.Pid}\nInput isolation: experimental, not implemented · Managed driver package: none approved";
    }
    private void AdvancedSupport(){if(!engine.IsBusy)AdvancedInputSetup.Create(this).ShowDialog();}
    private void Setup(bool directMouse=false,bool testCursor=false)
    {
        if(engine.IsBusy)return;
        var wizard=new SetupWizard(selectedTarget,prefs.MacroMousePath,prefs.UserMousePath,directMouse,testCursor,prefs.MatchWindowsPointer,prefs.CursorSensitivity){Owner=this};wizard.ShowDialog();
        if(wizard.Completed){prefs.SetupSeen=true;prefs.MacroMousePath=wizard.MacroMousePath;prefs.UserMousePath=wizard.UserMousePath;prefs.MatchWindowsPointer=wizard.MatchWindowsPointer;prefs.CursorSensitivity=wizard.CursorSensitivity;nextDeviceCheck=DateTime.MinValue;selectedTarget=wizard.Target;SavePreferences();RefreshTargets();}
        if(wizard.RequestDiagnostics)OpenDiagnostics();
    }
    internal void ConfigureSnapshot(string mode){prefs.SetupSeen=true;prefs.Dark=mode.Contains("dark");Theme(prefs.Dark);if(mode.Contains("advanced"))Get<ComboBox>("Mode").SelectedIndex=1;}
    internal void OpenDiagnostics()
    {
        if(engine.IsBusy)return;
        if(diagnostics!=null){diagnostics.Activate();return;}
        diagnostics=new MainWindow(hosted:true,initialTarget:selectedTarget){Owner=this};
        try {diagnostics.ShowDialog();}finally{diagnostics=null;}
    }
    private void OpenMacro()
    {
        Directory.CreateDirectory(LibraryDirectory);var dialog=new Microsoft.Win32.OpenFileDialog{Filter="Macro JSON (*.json)|*.json",InitialDirectory=LibraryDirectory};if(dialog.ShowDialog(this)!=true)return;
        try
        {
            if(new FileInfo(dialog.FileName).Length>MacroDocument.MaxFileBytes)throw new InvalidDataException("Macro files must be smaller than 64 MB.");
            LoadDocument(File.ReadAllText(dialog.FileName),Path.GetFileNameWithoutExtension(dialog.FileName));
        }catch(Exception e){UiTheme.Message(this,e.Message,"Could not open macro");}
    }
    internal void LoadDocument(string json,string name)
    {
        if(System.Text.Encoding.UTF8.GetByteCount(json)>MacroDocument.MaxFileBytes)throw new InvalidDataException("Macro files must be smaller than 64 MB.");
        using var parsed=JsonDocument.Parse(json);
        if(parsed.RootElement.ValueKind!=JsonValueKind.Object || !parsed.RootElement.TryGetProperty("actions",out var actions) || actions.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("This file needs an actions list.");
        var profile=PlaybackSettings.Read(json);
        if(profile!=null){Get<ComboBox>("Speed").Text=profile.Speed.ToString("0.################",System.Globalization.CultureInfo.InvariantCulture);Get<TextBox>("Loops").Text=profile.Loops.ToString();Get<CheckBox>("Continuous").IsChecked=profile.Continuous;}
        macroJson=json;Get<TextBlock>("MacroName").Text=parsed.RootElement.TryGetProperty("name",out var storedName)&&storedName.ValueKind==JsonValueKind.String?storedName.GetString()??name:name;Get<TextBlock>("MacroDetail").Text=$"{actions.GetArrayLength()} actions";Get<Button>("Save").IsEnabled=true;Get<Button>("Play").IsEnabled=Get<ComboBox>("Mode").SelectedIndex==0 && engine.State is "Ready" or "Paused";Status("Ready");
    }
    internal void SaveCopy(string path)
    {
        if(macroJson==null)throw new InvalidOperationException("Open a macro first.");
        if(System.Text.Encoding.UTF8.GetByteCount(macroJson)>MacroDocument.MaxFileBytes)throw new InvalidDataException("Macro files must be smaller than 64 MB.");
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {File.WriteAllText(temporary,macroJson);File.Move(temporary,path,true);}
        finally {if(File.Exists(temporary))File.Delete(temporary);}
    }
    private void SaveMacro()
    {
        if(macroJson==null)return;
        Directory.CreateDirectory(LibraryDirectory);var dialog=new Microsoft.Win32.SaveFileDialog{Filter="Macro JSON (*.json)|*.json",InitialDirectory=LibraryDirectory,FileName=Get<TextBlock>("MacroName").Text+".json"};if(dialog.ShowDialog(this)!=true)return;
        try {macroJson=CurrentPlaybackSettings().Write(macroJson,Path.GetFileNameWithoutExtension(dialog.FileName));SaveCopy(dialog.FileName);Get<TextBlock>("MacroName").Text=Path.GetFileNameWithoutExtension(dialog.FileName);Status("Ready");}catch(Exception e){UiTheme.Message(this,e.Message,"Could not save macro");}
    }
    private void Settings()=>CreateSettingsWindow().ShowDialog();
    internal Window CreateSettingsWindow()
    {
        var window=new Window{Owner=this,Title="Preferences",Width=430,Height=535,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Background,Foreground=Foreground,Resources=Resources};
        var panel=new StackPanel{Margin=new Thickness(24)};window.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        panel.Children.Add(new TextBlock{Text="Make it yours",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,16)});
        void Toggle(string label,bool initial,Action<bool> changed){var c=new CheckBox{Content=label,IsChecked=initial,Margin=new Thickness(0,6,0,6)};c.SetResourceReference(ForegroundProperty,"Ink");c.Click+=(_,_)=>changed(c.IsChecked==true);panel.Children.Add(c);}
        Toggle("Dark mode",prefs.Dark,v=>{prefs.Dark=v;Theme(v);window.Background=Background;window.Foreground=Foreground;UiTheme.Caption(window,v);SavePreferences();});
        Toggle("Always on top",prefs.AlwaysOnTop,v=>{prefs.AlwaysOnTop=v;Topmost=v;SavePreferences();});
        Toggle("Minimize to tray",prefs.MinimizeToTray,v=>{prefs.MinimizeToTray=v;SavePreferences();});
        void Hotkey(string label,int index,Shortcut defaultValue)
        {
            var row=new DockPanel{Margin=new Thickness(0,8,0,0)};
            var capture=new Button{Content=Bindings[index].ToString(),MinWidth=120};
            var reset=new Button{Content="Reset",ToolTip="Reset to Default"};
            void Apply(Shortcut binding)
            {
                if((binding.Key==121&&(index!=2||binding.Modifiers!=0))||Bindings.Where((_,i)=>i!=index).Contains(binding)){UiTheme.Message(window,"Choose a different shortcut. This combination is already assigned.","Shortcut conflict");return;}
                var previous=Bindings[index];
                void Assign(Shortcut v){if(index==0){prefs.RecordKey=v.Key;prefs.RecordModifiers=v.Modifiers;}else if(index==1){prefs.PlayKey=v.Key;prefs.PlayModifiers=v.Modifiers;}else{prefs.StopKey=v.Key;prefs.StopModifiers=v.Modifiers;}}
                Assign(binding);RegisterControls();
                if(!hotkeysReady){Assign(previous);RegisterControls();UiTheme.Message(window,"This shortcut is unavailable. Your previous shortcut was kept.","Shortcut conflict");return;}
                capture.Content=binding.ToString();SavePreferences();
            }
            capture.Click+=(_,_)=>{UnregisterControls();Shortcut? chosen;try{chosen=Shortcut.Capture(window);}finally{RegisterControls();}if(chosen!=null)Apply(chosen);};
            reset.Click+=(_,_)=>Apply(defaultValue);
            DockPanel.SetDock(reset,Dock.Right);row.Children.Add(reset);DockPanel.SetDock(capture,Dock.Right);row.Children.Add(capture);row.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center});panel.Children.Add(row);
        }
        Hotkey("Record / finish",0,new Shortcut(119));Hotkey("Play / pause",1,new Shortcut(120));Hotkey("Stop",2,new Shortcut(121));
        panel.Children.Add(new TextBlock{Text="F10 always stops playback or recording.",Margin=new Thickness(0,12,0,0)});
        var library=new Button{Content="Open saved macros folder",Margin=new Thickness(0,10,0,0)};library.Click+=(_,_)=>{Directory.CreateDirectory(LibraryDirectory);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LibraryDirectory){UseShellExecute=true});};panel.Children.Add(library);
        var diagnosticsButton=new Button{Content="Diagnostics",Margin=new Thickness(0,12,0,0)};
        var advancedPanel=new StackPanel();
        var support=new Button{Content="Install Advanced Input Support",Margin=new Thickness(0,8,0,0)};support.Click+=(_,_)=>{window.Close();AdvancedSupport();};advancedPanel.Children.Add(support);advancedPanel.Children.Add(diagnosticsButton);
        var advanced=new Expander{Header="Advanced",Margin=new Thickness(0,18,0,0),Content=advancedPanel};panel.Children.Add(advanced);
        diagnosticsButton.Click+=(_,_)=>{window.Close();OpenDiagnostics();};UiTheme.Inherit(window,this);return window;
    }
    private PlaybackSettings CurrentPlaybackSettings()
    {
        double speed=PlaybackSettings.ParseSpeed(Get<ComboBox>("Speed").Text);
        if(!int.TryParse(Get<TextBox>("Loops").Text,out int loops)||loops<1||loops>1000000)throw new ArgumentException("Loops must be a whole number between 1 and 1,000,000.");
        return new(speed,loops,Get<CheckBox>("Continuous").IsChecked==true);
    }
    private void SavePreferences()
    {
        string speed=Get<ComboBox>("Speed").Text;
        if(PlaybackSettings.IsNumericText(speed)&&double.TryParse(speed,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double value)&&double.IsFinite(value)&&value>=0.01&&value<=1000)prefs.Speed=value;
        if(int.TryParse(Get<TextBox>("Loops").Text,out int loops)&&loops>0&&loops<=1000000)prefs.Loops=loops;
        prefs.Continuous=Get<CheckBox>("Continuous").IsChecked==true;
        try{Directory.CreateDirectory(MainWindow.DataDirectory);File.WriteAllText(settingsPath+".tmp",JsonSerializer.Serialize(prefs));File.Move(settingsPath+".tmp",settingsPath,true);}catch(Exception e){Status("Settings not saved: "+e.Message);}
    }
    internal void Theme(bool dark)=>UiTheme.Apply(this,dark);
    internal static ResourceDictionary CreateStyles()=>UiTheme.Styles();
    internal async System.Threading.Tasks.Task<bool> TestRecordFinishPlay(Target target)
    {
        Get<ComboBox>("Mode").SelectedIndex=0;Get<CheckBox>("Continuous").IsChecked=false;
        engine.AcceptInjectedForTest=true;
        Native.SetForegroundWindow(target.Handle);await System.Threading.Tasks.Task.Delay(100);
        if(Native.GetForegroundWindow()!=target.Handle)throw new InvalidOperationException("Recording fixture must be focused.");
        var point=new Native.Point(80,80);Native.ClientToScreen(target.Handle,ref point);
        Get<Button>("Record").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        foreach(var type in new[]{"move","mouseDown","mouseUp"}){await System.Threading.Tasks.Task.Run(()=>ClassicEngine.Send(new(){Type=type,X=point.X,Y=point.Y}));await System.Threading.Tasks.Task.Delay(35);}
        Get<Button>("Record").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        bool saved=macroJson!=null&&MacroDocument.Parse(macroJson).Actions.Any(a=>a.Type=="mouseDown");
        string before=Native.Title(target.Handle);
        Get<Button>("Play").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for(int i=0;i<100&&engine.IsBusy;i++)await System.Threading.Tasks.Task.Delay(20);
        bool replayed=before!=Native.Title(target.Handle)&&engine.State=="Ready";
        string? previous=macroJson;
        Get<Button>("Record").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Get<Button>("Record").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        bool preserved=previous==macroJson&&Get<TextBlock>("Status").Text.StartsWith("No input captured");
        engine.AcceptInjectedForTest=false;return saved&&replayed&&preserved;
    }
    internal bool TestEmptyPlay()
    {
        macroJson=null;EngineChanged();Get<Button>("Play").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        return Get<Button>("Play").IsEnabled && Get<TextBlock>("Status").Text=="No recording available. Record or open a macro first.";
    }
    private const string Layout="""
    <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Margin="24">
      <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
      <DockPanel Margin="0,0,0,20"><Button x:Name="Settings" DockPanel.Dock="Right" Content="⚙  Preferences"/><ComboBox x:Name="Mode" DockPanel.Dock="Right" Width="126" Margin="8,3,10,3" SelectedIndex="0"><ComboBoxItem>Classic</ComboBoxItem><ComboBoxItem>Advanced</ComboBoxItem></ComboBox><StackPanel><TextBlock Text="TinyTask 2.0" FontSize="27" FontWeight="SemiBold"/><TextBlock Text="Little tasks. Less effort." Foreground="{DynamicResource Muted}" Margin="0,3,0,0"/></StackPanel></DockPanel>
      <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto"><StackPanel>
      <Border CornerRadius="16" Background="{DynamicResource Surface}" Padding="16"><StackPanel>
        <UniformGrid Columns="6"><Button x:Name="Open" Content="Open"/><Button x:Name="Save" Content="Save" IsEnabled="False"/>
          <Button x:Name="Record" Content="●  Record" Background="#B72D43" Foreground="White"/>
          <Button x:Name="Play" Content="▶  Play" Background="#335FCE" Foreground="White" IsEnabled="False"/>
          <Button x:Name="Pause" Content="Ⅱ  Pause" IsEnabled="False"/><Button x:Name="Stop" Content="■  Stop"/></UniformGrid>
        <TextBlock x:Name="MacroName" Text="No macro open" FontSize="20" FontWeight="SemiBold" Margin="6,22,0,4"/><TextBlock x:Name="MacroDetail" Text="Record a task or open a saved macro." Foreground="{DynamicResource Muted}" Margin="6,0,0,18"/>
        <WrapPanel Margin="6,0,0,0"><StackPanel Margin="0,0,22,0"><TextBlock Text="Playback speed" Margin="0,0,0,6"/><ComboBox x:Name="Speed" Width="125" IsEditable="True"><ComboBoxItem>0.5</ComboBoxItem><ComboBoxItem>1</ComboBoxItem><ComboBoxItem>1.5</ComboBoxItem><ComboBoxItem>2</ComboBoxItem><ComboBoxItem>10</ComboBoxItem><ComboBoxItem>100</ComboBoxItem></ComboBox></StackPanel><StackPanel><TextBlock Text="Loops" Margin="0,0,0,6"/><TextBox x:Name="Loops" Width="80" Text="1"/></StackPanel><CheckBox x:Name="Continuous" Content="Continuous" VerticalAlignment="Bottom" Margin="16,0,0,8"/></WrapPanel>
      </StackPanel></Border>
      <Button x:Name="Sessions" Content="Sessions" HorizontalAlignment="Left" Margin="6,8,0,0"/><TextBlock Text="Classic playback controls your mouse and keyboard. Starts immediately; recorded delays are preserved. F10 stops." TextWrapping="Wrap" Foreground="{DynamicResource Muted}" Margin="6,14,6,12"/>
      <StackPanel x:Name="AdvancedPanel" Visibility="Collapsed">
        <Border Background="{DynamicResource Surface}" CornerRadius="16" Padding="18"><StackPanel>
          <DockPanel><Button x:Name="Setup" DockPanel.Dock="Right" Content="Guided setup"/><TextBlock Text="Your workspace" FontSize="20" FontWeight="SemiBold" VerticalAlignment="Center"/></DockPanel>
          <TextBlock Text="Target app" Margin="0,14,0,6"/><DockPanel><Button x:Name="Refresh" Content="Refresh" DockPanel.Dock="Right"/><ComboBox x:Name="Targets" MinWidth="240"/></DockPanel>
          <TextBlock x:Name="FocusedApp" Text="Focused app: Desktop" Foreground="{DynamicResource Muted}" Margin="0,8,0,12"/>
          <DockPanel Margin="0,4"><Button x:Name="InputSupport" DockPanel.Dock="Right" Content="Set up"/><StackPanel><TextBlock Text="Input isolation / Advanced Input Support"/><TextBlock Text="Experimental - component setup pending" Foreground="{DynamicResource Muted}"/></StackPanel></DockPanel>
          <DockPanel Margin="0,4"><Button x:Name="ChooseMouse" DockPanel.Dock="Right" Content="Choose mouse"/><StackPanel><TextBlock Text="Second mouse"/><TextBlock x:Name="MouseStatus" Text="Not configured" Foreground="{DynamicResource Muted}" TextWrapping="Wrap"/></StackPanel></DockPanel>
          <DockPanel Margin="0,4"><Button x:Name="TestCursor" DockPanel.Dock="Right" Content="Set up / Test"/><StackPanel><TextBlock Text="Virtual cursor"/><TextBlock Text="Ready to test - physical cursor remains shared" Foreground="{DynamicResource Muted}"/></StackPanel></DockPanel>
          <DockPanel Margin="0,4"><Button x:Name="KeyboardSetup" DockPanel.Dock="Right" Content="Configure"/><StackPanel><TextBlock Text="User keyboard routing"/><TextBlock Text="Experimental - foreground routing only" Foreground="{DynamicResource Muted}"/></StackPanel></DockPanel>
          <Button x:Name="Compatibility" Content="Test compatibility" HorizontalAlignment="Left"/>
          <Expander Header="Show technical details" Margin="0,12,0,0"><TextBlock x:Name="Technical" TextWrapping="Wrap" Margin="0,8,0,0" FontSize="12"/></Expander>
        </StackPanel></Border>
      </StackPanel>
      </StackPanel></ScrollViewer>
      <DockPanel Grid.Row="2" Margin="4,16,4,0"><TextBlock DockPanel.Dock="Right" Text="Close to exit · Minimize to keep in tray" Foreground="{DynamicResource Muted}" FontSize="12"/><TextBlock Text="●" Foreground="#329273" Margin="0,0,8,0"/><TextBlock x:Name="Status" Text="Ready"/></DockPanel>
    </Grid>
    """;
}

internal sealed record FriendlyTarget(Target Target)
{
    public ImageSource? Icon
    {
        get {try {using var p=System.Diagnostics.Process.GetProcessById((int)Target.Pid);using var icon=System.Drawing.Icon.ExtractAssociatedIcon(p.MainModule!.FileName!);if(icon==null)return null;var source=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromWidthAndHeight(20,20));source.Freeze();return source;}catch{return null;}}
    }
    public string Label=>ToString();
    internal static DataTemplate Template()=>(DataTemplate)XamlReader.Parse("""
    <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><StackPanel Orientation="Horizontal"><Image Source="{Binding Icon}" Width="20" Height="20" Margin="0,0,8,0"/><TextBlock Text="{Binding Label}" TextTrimming="CharacterEllipsis" MaxWidth="365" VerticalAlignment="Center"/></StackPanel></DataTemplate>
    """);
    internal static string Name(string process)=>process.ToLowerInvariant() switch {"robloxplayerbeta"=>"Roblox","chrome"=>"Chrome","opera"=>"Opera","msedge"=>"Microsoft Edge","winword"=>"Word","notepad"=>"Notepad",_=>process};
    public override string ToString()=>Name(Target.Process)+" — "+Target.Title;
    internal static System.Collections.Generic.List<FriendlyTarget> List()=>Native.Windows().Where(t=>t.Pid!=Environment.ProcessId).Select(t=>new FriendlyTarget(t)).ToList();
}
