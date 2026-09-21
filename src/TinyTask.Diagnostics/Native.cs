using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TinyTask;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; public Point(int x, int y) { X=x; Y=y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct DeviceEntry { public nint Handle; public uint Type; }
    [StructLayout(LayoutKind.Sequential)] internal struct Registration { public ushort Page, Usage; public uint Flags; public nint Target; }
    [StructLayout(LayoutKind.Sequential)] internal struct RawHeader { public uint Type, Size; public nint Device, WParam; }
    [DllImport("user32.dll", SetLastError=true)] internal static extern uint GetRawInputDeviceList([Out] DeviceEntry[]? list, ref uint count, uint size);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern uint GetRawInputDeviceInfo(nint device, uint command, StringBuilder? data, ref uint size);
    [DllImport("user32.dll", SetLastError=true)] internal static extern bool RegisterRawInputDevices(Registration[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError=true)] internal static extern uint GetRawInputData(nint input, uint command, nint data, ref uint size, uint headerSize);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(nint window, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint window,uint flags);
    [DllImport("user32.dll",EntryPoint="SendMessageTimeoutW",SetLastError=true)] internal static extern nint SendMessageTimeout(nint hwnd,uint msg,nint w,nint l,uint flags,uint timeout,out nuint result);
    [DllImport("user32.dll")] internal static extern bool ClientToScreen(nint window, ref Point point);
    [DllImport("user32.dll")] internal static extern bool ScreenToClient(nint window, ref Point point);
    [DllImport("user32.dll")] internal static extern nint ChildWindowFromPointEx(nint window, Point point, uint flags);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int GetWindowText(nint window, StringBuilder text, int max);
    internal delegate bool EnumProc(nint window, nint data);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, nint data);
    [DllImport("user32.dll", EntryPoint="PostMessageW", SetLastError=true)] internal static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError=true)] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] internal static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int metric);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW")] internal static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern nint FindWindowEx(nint parent, nint after, string? className, string? title);
    internal static string Title(nint window) { var s=new StringBuilder(1024); GetWindowText(window,s,s.Capacity); return s.ToString(); }
    internal static nint Coordinates(int x,int y)
    {
        if (x<short.MinValue || x>short.MaxValue || y<short.MinValue || y>short.MaxValue) throw new ArgumentOutOfRangeException(nameof(x), "Window-message coordinates must fit signed 16-bit values.");
        return (nint)((y & 0xffff)<<16 | (x & 0xffff));
    }
    internal static void Post(nint hwnd, uint message, nint w=default, nint l=default)
    { if (!PostMessage(hwnd,message,w,l)) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    internal static List<Target> Windows(bool visibleOnly=true)
    {
        var result=new List<Target>();
        EnumWindows((hwnd,_)=> {
            if (visibleOnly && !IsWindowVisible(hwnd)) return true;
            string title=Title(hwnd); if (title.Length==0) return true;
            GetWindowThreadProcessId(hwnd,out uint pid);
            try { result.Add(new Target(hwnd,pid,Process.GetProcessById((int)pid).ProcessName,title)); } catch (ArgumentException) { }
            return true;
        },0);
        return result;
    }
}

internal sealed record Target(nint Handle,uint Pid,string Process,string Title)
{
    public override string ToString()=> $"{Title} — {Process} • PID {Pid} • 0x{Handle:X}";
    internal void Validate()
    {
        Native.GetWindowThreadProcessId(Handle,out uint pid);
        if (!Native.IsWindow(Handle) || pid!=Pid) throw new InvalidOperationException("Target closed or changed. Refresh and select it again.");
        if (Native.IsIconic(Handle)) throw new InvalidOperationException("Restore the target before probing; minimized-window support is not assumed.");
    }
}
