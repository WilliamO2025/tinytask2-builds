using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TinyTask;

internal sealed class SetupWizard : Window
{
    internal Target? Target {get;private set;}
    internal bool Completed {get;private set;}
    internal bool RequestDiagnostics {get;private set;}
    internal string? MacroMousePath {get;private set;}
    internal string? UserMousePath {get;private set;}
    private int step;
    private bool second,detected,clicked,hotkey;
    private int normalClicks,virtualClicks;
    private readonly Button testButton=new(){Width=140,Height=60,Margin=new Thickness(0),Content="Test button",Background=Brushes.RoyalBlue,Foreground=Brushes.White};
    private readonly TextBlock heading=new(){FontSize=23,FontWeight=FontWeights.SemiBold},subtitle=new(){Margin=new Thickness(0,8,0,20),TextWrapping=TextWrapping.Wrap};
    private readonly StackPanel body=new();
    private readonly Button next=new(){Content="Next"},back=new(){Content="Back"};
    private readonly ComboBox apps=new(){MinWidth=380},macroMouse=new(){MinWidth=380},userMouse=new(){MinWidth=380};
    private readonly TextBlock observation=new(){Margin=new Thickness(0,12,0,0),TextWrapping=TextWrapping.Wrap};
    private readonly Canvas pad=new(){Width=460,Height=160,Background=Brushes.AliceBlue,ClipToBounds=true};
    private readonly Ellipse pointer=new(){Width=14,Height=14,Fill=Brushes.RoyalBlue,IsHitTestVisible=false};
    private RawInput? raw;
    private HwndSource? source;
    private nint hwnd;
    private int px=35,py=80;
    private readonly DispatcherTimer render=new(){Interval=TimeSpan.FromMilliseconds(33)};
    internal SetupWizard(Target? target,string? macroPath=null,string? userPath=null,bool directMouse=false,bool testCursor=false)
    {
        MacroMousePath=macroPath;UserMousePath=userPath;second=directMouse;step=directMouse?2:0;
        Target=target;Title="Set up Advanced Mode";Width=590;Height=575;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        apps.ItemTemplate=FriendlyTarget.Template();
        FontFamily=new FontFamily("Segoe UI");FontSize=14;Background=new SolidColorBrush(Color.FromRgb(244,246,250));Foreground=Brushes.MidnightBlue;
        var root=new DockPanel{Margin=new Thickness(26)};Content=root;
        var top=new StackPanel();top.Children.Add(heading);top.Children.Add(subtitle);DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        var bottom=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        foreach(var b in new[]{back,next}){b.Padding=new Thickness(22,10,22,10);b.Margin=new Thickness(4);bottom.Children.Add(b);}
        DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        back.Click+=(_,_)=>{StopObservation();step=Math.Max(0,step-1);ShowStep();};next.Click+=(_,_)=>Next();
        SourceInitialized+=(_,_)=>{hwnd=new WindowInteropHelper(this).Handle;source=HwndSource.FromHwnd(hwnd);source.AddHook(WndProc);raw=new RawInput(hwnd);hotkey=Native.RegisterHotKey(hwnd,14,0x4003,0x7B);};
        Closed+=(_,_)=>{StopObservation();raw?.Dispose();source?.RemoveHook(WndProc);if(hotkey)Native.UnregisterHotKey(hwnd,14);};
        render.Tick+=(_,_)=>UpdateFeedback();
        testButton.Click+=(_,_)=>{normalClicks++;UpdateFeedback();};
        pad.SetResourceReference(BackgroundProperty,"TestSurface");
        Loaded+=(_,_)=>{if(Owner!=null){UiTheme.Inherit(this,Owner);UiTheme.Caption(this,UiTheme.IsDark(Owner));}};
        Loaded+=(_,_)=>{if(testCursor && macroMouse.SelectedItem is MouseChoice m && userMouse.SelectedItem is MouseChoice u && m.Device.Path==macroPath && u.Device.Path==userPath && m.Device.Handle!=u.Device.Handle){step=3;ShowStep();}};
        ShowStep();
    }
    private void Text(string text)=>body.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,10)});
    private void ShowStep()
    {
        body.Children.Clear();back.IsEnabled=step>0;next.Content=step==4?"Finish":"Next";next.IsEnabled=true;
        heading.Text=$"{step+1} of 5 · "+new[]{"Choose your app","Choose your input","Choose your mice","Try the second cursor","Check compatibility"}[step];
        subtitle.Text="Set up your workspace. You can change this later.";
        if(step==0)
        {
            var list=FriendlyTarget.List();apps.ItemsSource=list;apps.SelectedItem=Target==null?list.FirstOrDefault():list.FirstOrDefault(t=>t.Target.Handle==Target.Handle && t.Target.Pid==Target.Pid);
            body.Children.Add(apps);Text("Keep your target app open. Choose the window you want to test.");
        }
        if(step==1)
        {
            var normal=new RadioButton{Content="Use my normal mouse",IsChecked=!second,Margin=new Thickness(0,10,0,16)};
            var separate=new RadioButton{Content="Use a second mouse so I can keep using my computer",IsChecked=second};
            normal.Checked+=(_,_)=>second=false;separate.Checked+=(_,_)=>second=true;body.Children.Add(normal);body.Children.Add(separate);
            Text("Second-mouse detection is available. Independent control is not available yet: both mice still affect the Windows cursor.");
        }
        if(step==2)
        {
            if(!second){Text("Your normal mouse remains unchanged. Continue to review target compatibility.");return;}
            var mice=RawInput.Devices().Where(d=>d.Type==0).Select((d,i)=>new MouseChoice(d,AdvancedInputSetup.MouseName(d,i))).ToList();
            macroMouse.ItemsSource=mice;userMouse.ItemsSource=mice;
            macroMouse.SelectedItem=string.IsNullOrEmpty(MacroMousePath)?mice.ElementAtOrDefault(mice.Count>1?1:0):mice.FirstOrDefault(m=>m.Device.Path==MacroMousePath);
            userMouse.SelectedItem=string.IsNullOrEmpty(UserMousePath)?mice.FirstOrDefault():mice.FirstOrDefault(m=>m.Device.Path==UserMousePath);
            Text("Use this mouse for TinyTask");body.Children.Add(macroMouse);Text("Use this mouse for me");body.Children.Add(userMouse);
            Text("Move each selected mouse on the next page to identify it. Device choices are saved when you finish. Both physical mice still affect the normal Windows cursor.");
            if(mice.Count<2){Text("Two mouse devices are needed for this test. Connect another mouse, or go back and select your normal mouse.");next.IsEnabled=false;}
        }
        if(step==3)
        {
            if(!second){Text("Second cursor test skipped because you selected your normal mouse.");return;}
            if(!hotkey){Text("The emergency shortcut is in use. Close other diagnostics windows and reopen setup to test the mouse.");return;}
            detected=false;clicked=false;px=35;py=80;pad.Children.Clear();
            BuildTestPad();body.Children.Add(pad);body.Children.Add(observation);UpdateFeedback();
            Text("This blue cursor is a test pointer. Your normal cursor will also move. Ctrl + Alt + F12 stops observation.");
            var stop=new Button{Content="Disable test cursor"};stop.Click+=(_,_)=>{StopObservation();pointer.Visibility=Visibility.Hidden;observation.Text="Test stopped. Physical controls were never blocked.";};body.Children.Add(stop);pointer.Visibility=Visibility.Visible;
            raw!.Start();render.Start();
        }
        if(step==4)
        {
            bool roblox=Target?.Process.Equals("RobloxPlayerBeta",StringComparison.OrdinalIgnoreCase)==true;
            Text(Target==null?"No target selected.":"Results for "+FriendlyTarget.Name(Target.Process));
            Text(roblox?"Previous Roblox tests (20 Sep 2026)\n\nMouse clicks: No menu activation observed\nKeyboard: Worked in foreground with scan-code Escape\nBackground input: Did not activate the menu\nInput isolation: Unavailable":"Mouse clicks: Not tested\nKeyboard: Not tested\nBackground input: Not tested\nInput isolation: Unavailable");
            Text(second?$"Second mouse detection: {(detected?"Passed":"Not verified")}\nSecond-cursor click: {(clicked?"Passed":"Not verified")}":"Using your normal mouse.");
            Text("Some Advanced Mode features are unavailable for this application. No input has been sent to the target by this wizard.");
            var test=new Button{Content="Open diagnostics for a controlled test",Padding=new Thickness(12,8,12,8)};
            test.Click+=(_,_)=>{RequestDiagnostics=true;Completed=true;Close();};body.Children.Add(test);
        }
    }
    private void Next()
    {
        try
        {
            if(step==0){Target=(apps.SelectedItem as FriendlyTarget)?.Target;if(Target==null)throw new InvalidOperationException("Choose an app first.");Target.Validate();}
            if(step==2 && second && (macroMouse.SelectedItem is not MouseChoice m || userMouse.SelectedItem is not MouseChoice u || m.Device.Handle==u.Device.Handle))throw new InvalidOperationException("Choose a different mouse for each role.");
            if(step==2 && second)
            {
                var chosen=(MouseChoice)macroMouse.SelectedItem;var personal=(MouseChoice)userMouse.SelectedItem;var connected=RawInput.Devices();
                if(!connected.Any(d=>d.Handle==chosen.Device.Handle && d.Path==chosen.Device.Path) || !connected.Any(d=>d.Handle==personal.Device.Handle && d.Path==personal.Device.Path))throw new InvalidOperationException("A selected mouse disconnected. Go Back and return to refresh the list.");
                MacroMousePath=chosen.Device.Path;UserMousePath=personal.Device.Path;
            }
            StopObservation();if(step==4){Completed=true;Close();return;}step++;ShowStep();
        }catch(Exception e){UiTheme.Message(this,e.Message,"Setup");}
    }
    private nint WndProc(nint window,int message,nint w,nint l,ref bool handled)
    {
        if(message==0x312 && w==14){StopObservation();observation.Text="Observation stopped. Your controls were not blocked.";handled=true;}
        if(message==0xFF && raw?.Enabled==true && raw.Read(l) is {Type:0} sample)Observe(sample);
        return 0;
    }
    private void Observe(RawSample sample)
    {
        if(macroMouse.SelectedItem is not MouseChoice mouse || sample.Device!=mouse.Device.Handle)return;
        detected=true;
        if((sample.Flags&1)==0){px=Math.Clamp(px+sample.X,0,446);py=Math.Clamp(py+sample.Y,0,146);}
        else {px=(int)((long)sample.X*446/65535);py=(int)((long)sample.Y*146/65535);}
        if((sample.Buttons&1)!=0 && new Rect(Canvas.GetLeft(testButton),Canvas.GetTop(testButton),testButton.Width,testButton.Height).Contains(new Point(px,py))){clicked=true;virtualClicks++;UpdateFeedback();}
    }
    private void BuildTestPad()
    {
        pad.Children.Clear();Canvas.SetLeft(testButton,280);Canvas.SetTop(testButton,50);pad.Children.Add(testButton);pad.Children.Add(pointer);
    }
    private void UpdateFeedback()
    {
        Canvas.SetLeft(pointer,px);Canvas.SetTop(pointer,py);
        observation.Text=(detected?"Selected mouse detected":"Move your selected mouse.")+ $"\nNormal button clicks: {normalClicks} · Selected cursor presses: {virtualClicks}";
    }
    internal async System.Threading.Tasks.Task<bool> TestRepeatedClicks()
    {
        heading.Text="4 of 5 · Try the second cursor";
        BuildTestPad();body.Children.Clear();body.Children.Add(pad);body.Children.Add(observation);
        for(int i=0;i<12;i++)testButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UpdateLayout();if(normalClicks!=12 || virtualClicks!=0 || pointer.IsHitTestVisible || testButton.ActualWidth!=140 || testButton.ActualHeight!=60)return false;
        Topmost=true;Activate();Native.SetForegroundWindow(hwnd);await System.Threading.Tasks.Task.Delay(200);
        if(Native.GetForegroundWindow()!=hwnd)
        {
            Native.GetWindowRect(hwnd,out var bounds);var titlePoint=new Native.Point(bounds.Left+100,bounds.Top+12);
            if(Native.GetAncestor(Native.WindowFromPoint(titlePoint),2)!=hwnd)throw new InvalidOperationException("Wizard title bar is occluded; test input not sent.");
            await System.Threading.Tasks.Task.Run(()=>{ClassicEngine.Send(new(){Type="mouseDown",X=titlePoint.X,Y=titlePoint.Y});ClassicEngine.Send(new(){Type="mouseUp",X=titlePoint.X,Y=titlePoint.Y});});
            await System.Threading.Tasks.Task.Delay(200);
        }
        var point=testButton.PointToScreen(new Point(70,30));
        for(int i=0;i<6;i++)
        {
            if(Native.GetForegroundWindow()!=hwnd)throw new InvalidOperationException("Wizard is not focused; test input not sent.");
            await System.Threading.Tasks.Task.Run(()=>{ClassicEngine.Send(new(){Type="mouseDown",X=(int)point.X,Y=(int)point.Y});ClassicEngine.Send(new(){Type="mouseUp",X=(int)point.X,Y=(int)point.Y});});
            await System.Threading.Tasks.Task.Delay(100);
            if(normalClicks!=13+i)return false;
        }
        return virtualClicks==0;
    }
    private void StopObservation(){render.Stop();try{raw?.Stop();}catch(Exception e){observation.Text="Could not stop observation: "+e.Message;}}
    internal bool TestDeviceFilter()
    {
        BuildTestPad();
        macroMouse.Items.Add(new MouseChoice(new Device((nint)101,0,"test-only"),"Test mouse"));macroMouse.SelectedIndex=0;
        Observe(new RawSample((nint)202,0,300,0,0,1,0));if(detected || clicked || px!=35)return false;
        Observe(new RawSample((nint)101,0,270,0,0,1,0));return detected && clicked && px==305;
    }
    internal bool HasNoSelectedMice=>macroMouse.SelectedItem==null && userMouse.SelectedItem==null;
    private sealed record MouseChoice(Device Device,string Name){public override string ToString()=>Name;}
}
