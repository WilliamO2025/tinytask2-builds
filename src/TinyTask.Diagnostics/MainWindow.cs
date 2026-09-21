using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace TinyTask;

internal sealed class Preferences
{
    public bool SetupSeen {get;set;}
    public bool Dark {get;set;}
    public bool MinimizeToTray {get;set;}=true;
    public bool AlwaysOnTop {get;set;}
}

internal sealed class MainWindow : Window
{
    internal static readonly string DataDirectory=Environment.GetEnvironmentVariable("TINYTASK_LAB_DATA")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TinyTask2",AppIdentity.DataFolder);
    private readonly ComboBox targets=new() {MinWidth=500,MaxWidth=840},devices=new() {MinWidth=500,MaxWidth=840};
    private readonly TextBox x=new(){Text="80",Width=70},y=new(){Text="80",Width=70},sampleText=new(){Text="TinyTask probe",Width=180};
    private readonly TextBlock status=new(){Text="Ready",TextWrapping=TextWrapping.Wrap},sources=new(){TextWrapping=TextWrapping.Wrap},cursorText=new();
    private readonly TextBox log=new(){IsReadOnly=true,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new FontFamily("Consolas"),FontSize=12};
    private readonly CheckBox visual=new(){Content="Show independent cursor"},follow=new(){Content="Track selected mouse (observation only)"};
    private readonly List<object> observations=new();
    private readonly List<Process> fixtures=new();
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(100)};
    private RawInput? raw;
    private HwndSource? source;
    private nint hwnd;
    private readonly CursorWindow overlay=new();
    private CancellationTokenSource? pending;
    private Forms.NotifyIcon? tray;
    private RawSample? lastSample;
    private long packets;
    private int virtualX=80,virtualY=80;
    private bool closed,hotkeyReady;
    private Preferences prefs=new();
    private readonly bool hosted;
    private Target? rememberedTarget;
    internal Target? SelectedTarget=>targets.SelectedItem as Target;

    public MainWindow(bool hosted=false,Target? initialTarget=null)
    {
        this.hosted=hosted;
        rememberedTarget=initialTarget;
        Title="TinyTask 2.0 • Input Lab 0.2"; Width=1040; Height=820; MinWidth=850; MinHeight=660;
        Background=new SolidColorBrush(Color.FromRgb(244,247,251)); FontFamily=new FontFamily("Segoe UI"); FontSize=14;
        try { Directory.CreateDirectory(DataDirectory); if(File.Exists(SettingsPath)) prefs=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(SettingsPath))??new(); } catch(Exception e) { status.Text="Settings could not be loaded: "+e.Message; }
        Topmost=prefs.AlwaysOnTop;UiTheme.Apply(this,prefs.Dark);
        var root=new DockPanel {Margin=new Thickness(24)}; Content=root;
        var heading=new StackPanel(); DockPanel.SetDock(heading,Dock.Top); root.Children.Add(heading);
        heading.Children.Add(new TextBlock {Text="TinyTask 2.0",FontSize=29,FontWeight=FontWeights.Bold});
        heading.Children.Add(new TextBlock {Text="INPUT LAB  /  Technical proof of concept",Margin=new Thickness(0,2,0,14)});
        heading.Children.Add(new TextBlock {Text="Test background input without moving your system cursor. Physical input is observed, never blocked.",TextWrapping=TextWrapping.Wrap});
        heading.Children.Add(new TextBlock {Text="Input isolation and OS-independent focus: unavailable. A visual cursor does not change that.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,12)});
        heading.Children.Add(Row(Button("Refresh windows",RefreshWindows),targets));
        heading.Children.Add(Row(Button("Refresh devices",RefreshDevices),devices));
        heading.Children.Add(Row(Label("Client X"),x,Label("Y"),y,visual,follow));
        heading.Children.Add(Row(Button("Move virtual cursor",MoveVirtual),Button("Open test target",OpenFixture),Button("Start observing",Start),Button("Stop / release",Stop)));
        heading.Children.Add(Row(Button("Test left click",()=>Probe("click")),Button("Test F6 down/up",()=>Probe("key")),Button("Test scroll",()=>Probe("scroll")),Button("Test UIA Invoke",()=>Probe("uia"))));
        heading.Children.Add(Row(sampleText,Button("Send test text",()=>Probe("text")),Button("Mark worked",()=>Verdict("User observed success")),Button("Mark failed",()=>Verdict("User observed failure"))));
        var isolation=new CheckBox {Content="Input Isolation Mode (unsupported by this user-mode prototype)",IsEnabled=false}; heading.Children.Add(Row(isolation));
        var always=new CheckBox {Content="Always on top",IsChecked=prefs.AlwaysOnTop}; always.Click+=(_,_)=>{prefs.AlwaysOnTop=always.IsChecked==true;Topmost=prefs.AlwaysOnTop;SaveSettings();};
        var minimize=new CheckBox {Content="Minimize to tray",IsChecked=prefs.MinimizeToTray}; minimize.Click+=(_,_)=>{prefs.MinimizeToTray=minimize.IsChecked==true;SaveSettings();};
        var dark=new CheckBox{Content="Dark mode",IsChecked=prefs.Dark};dark.Click+=(_,_)=>{prefs.Dark=dark.IsChecked==true;UiTheme.Apply(this,prefs.Dark);SaveSettings();};
        if(!hosted)heading.Children.Add(Row(dark));
        heading.Children.Add(Row(always,minimize,Button("Optional components",ShowSetup),Button("Export report",Export)));
        heading.Children.Add(Row(sources)); heading.Children.Add(Row(cursorText));
        var statusPanel=new Border {Padding=new Thickness(12),Margin=new Thickness(0,8,0,8),Child=status};statusPanel.SetResourceReference(BackgroundProperty,"Surface");heading.Children.Add(statusPanel);
        var footer=new TextBlock {Text="Emergency stop: Ctrl + Alt + F12  •  Close exits completely  •  No drivers installed",Margin=new Thickness(0,8,0,0)};
        DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer); root.Children.Add(log);
        visual.Click+=(_,_)=>MoveVirtual();
        SourceInitialized+=(_,_)=>{Initialize();UiTheme.Caption(this,prefs.Dark);};
        Loaded+=(_,_)=> { if(hosted && Owner!=null){UiTheme.Inherit(this,Owner);UiTheme.Caption(this,UiTheme.IsDark(Owner));} if(!hosted && !prefs.SetupSeen) ShowSetup(); };
        Closed+=(_,_)=>Cleanup();
        StateChanged+=(_,_)=> {if(!hosted && WindowState==WindowState.Minimized && prefs.MinimizeToTray) {Hide(); overlay.Hide();}};
        timer.Tick+=(_,_)=>Tick();
    }
    private string SettingsPath=>Path.Combine(DataDirectory,"settings.json");
    private static TextBlock Label(string value)=>new(){Text=value,VerticalAlignment=VerticalAlignment.Center};
    private Button Button(string text,Action action)
    {
        var b=new Button {Content=text,Padding=new Thickness(11,6,11,6),Margin=new Thickness(0,0,6,0)};
        b.Click+=(_,_)=>Safe(action); return b;
    }
    private static WrapPanel Row(params UIElement[] elements)
    { var row=new WrapPanel {Margin=new Thickness(0,6,0,0)}; foreach(var item in elements) { if(item is FrameworkElement e) e.Margin=new Thickness(0,0,8,4);row.Children.Add(item); } return row; }
    private void Safe(Action action) {try {action();} catch(Exception e) {Write("Error: "+e.Message);}}
    private void Initialize()
    {
        hwnd=new WindowInteropHelper(this).Handle; source=HwndSource.FromHwnd(hwnd); source.AddHook(WndProc); raw=new RawInput(hwnd);
        hotkeyReady=Native.RegisterHotKey(hwnd,12,0x4003,0x7B);
        if(!hosted){tray=new Forms.NotifyIcon {Text="TinyTask 2.0 Input Lab",Icon=System.Drawing.Icon.ExtractAssociatedIcon(AppIdentity.ExecutablePath),Visible=true};
        var menu=new Forms.ContextMenuStrip(); menu.Items.Add("Open Input Lab",null,(_,_)=>Dispatcher.Invoke(Restore));
        menu.Items.Add("Stop / release",null,(_,_)=>Dispatcher.Invoke(()=>Safe(Stop))); menu.Items.Add("Exit",null,(_,_)=>Dispatcher.Invoke(Close)); tray.ContextMenuStrip=menu; tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(Restore);}
        RefreshWindows(); Safe(RefreshDevices); Tick();
        Write(hotkeyReady?"Emergency stop registered. Observation is off until Start observing.":"Emergency shortcut is already in use. Probes disabled until the shortcut is available; restart after freeing Ctrl+Alt+F12.");
    }
    private void Restore(){Show();WindowState=WindowState.Normal;Activate();}
    internal async Task LifecycleTest(string output)
    {
        var results=new List<object>();
        Start();results.Add(new {name="Raw Input registration",passed=raw?.Enabled==true});
        Native.GetCursorPos(out var before);var focus=Native.GetForegroundWindow();overlay.Place(300,300);
        await Task.Delay(200);Native.GetCursorPos(out var after);
        results.Add(new {name="Visual cursor independent",passed=before.X==after.X&&before.Y==after.Y&&focus==Native.GetForegroundWindow()});
        Native.Post(hwnd,0x312,12,0);await Task.Delay(100);
        results.Add(new {name="Emergency message releases observation",passed=raw?.Enabled==false&&!overlay.IsVisible});
        await Task.Delay(2000); // Exclude initial WPF layout/JIT work from the idle sample.
        using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var watch=Stopwatch.StartNew();
        await Task.Delay(3000);process.Refresh();
        var elapsed=watch.Elapsed.TotalMilliseconds;var used=(process.TotalProcessorTime-cpu).TotalMilliseconds;
        File.WriteAllText(output,JsonSerializer.Serialize(new {results,idleWallMs=elapsed,idleCpuMs=used,idleCpuPercentOfOneCore=100*used/elapsed,workingSetBytes=process.WorkingSet64,scope="Programmatic lifecycle test; physical hotkey delivery and crash recovery not verified."},new JsonSerializerOptions{WriteIndented=true}));
    }
    private nint WndProc(nint window,int message,nint w,nint l,ref bool handled)
    {
        if(message==0xFF && raw?.Enabled==true) { lastSample=raw.Read(l); packets++; Track(); }
        if(message==0xFE) Safe(RefreshDevices);
        if(message==0x312 && w==12) {Safe(Stop);handled=true;}
        // Do not consume WM_INPUT: DefWindowProc must perform its cleanup.
        return 0;
    }
    private void Start() {if(!hotkeyReady) throw new InvalidOperationException("Emergency shortcut unavailable.");raw?.Start();timer.Start();Write("Observing mouse/keyboard device metadata. Key values are not logged. Routing remains off.");}
    private void Stop() {pending?.Cancel();Routing.ReleaseAll();raw?.Stop();timer.Stop();follow.IsChecked=false;visual.IsChecked=false;overlay.Hide();Tick();Write("Stopped. Raw Input registration released; no physical input was blocked.");}
    internal void EmergencyStop()=>Safe(Stop);
    private void RefreshDevices()
    {
        var old=(devices.SelectedItem as Device)?.Handle; var list=RawInput.Devices(); devices.ItemsSource=list;
        devices.SelectedItem=list.FirstOrDefault(d=>d.Handle==old)??list.FirstOrDefault(d=>d.Type==0);
        Write($"Enumerated {list.Count} raw input device(s). Remote sessions can omit physical devices.");
    }
    private void RefreshWindows()
    {
        var old=SelectedTarget??rememberedTarget;
        var list=Native.Windows().Where(t=>t.Pid!=Environment.ProcessId).ToList();targets.ItemsSource=list;
        targets.SelectedItem=old==null?list.FirstOrDefault():list.FirstOrDefault(t=>t.Handle==old.Handle && t.Pid==old.Pid);
        rememberedTarget=SelectedTarget??old;
    }
    private void Track()
    {
        if(follow.IsChecked!=true || lastSample is not {Type:0} s || devices.SelectedItem is not Device d || s.Device!=d.Handle) return;
        if((s.Flags&1)==0) {virtualX=Math.Clamp(virtualX+s.X,0,32767);virtualY=Math.Clamp(virtualY+s.Y,0,32767);}
        else if(targets.SelectedItem is Target t && Native.GetClientRect(t.Handle,out var r)) {virtualX=(int)((long)s.X*Math.Max(0,r.Right-1)/65535);virtualY=(int)((long)s.Y*Math.Max(0,r.Bottom-1)/65535);}
        // This is a software coordinate accumulator, not OS device suppression.
    }
    private void Tick()
    {
        Native.GetCursorPos(out var real);
        sources.Text=lastSample==null?$"Observation: {(raw?.Enabled==true?"on":"off")} • {packets} packets":$"Observation: {(raw?.Enabled==true?"on":"off")} • {packets} packets • Last source: 0x{lastSample.Device:X} ({(lastSample.Type==0?"mouse":"keyboard")})";
        cursorText.Text=$"System cursor: {real.X}, {real.Y}   |   Virtual client cursor: {virtualX}, {virtualY}   |   Real focus: 0x{Native.GetForegroundWindow():X}";
        if(visual.IsChecked==true && IsVisible && WindowState!=WindowState.Minimized && targets.SelectedItem is Target target && Native.IsWindow(target.Handle) && !Native.IsIconic(target.Handle))
        {var point=new Native.Point(virtualX,virtualY);Native.ClientToScreen(target.Handle,ref point);overlay.Place(point.X,point.Y);}
        else overlay.Hide();
    }
    private void MoveVirtual()
    {
        virtualX=Coordinate(x);virtualY=Coordinate(y);if(visual.IsChecked==true)timer.Start();else if(raw?.Enabled!=true)timer.Stop();Tick();
    }
    private static int Coordinate(TextBox box)=>int.TryParse(box.Text,out int value)&&value>=0&&value<=32767?value:throw new ArgumentException("Coordinates must be whole numbers from 0 to 32767.");
    private async void Probe(string kind)
    {
        if(pending!=null) {Write("A probe is already running. Stop it first.");return;}
        using var cancel=new CancellationTokenSource(); pending=cancel;
        try
        {
            if(!hotkeyReady) throw new InvalidOperationException("Emergency shortcut unavailable.");
            if(targets.SelectedItem is not Target target) throw new InvalidOperationException("Select a target window.");
            int px=Coordinate(x),py=Coordinate(y); string text=sampleText.Text;
            Routing.Receiver(target,px,py);
            Write($"{kind} probe in 3 seconds. Switch to another app to test unfocused delivery. Target: {target.Title}");
            await Task.Delay(3000,cancel.Token);target.Validate();
            var before=Native.GetForegroundWindow();Native.GetCursorPos(out var cursorBefore); var watch=Stopwatch.StartNew();
            if(kind=="click") await Routing.Click(target,px,py,cancel.Token);
            else if(kind=="key") Routing.Key(target,px,py,0x75);
            else if(kind=="text") Routing.Text(target,px,py,text);
            else if(kind=="scroll") Routing.Scroll(target,px,py,120);
            else await Routing.Invoke(target,px,py,cancel.Token);
            Native.GetCursorPos(out var afterCursor);var after=Native.GetForegroundWindow();
            observations.Add(new {time=DateTimeOffset.UtcNow,kind,target=target.ToString(),px,py,elapsedMs=watch.Elapsed.TotalMilliseconds,foregroundBefore=before.ToString(),foregroundAfter=after.ToString(),targetWasForeground=before==target.Handle,cursorBefore=new[]{cursorBefore.X,cursorBefore.Y},cursorAfter=new[]{afterCursor.X,afterCursor.Y},result=kind=="uia"?"Provider returned; effect unverified":"Queued; target behavior unverified"});
            Write($"{kind}: {(kind=="uia"?"provider returned":"messages queued")}. Target response is UNVERIFIED. Observe it, then mark worked/failed. Focus changed: {before!=after}.");
        }
        catch(OperationCanceledException) {Write("Probe cancelled or UIA provider timed out. Any posted mouse-down is paired with mouse-up.");}
        catch(Exception e) {Write("Probe failed: "+e.Message);observations.Add(new {time=DateTimeOffset.UtcNow,kind,error=e.Message});}
        finally {pending=null;}
    }
    private void Verdict(string value) {observations.Add(new {time=DateTimeOffset.UtcNow,verdict=value,scope="Most recent probe; human observation"});Write(value+" for most recent probe.");}
    private void OpenFixture()
    {
        var process=Process.Start(new ProcessStartInfo(AppIdentity.ExecutablePath) {UseShellExecute=false,ArgumentList={"--fixture",Guid.NewGuid().ToString("N")[..6]}});
        if(process!=null) fixtures.Add(process);
        Write("Test target opened. Refresh windows and select it. Button: (80,80), text box: (80,135). F6 counter accepts WM_KEYDOWN.");
    }
    private void Export()
    {
        var dialog=new Microsoft.Win32.SaveFileDialog {Filter="JSON report|*.json",FileName="TinyTask-input-report.json"}; if(dialog.ShowDialog(this)!=true)return;
        var report=new {version="0.2.0",generated=DateTimeOffset.UtcNow,os=Environment.OSVersion.ToString(),session=Process.GetCurrentProcess().SessionId,deviceCount=devices.Items.Count,physicalSuppression=false,independentOsFocus=false,observations};
        File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true}));Write("Report exported. It includes target window titles, but no typed test text or physical keystrokes.");
    }
    private void ShowSetup()
    {
        var dialog=new Window {Title="Optional components",Owner=this,Width=620,Height=350,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        UiTheme.Inherit(dialog,this);var panel=new StackPanel {Margin=new Thickness(24)};dialog.Content=panel;
        panel.Children.Add(new TextBlock {Text="Everything needed for Input Lab is included.",FontSize=20,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap});
        panel.Children.Add(new TextBlock {Margin=new Thickness(0,15,0,15),TextWrapping=TextWrapping.Wrap,Text="ASTER is an optional, separately licensed multiseat product. It may provide independent workstations on one PC, usually with additional display/input hardware. Roblox compatibility has not been verified. It is not a TinyTask dependency.\n\nThis app will not download drivers, install software, or change your input configuration automatically."});
        panel.Children.Add(Button("Open official ASTER website",()=>Process.Start(new ProcessStartInfo("https://ibiksoft.com/"){UseShellExecute=true})));
        panel.Children.Add(Button("Continue without optional components",()=>dialog.Close()));
        dialog.ShowDialog();prefs.SetupSeen=true;SaveSettings();
    }
    private void SaveSettings()
    {Safe(()=> {Directory.CreateDirectory(DataDirectory);File.WriteAllText(SettingsPath+".tmp",JsonSerializer.Serialize(prefs));File.Move(SettingsPath+".tmp",SettingsPath,true);});}
    private void Write(string text)
    {if(closed)return;status.Text=text;log.AppendText($"{DateTime.Now:HH:mm:ss}  {text}\n");if(log.Text.Length>24000)log.Text=log.Text[^16000..];log.ScrollToEnd();}
    private void Cleanup()
    {
        if(closed)return;pending?.Cancel();Routing.ReleaseAll();timer.Stop();SaveSettings();closed=true;
        try {raw?.Dispose();} catch { /* Process exit also removes registrations. */ }
        source?.RemoveHook(WndProc);Native.UnregisterHotKey(hwnd,12);overlay.Close();tray?.Dispose();
        foreach(var fixture in fixtures) {if(!fixture.HasExited)fixture.CloseMainWindow();fixture.Dispose();}
    }
}

internal sealed class CursorWindow : Window
{
    private nint handle;
    private int lastX=int.MinValue,lastY=int.MinValue;
    public CursorWindow()
    {
        Width=32;Height=36;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;IsHitTestVisible=false;
        Content=new System.Windows.Shapes.Path {Data=Geometry.Parse("M 2,2 L 2,25 L 8,19 L 13,30 L 18,28 L 13,17 L 23,17 Z"),Fill=Brushes.DeepSkyBlue,Stroke=Brushes.White,StrokeThickness=2};
        SourceInitialized+=(_,_)=> {handle=new WindowInteropHelper(this).Handle;Native.SetWindowLongPtr(handle,-20,Native.GetWindowLongPtr(handle,-20)|0x08000000|0x20|0x80);};
    }
    internal void Place(int x,int y) {if(IsVisible&&x==lastX&&y==lastY)return;if(!IsVisible)Show();Native.SetWindowPos(handle,(nint)(-1),x,y,0,0,0x0011);lastX=x;lastY=y;}
}
