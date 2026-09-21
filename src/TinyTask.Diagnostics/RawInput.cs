using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace TinyTask;

internal sealed record Device(nint Handle,uint Type,string Path)
{
    public override string ToString()=> $"{(Type==0?"Mouse":Type==1?"Keyboard":"HID")} • 0x{Handle:X} • {Path}";
}
internal sealed record RawSample(nint Device,uint Type,int X,int Y,ushort Flags,ushort Buttons,ushort Key);

internal sealed class RawInput : IDisposable
{
    private readonly nint window;
    private readonly nint buffer=Marshal.AllocHGlobal(4096);
    private bool disposed;
    public bool Enabled { get; private set; }
    public RawInput(nint hwnd) { window=hwnd; }
    public void Start()
    {
        var regs=new[] { new Native.Registration {Page=1,Usage=2,Flags=0x2100,Target=window}, new Native.Registration {Page=1,Usage=6,Flags=0x2100,Target=window} };
        if (!Native.RegisterRawInputDevices(regs,2,(uint)Marshal.SizeOf<Native.Registration>())) throw new Win32Exception(Marshal.GetLastWin32Error());
        Enabled=true;
    }
    public void Stop()
    {
        if (!Enabled) return;
        var regs=new[] { new Native.Registration {Page=1,Usage=2,Flags=1},new Native.Registration {Page=1,Usage=6,Flags=1} };
        if (!Native.RegisterRawInputDevices(regs,2,(uint)Marshal.SizeOf<Native.Registration>())) throw new Win32Exception(Marshal.GetLastWin32Error());
        Enabled=false;
    }
    public static List<Device> Devices()
    {
        // Hot-plug can change the count between calls; retry with a fresh count.
        for (int attempt=0;attempt<3;attempt++)
        {
            uint count=0; uint size=(uint)Marshal.SizeOf<Native.DeviceEntry>();
            if (Native.GetRawInputDeviceList(null,ref count,size)==uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
            var entries=new Native.DeviceEntry[count];
            uint actual=Native.GetRawInputDeviceList(entries,ref count,size);
            if (actual==uint.MaxValue) continue;
            var result=new List<Device>();
            for(int i=0;i<actual;i++)
            {
                uint length=0;
                Native.GetRawInputDeviceInfo(entries[i].Handle,0x20000007,null,ref length);
                var name=new StringBuilder((int)Math.Min(length+1,32768));
                if(length>0 && length<32768) Native.GetRawInputDeviceInfo(entries[i].Handle,0x20000007,name,ref length);
                result.Add(new Device(entries[i].Handle,entries[i].Type,name.ToString()));
            }
            return result;
        }
        throw new InvalidOperationException("Device list changed repeatedly. Refresh devices.");
    }
    public RawSample? Read(nint handle)
    {
        uint length=4096, headerSize=(uint)Marshal.SizeOf<Native.RawHeader>();
        uint read=Native.GetRawInputData(handle,0x10000003,buffer,ref length,headerSize);
        if (read==uint.MaxValue || read<headerSize) return null;
        var header=Marshal.PtrToStructure<Native.RawHeader>(buffer);
        nint data=buffer+(int)headerSize;
        if(header.Type==0 && read>=headerSize+24)
            return new RawSample(header.Device,0,Marshal.ReadInt32(data,12),Marshal.ReadInt32(data,16),(ushort)Marshal.ReadInt16(data), (ushort)Marshal.ReadInt16(data,4),0);
        if(header.Type==1 && read>=headerSize+16)
            return new RawSample(header.Device,1,0,0,(ushort)Marshal.ReadInt16(data,2),0,(ushort)Marshal.ReadInt16(data,6));
        return null;
    }
    public void Dispose()
    {
        if(disposed) return;
        try { Stop(); } finally { Marshal.FreeHGlobal(buffer); disposed=true; }
    }
}
