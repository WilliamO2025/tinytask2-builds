using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace TinyTask;

internal static class UiTheme
{
    internal static bool IsDark(Window window)=>window.Resources["Dark"] is true;
    internal static void Apply(Window window,bool dark)
    {
        if(!window.Resources.Contains("ThemeInstalled"))window.Resources=Styles();
        window.Resources["Dark"]=dark;
        foreach(var (key,light,night) in new[]{("Canvas","#F4F6FA","#151922"),("Surface","#FFFFFF","#222836"),("Ink","#202B3E","#EDF2FA"),("Muted","#5D6C83","#AFBCD0"),("Line","#DCE3EE","#465269"),("Hover","#E7EDFA","#35415A"),("TestSurface","#EDF3FF","#1C2942")})
            window.Resources[key]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark?night:light));
        window.SetResourceReference(Window.BackgroundProperty,"Canvas");window.SetResourceReference(Window.ForegroundProperty,"Ink");
        Caption(window,dark);
    }
    internal static void Inherit(Window window,Window owner)
    {
        window.Resources=owner.Resources;window.SetResourceReference(Window.BackgroundProperty,"Canvas");window.SetResourceReference(Window.ForegroundProperty,"Ink");
        window.SourceInitialized+=(_,_)=>Caption(window,IsDark(owner));
    }
    internal static void Caption(Window window,bool dark)
    {
        nint handle=new WindowInteropHelper(window).Handle;if(handle==0)return;
        int enabled=dark?1:0;DwmSetWindowAttribute(handle,20,ref enabled,sizeof(int));
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window,int attribute,ref int value,int size);
    internal static void Message(Window owner,string text,string title)
    {
        var dialog=new Window{Owner=owner,Title=title,Width=430,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};Inherit(dialog,owner);
        var panel=new StackPanel{Margin=new Thickness(24)};dialog.Content=panel;
        panel.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18)});
        var ok=new Button{Content="OK",IsDefault=true,IsCancel=true,MinWidth=80,HorizontalAlignment=HorizontalAlignment.Right};ok.Click+=(_,_)=>dialog.Close();panel.Children.Add(ok);dialog.ShowDialog();
    }
    internal static ResourceDictionary Styles()
    {
        var resources=(ResourceDictionary)XamlReader.Parse("""
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style TargetType="TextBlock"><Setter Property="Foreground" Value="{DynamicResource Ink}"/></Style>
          <Style TargetType="Button"><Setter Property="MinHeight" Value="36"/><Setter Property="Padding" Value="14,8"/><Setter Property="Margin" Value="3"/><Setter Property="Background" Value="{DynamicResource Surface}"/><Setter Property="Foreground" Value="{DynamicResource Ink}"/><Setter Property="BorderBrush" Value="{DynamicResource Line}"/><Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="Frame" CornerRadius="8" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="#5985EB"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="#5985EB"/><Setter TargetName="Frame" Property="BorderThickness" Value="2"/></Trigger><Trigger Property="IsPressed" Value="True"><Setter Property="Opacity" Value="0.7"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="CheckBox"><Setter Property="Foreground" Value="{DynamicResource Ink}"/></Style>
          <Style TargetType="RadioButton"><Setter Property="Foreground" Value="{DynamicResource Ink}"/></Style>
          <Style TargetType="TextBox"><Setter Property="Padding" Value="8,5"/><Setter Property="Foreground" Value="{DynamicResource Ink}"/><Setter Property="CaretBrush" Value="{DynamicResource Ink}"/><Setter Property="Background" Value="{DynamicResource Surface}"/><Setter Property="BorderBrush" Value="{DynamicResource Line}"/></Style>
          <Style TargetType="ComboBoxItem"><Setter Property="Foreground" Value="{DynamicResource Ink}"/><Setter Property="Background" Value="{DynamicResource Surface}"/><Setter Property="Padding" Value="8,6"/><Setter Property="HorizontalContentAlignment" Value="Stretch"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem"><Border x:Name="Item" Padding="{TemplateBinding Padding}" Background="{TemplateBinding Background}"><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property="IsHighlighted" Value="True"><Setter TargetName="Item" Property="Background" Value="{DynamicResource Hover}"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter TargetName="Item" Property="Background" Value="{DynamicResource Hover}"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
          <Style TargetType="ComboBox"><Setter Property="MinHeight" Value="36"/><Setter Property="Foreground" Value="{DynamicResource Ink}"/><Setter Property="Background" Value="{DynamicResource Surface}"/><Setter Property="BorderBrush" Value="{DynamicResource Line}"/><Setter Property="ScrollViewer.CanContentScroll" Value="True"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid>
              <ToggleButton x:Name="Toggle" Focusable="False" IsChecked="{Binding IsDropDownOpen,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}" ClickMode="Press"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border x:Name="Frame" Background="{DynamicResource Surface}" BorderBrush="{DynamicResource Line}" BorderThickness="1" CornerRadius="6"><TextBlock Text="⌄" Foreground="{DynamicResource Ink}" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,10,3"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="#5985EB"/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template></ToggleButton>
              <ContentPresenter x:Name="Selection" Margin="10,5,30,5" VerticalAlignment="Center" IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" ContentStringFormat="{TemplateBinding SelectionBoxItemStringFormat}"/>
              <TextBox x:Name="PART_EditableTextBox" Visibility="Hidden" Margin="4,2,28,2" BorderThickness="0" VerticalContentAlignment="Center" IsReadOnly="{TemplateBinding IsReadOnly}"/>
              <Popup x:Name="PART_Popup" Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}" AllowsTransparency="True" Focusable="False"><Border MinWidth="{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}" MaxHeight="{TemplateBinding MaxDropDownHeight}" Background="{DynamicResource Surface}" BorderBrush="{DynamicResource Line}" BorderThickness="1" CornerRadius="6" Padding="2"><ScrollViewer CanContentScroll="True"><ItemsPresenter/></ScrollViewer></Border></Popup>
              </Grid><ControlTemplate.Triggers><Trigger Property="IsEditable" Value="True"><Setter TargetName="Selection" Property="Visibility" Value="Hidden"/><Setter TargetName="PART_EditableTextBox" Property="Visibility" Value="Visible"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="Expander"><Setter Property="Foreground" Value="{DynamicResource Ink}"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Expander"><StackPanel><ToggleButton Foreground="{DynamicResource Ink}" HorizontalContentAlignment="Left" IsChecked="{Binding IsExpanded,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="Transparent" Padding="2,6"><DockPanel><TextBlock Foreground="{DynamicResource Ink}" x:Name="Arrow" Text="›" FontSize="18" Width="22"/><ContentPresenter Content="{Binding Header,RelativeSource={RelativeSource AncestorType=Expander}}" VerticalAlignment="Center"/></DockPanel></Border><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Arrow" Property="Text" Value="⌄"/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter x:Name="Body" Visibility="Collapsed"/></StackPanel><ControlTemplate.Triggers><Trigger Property="IsExpanded" Value="True"><Setter TargetName="Body" Property="Visibility" Value="Visible"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
          <Style TargetType="ScrollBar"><Setter Property="Background" Value="{DynamicResource Canvas}"/><Setter Property="Width" Value="12"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Grid Background="{TemplateBinding Background}"><Track x:Name="PART_Track" Orientation="{TemplateBinding Orientation}" IsDirectionReversed="True"><Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="{DynamicResource Line}" CornerRadius="5" Margin="2"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton></Track></Grid><ControlTemplate.Triggers><Trigger Property="Orientation" Value="Horizontal"><Setter Property="Width" Value="Auto"/><Setter Property="Height" Value="12"/><Setter TargetName="PART_Track" Property="IsDirectionReversed" Value="False"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
        </ResourceDictionary>
        """);resources["ThemeInstalled"]=true;return resources;
    }
}
