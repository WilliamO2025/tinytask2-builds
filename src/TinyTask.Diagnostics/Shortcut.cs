using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TinyTask;

internal sealed record Shortcut(int Key,uint Modifiers=0)
{
    internal bool Matches(int key,uint modifiers)=>Key==key&&Modifiers==modifiers;
    internal static uint CurrentModifiers()=>
        ((Native.GetAsyncKeyState(0x12)&0x8000)!=0?1u:0)|
        ((Native.GetAsyncKeyState(0x11)&0x8000)!=0?2u:0)|
        ((Native.GetAsyncKeyState(0x10)&0x8000)!=0?4u:0)|
        (((Native.GetAsyncKeyState(0x5B)|Native.GetAsyncKeyState(0x5C))&0x8000)!=0?8u:0);
    public override string ToString()=>(Modifiers.HasFlag(2)?"Ctrl + ":"")+(Modifiers.HasFlag(4)?"Shift + ":"")+(Modifiers.HasFlag(1)?"Alt + ":"")+(Modifiers.HasFlag(8)?"Win + ":"")+KeyInterop.KeyFromVirtualKey(Key);
    internal static Shortcut? Capture(Window owner)
    {
        Shortcut? result=null;
        var dialog=new Window{Owner=owner,Title="Choose shortcut",Width=390,Height=165,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};
        UiTheme.Inherit(dialog,owner);
        dialog.Content=new TextBlock{Text="Press a key or key combination…\nEscape cancels. F10 remains the emergency stop.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(24)};
        dialog.PreviewKeyDown+=(_,e)=>{
            e.Handled=true;var key=e.Key==System.Windows.Input.Key.System?e.SystemKey:e.Key;
            if(key==System.Windows.Input.Key.Escape){dialog.Close();return;}
            if(key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin)return;
            int vk=KeyInterop.VirtualKeyFromKey(key);if(vk<1||vk>254)return;
            result=new(vk,CurrentModifiers());dialog.Close();
        };
        dialog.ShowDialog();return result;
    }
    internal static bool Contains(IReadOnlyList<MacroAction> actions,IReadOnlyList<Shortcut> bindings)
    {
        var held=new HashSet<int>();
        foreach(var action in actions)
        {
            if(action.Type=="keyDown")
            {
                held.Add(action.Key);
                uint modifiers=(held.Overlaps(new[]{18,164,165})?1u:0)|(held.Overlaps(new[]{17,162,163})?2u:0)|(held.Overlaps(new[]{16,160,161})?4u:0)|(held.Overlaps(new[]{91,92})?8u:0);
                foreach(var binding in bindings)if(binding.Matches(action.Key,modifiers)||action.Key==121)return true;
            }
            else if(action.Type=="keyUp")held.Remove(action.Key);
        }
        return false;
    }
}

internal static class ModifierBits
{
    internal static bool HasFlag(this uint value,uint mask)=>(value&mask)!=0;
}
