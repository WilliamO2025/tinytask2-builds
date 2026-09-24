using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace TinyTask;

// This readiness screen never downloads, elevates, installs, filters, or removes input devices.
// An install action must not be enabled before a concrete package and its lifecycle are approved.
internal static class AdvancedInputSetup
{
    internal static string MouseName(Device device,int index)
    {
        string name="Mouse "+(index+1);
        try
        {
            var parts=device.Path.Replace(@"\\?\","").Split('#');
            if(parts.Length<3 || parts.Take(3).Any(p=>p.Contains('\\') || p.Contains('/')))return name;
            using var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\"+string.Join("\\",parts.Take(3)));
            string? description=key?.GetValue("FriendlyName") as string??key?.GetValue("DeviceDesc") as string;
            if(!string.IsNullOrWhiteSpace(description))name+=" - "+description.Split(';').Last()[..Math.Min(description.Split(';').Last().Length,80)];
        }
        catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}catch(System.IO.IOException){}
        return name;
    }
    internal static Window Create(Window owner)
    {
        var window=new Window{Owner=owner,Title="Advanced Input Support",Width=530,Height=510,WindowStartupLocation=WindowStartupLocation.CenterOwner};UiTheme.Inherit(window,owner);
        var panel=new StackPanel{Margin=new Thickness(24)};window.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        void Text(string value)=>panel.Children.Add(new TextBlock{Text=value,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)});
        Text("Advanced Input Support — Setup not yet available");
        Text("No TinyTask-managed input component is installed. Classic Mode works without one.");
        Text("The device-filter policy prototype passes its software recovery tests. It is not an installable driver: the Windows driver adapter, routing service, hardware tests and trusted signing are still required.");
        Text("The separate 20-second routing test can move a virtual cursor while the real cursor stays still. Roblox may still receive physical clicks. This is an experiment, not verified Roblox isolation.");
        var experiment=new Button{Content="Get experimental routing test"};
        experiment.Click+=(_,_)=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/WilliamO2025/tinytask2-builds/releases/tag/v0.2.0-rc3"){UseShellExecute=true});}catch(Exception e){UiTheme.Message(window,e.Message,"Could not open downloads");}};
        panel.Children.Add(experiment);
        Text("Installation is blocked until a specific package passes publisher, license, signature, device-safety and removal checks. No software is downloaded and no administrator permission is requested on this screen.");
        var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)};
        var check=new Button{Content="Check connected devices"};check.Click+=(_,_)=>{try{var devices=RawInput.Devices();status.Text=$"Windows reports {devices.Count(d=>d.Type==0)} mouse and {devices.Count(d=>d.Type==1)} keyboard entries. Detection is not proof of working input or isolation.";}catch(Exception e){status.Text="Device check failed. Retry or open Diagnostics. "+e.Message;}};
        panel.Children.Add(check);panel.Children.Add(status);
        var install=new Button{Content="Install Advanced Input Support",IsEnabled=false,ToolTip="No approved installable TinyTask component is available."};panel.Children.Add(install);
        Text("Removal: there is no TinyTask-managed component to uninstall. This app will not remove another application's drivers.");
        var close=new Button{Content="Close",IsCancel=true};close.Click+=(_,_)=>window.Close();panel.Children.Add(close);return window;
    }
}
