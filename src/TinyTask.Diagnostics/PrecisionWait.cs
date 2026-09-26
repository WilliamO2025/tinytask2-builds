using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace TinyTask;

// One unnamed timer per playback, not one native allocation per action.
internal sealed class PrecisionWait : IDisposable
{
    private readonly EventWaitHandle timer=new(false,EventResetMode.AutoReset);
    internal bool HighResolution {get;}
    internal PrecisionWait()
    {
        nint handle=CreateWaitableTimerExW(0,null,2,0x00100002);
        HighResolution=handle!=0;
        if(handle==0)handle=CreateWaitableTimerExW(0,null,0,0x00100002);
        if(handle==0){timer.Dispose();throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not create playback timer.");}
        var old=timer.SafeWaitHandle;timer.SafeWaitHandle=new SafeWaitHandle(handle,true);old.Dispose();
    }
    internal async Task Delay(double seconds,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(seconds<=0)return;
        long due=-Math.Max(1,(long)Math.Ceiling(Math.Min(seconds,60)*10_000_000));
        if(!SetWaitableTimerEx(timer.SafeWaitHandle,ref due,0,0,0,0,0))throw new Win32Exception(Marshal.GetLastWin32Error());
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration=ThreadPool.RegisterWaitForSingleObject(timer,(_,_)=>completion.TrySetResult(),null,Timeout.Infinite,true);
        using var cancel=token.Register(()=>completion.TrySetCanceled(token));
        try{await completion.Task;}
        finally{registration.Unregister(null);CancelWaitableTimer(timer.SafeWaitHandle);}
    }
    public void Dispose()=>timer.Dispose();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern nint CreateWaitableTimerExW(nint attributes,string? name,uint flags,uint access);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetWaitableTimerEx(SafeWaitHandle timer,ref long due,int period,nint callback,nint argument,nint context,uint tolerance);
    [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool CancelWaitableTimer(SafeWaitHandle timer);
}
