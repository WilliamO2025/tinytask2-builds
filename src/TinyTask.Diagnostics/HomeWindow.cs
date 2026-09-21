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
    public double Speed {get;set;}=1;
    public int Loops {get;set;}=1;
    public bool Continuous {get;set;}
    public int RecordKey {get;set;}=119;
    public int PlayKey {get;set;}=120;
}

// Presentation only: diagnostics retain their existing engine and controls.
internal sealed class HomeWindow : Window
{
    private readonly HomePreferences prefs;
    private readonly FrameworkElement view;
    private readonly Forms.NotifyIcon tray;
    private MainWindow? diagnostics;
    private string? macroJson;
    private readonly string settingsPath=Path.Combine(MainWindow.DataDirectory,"home-settings.json");
    private Target? selectedTarget;
    private readonly ClassicEngine engine=new();
    private HwndSource? source;
    private nint hwnd;
    private bool hotkeysReady,wasRecording;
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
        Get<ComboBox>("Speed").Text=prefs.Speed.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"x";
        Get<TextBox>("Loops").Text=prefs.Loops.ToString();Get<CheckBox>("Continuous").IsChecked=prefs.Continuous;
        Get<ComboBox>("Mode").SelectionChanged+=(_,_)=>ModeChanged();
        Get<Button>("Open").Click+=(_,_)=>OpenMacro();Get<Button>("Save").Click+=(_,_)=>SaveMacro();
        Get<Button>("Record").Click+=(_,_)=>ToggleRecord();Get<Button>("Play").Click+=(_,_)=>PlayMacro();Get<Button>("Pause").Click+=(_,_)=>PauseMacro();
        Get<Button>("Stop").Click+=(_,_)=>StopAll();
        engine.Changed+=EngineChanged;engine.Failed+=message=>Dispatcher.BeginInvoke(()=>Status(message));
        engine.IsControlKey=key=>key==prefs.RecordKey || key==prefs.PlayKey || key==121;
        Get<Button>("Setup").Click+=(_,_)=>Setup();Get<Button>("Compatibility").Click+=(_,_)=>Setup();
        Get<Button>("Settings").Click+=(_,_)=>Settings();
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
        Closed+=(_,_)=>{focusTimer.Stop();engine.Dispose();diagnostics?.Close();SavePreferences();UnregisterControls();source?.RemoveHook(WndProc);tray.Dispose();};
    }
    private void StopAll(){engine.Stop();diagnostics?.EmergencyStop();}
    private nint WndProc(nint window,int message,nint w,nint l,ref bool handled)
    {
        if(message==0x312){if(w==31)ToggleRecord();else if(w==32){if(engine.State is "Playing" or "Paused")PauseMacro();else PlayMacro();}else if(w==33)StopAll();else return 0;handled=true;}return 0;
    }
    private void UnregisterControls(){foreach(int id in new[]{31,32,33})Native.UnregisterHotKey(hwnd,id);}
    private void RegisterControls()
    {
        UnregisterControls();
        bool record=Native.RegisterHotKey(hwnd,31,0x4000,(uint)prefs.RecordKey),play=Native.RegisterHotKey(hwnd,32,0x4000,(uint)prefs.PlayKey),stop=Native.RegisterHotKey(hwnd,33,0x4000,121);
        hotkeysReady=record&&play&&stop;
        if(!hotkeysReady)Status("A hotkey is in use. Change recording/playback keys in Preferences; F10 must be available.");
        EngineChanged();
    }
    private void ToggleRecord()
    {
        try {if(engine.State=="Recording"){engine.Stop();return;}if(Get<ComboBox>("Mode").SelectedIndex!=0)throw new InvalidOperationException("Switch to Classic to record. Advanced tools are experimental.");if(!hotkeysReady)throw new InvalidOperationException("Free the selected hotkeys first; F10 is the emergency stop.");if(engine.IsBusy)return;SavePreferences();engine.Record();}
        catch(Exception e){Status(e.Message);}
    }
    private async void PlayMacro()
    {
        try
        {
            if(engine.State=="Paused"){engine.PauseResume();return;}
            if(engine.IsBusy)return;
            if(Get<ComboBox>("Mode").SelectedIndex!=0)throw new InvalidOperationException("Switch to Classic to play. Background playback is not available.");
            using var preview=macroJson==null?null:JsonDocument.Parse(macroJson);
            if(preview==null || preview.RootElement.GetProperty("actions").GetArrayLength()==0){Status("No recording available. Record or open a macro first.");return;}
            if(!hotkeysReady)throw new InvalidOperationException("Playback needs the configured hotkeys and F10 stop to be available.");
            if(macroJson==null)throw new InvalidOperationException("Record or open a macro first.");
            string speedText=Get<ComboBox>("Speed").Text.Trim().TrimEnd('x','X');
            if(!double.TryParse(speedText,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double rate) || !double.IsFinite(rate) || rate<0.01 || rate>1000 || !int.TryParse(Get<TextBox>("Loops").Text,out int count) || count<1 || count>1000000)throw new ArgumentException("Enter a speed from 0.01–1000x and a whole-number loop count from 1–1,000,000.");
            SavePreferences();await engine.Play(MacroDocument.Parse(macroJson),rate,count,prefs.Continuous);
        }catch(Exception e){engine.Stop();Status(e.Message);}
    }
    private void PauseMacro(){try{engine.PauseResume();}catch(Exception e){engine.Stop();Status(e.Message);}}
    private void EngineChanged()
    {
        if(wasRecording && engine.State!="Recording")
        {
            string json=JsonSerializer.Serialize(engine.Recording,MacroDocument.Json);LoadDocument(json,engine.Recording.Name);
            try{Directory.CreateDirectory(LibraryDirectory);SaveCopy(Path.Combine(MainWindow.DataDirectory,"last-recording.json"));SaveCopy(Path.Combine(LibraryDirectory,engine.Recording.Name+"-"+DateTime.Now.ToString("fff")+".json"));}catch(Exception e){Dispatcher.BeginInvoke(()=>Status("Recording kept in memory; auto-save failed: "+e.Message));}
        }
        wasRecording=engine.State=="Recording";
        Status(engine.State);Get<Button>("Record").Content=wasRecording?"Finish":"●  Record";
        bool classic=Get<ComboBox>("Mode").SelectedIndex==0;
        Get<ComboBox>("Mode").IsEnabled=!engine.IsBusy;
        Get<Button>("Record").IsEnabled=classic && hotkeysReady && engine.State is "Ready" or "Recording";
        Get<Button>("Play").IsEnabled=classic && engine.State is "Ready" or "Paused";
        Get<Button>("Pause").IsEnabled=engine.State is "Playing" or "Paused";
        Get<Button>("Pause").Content=engine.State=="Paused"?"Resume":"Ⅱ  Pause";
        foreach(string name in new[]{"Open","Save","Settings","Setup","Compatibility"})Get<Button>(name).IsEnabled=!engine.IsBusy && (name!="Save" || macroJson!=null);
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
        Get<TextBlock>("Technical").Text=selectedTarget==null?"No target selected.":$"Window: {selectedTarget.Title}\nHWND: 0x{selectedTarget.Handle:X} · PID: {selectedTarget.Pid}\nInput isolation: not implemented · Virtual HID: not installed by this app";
    }
    private void Setup()
    {
        if(engine.IsBusy)return;
        var wizard=new SetupWizard(selectedTarget){Owner=this};wizard.ShowDialog();
        if(wizard.Completed){prefs.SetupSeen=true;selectedTarget=wizard.Target;SavePreferences();RefreshTargets();}
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
        macroJson=json;Get<TextBlock>("MacroName").Text=name;Get<TextBlock>("MacroDetail").Text=$"{actions.GetArrayLength()} actions";Get<Button>("Save").IsEnabled=true;Get<Button>("Play").IsEnabled=Get<ComboBox>("Mode").SelectedIndex==0 && engine.State is "Ready" or "Paused";Status("Ready");
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
        try {SaveCopy(dialog.FileName);Status("Ready");}catch(Exception e){UiTheme.Message(this,e.Message,"Could not save macro");}
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
        void Hotkey(string label,int current,Action<int> change)
        {
            var row=new DockPanel{Margin=new Thickness(0,8,0,0)};var combo=new ComboBox{Width=90,ItemsSource=Enumerable.Range(1,12).Where(n=>n!=10).Select(n=>"F"+n).ToArray(),SelectedItem="F"+(current-111)};DockPanel.SetDock(combo,Dock.Right);row.Children.Add(combo);row.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center});panel.Children.Add(row);
            combo.SelectionChanged+=(_,_)=>{if(combo.SelectedItem is string value){int key=111+int.Parse(value[1..]);change(key);SavePreferences();RegisterControls();}};
        }
        Hotkey("Record / finish recording",prefs.RecordKey,key=>prefs.RecordKey=key);Hotkey("Play / pause",prefs.PlayKey,key=>prefs.PlayKey=key);
        panel.Children.Add(new TextBlock{Text="F10 always stops playback or recording.",Margin=new Thickness(0,12,0,0)});
        var library=new Button{Content="Open saved macros folder",Margin=new Thickness(0,10,0,0)};library.Click+=(_,_)=>{Directory.CreateDirectory(LibraryDirectory);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LibraryDirectory){UseShellExecute=true});};panel.Children.Add(library);
        var diagnosticsButton=new Button{Content="Diagnostics",Margin=new Thickness(0,12,0,0)};
        var advanced=new Expander{Header="Advanced",Margin=new Thickness(0,18,0,0),Content=diagnosticsButton};panel.Children.Add(advanced);
        diagnosticsButton.Click+=(_,_)=>{window.Close();OpenDiagnostics();};UiTheme.Inherit(window,this);return window;
    }
    private void SavePreferences()
    {
        string speed=Get<ComboBox>("Speed").Text.Trim().TrimEnd('x','X');
        if(double.TryParse(speed,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double value)&&double.IsFinite(value)&&value>=0.01&&value<=1000)prefs.Speed=value;
        if(int.TryParse(Get<TextBox>("Loops").Text,out int loops)&&loops>0&&loops<=1000000)prefs.Loops=loops;
        prefs.Continuous=Get<CheckBox>("Continuous").IsChecked==true;
        try{Directory.CreateDirectory(MainWindow.DataDirectory);File.WriteAllText(settingsPath+".tmp",JsonSerializer.Serialize(prefs));File.Move(settingsPath+".tmp",settingsPath,true);}catch(Exception e){Status("Settings not saved: "+e.Message);}
    }
    internal void Theme(bool dark)=>UiTheme.Apply(this,dark);
    internal static ResourceDictionary CreateStyles()=>UiTheme.Styles();
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
        <WrapPanel Margin="6,0,0,0"><StackPanel Margin="0,0,22,0"><TextBlock Text="Playback speed" Margin="0,0,0,6"/><ComboBox x:Name="Speed" Width="125" IsEditable="True"><ComboBoxItem>0.5x</ComboBoxItem><ComboBoxItem>1x</ComboBoxItem><ComboBoxItem>2x</ComboBoxItem><ComboBoxItem>10x</ComboBoxItem><ComboBoxItem>100x</ComboBoxItem></ComboBox></StackPanel><StackPanel><TextBlock Text="Loops" Margin="0,0,0,6"/><TextBox x:Name="Loops" Width="80" Text="1"/></StackPanel><CheckBox x:Name="Continuous" Content="Continuous" VerticalAlignment="Bottom" Margin="16,0,0,8"/></WrapPanel>
      </StackPanel></Border>
      <TextBlock Text="Classic playback controls your mouse and keyboard. Starts after 3 seconds. F10 stops." TextWrapping="Wrap" Foreground="{DynamicResource Muted}" Margin="6,14,6,12"/>
      <StackPanel x:Name="AdvancedPanel" Visibility="Collapsed">
        <Border Background="{DynamicResource Surface}" CornerRadius="16" Padding="18"><StackPanel>
          <DockPanel><Button x:Name="Setup" DockPanel.Dock="Right" Content="Guided setup"/><TextBlock Text="Your workspace" FontSize="20" FontWeight="SemiBold" VerticalAlignment="Center"/></DockPanel>
          <TextBlock Text="Target app" Margin="0,14,0,6"/><DockPanel><Button x:Name="Refresh" Content="Refresh" DockPanel.Dock="Right"/><ComboBox x:Name="Targets" MinWidth="240"/></DockPanel>
          <TextBlock x:Name="FocusedApp" Text="Focused app: Desktop" Foreground="{DynamicResource Muted}" Margin="0,8,0,12"/>
          <TextBlock Text="Input isolation: Unavailable"/><TextBlock Text="Second mouse: Not active" Margin="0,6,0,0"/><TextBlock Text="Virtual cursor: Available in guided setup" Margin="0,6,0,0"/><TextBlock Text="User keyboard routing: Unavailable" Margin="0,6,0,10"/>
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
