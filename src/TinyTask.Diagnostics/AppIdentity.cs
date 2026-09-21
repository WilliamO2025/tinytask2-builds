using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TinyTask;

internal static class AppIdentity
{
#if INPUT_LAB
    internal const bool IsLab=true;
    internal const string AppId="TinyTask2.InputLab.Desktop";
    internal const string ProductGuid="{DEB0B44A-BD91-4FD5-823D-0350BE63D191}";
    internal const string ExecutableName="TinyTask2-InputLab.exe";
    internal const string DisplayName="TinyTask 2.0 Input Lab";
    internal const string InstallFolder="TinyTask2-InputLab";
    internal const string DataFolder="InputLab";
#else
    internal const bool IsLab=false;
    internal const string AppId="TinyTask2.Classic.Desktop";
    internal const string ProductGuid="{A7EEA785-D560-48E3-A155-844B5642E13C}";
    internal const string ExecutableName="TinyTask2.exe";
    internal const string DisplayName="TinyTask 2.0";
    internal const string InstallFolder="TinyTask2";
    internal const string DataFolder="User";
#endif
    internal static string ExecutablePath=>Path.GetFullPath(Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable."));
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string id);
    internal static void Register()=>Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(AppId));

    // Set the shortcut's explicit identity to match the process taskbar identity.
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=false)] private static extern void SHGetPropertyStoreFromParsingName(string path,nint context,uint flags,ref Guid iid,out IPropertyStore store);
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey {public Guid Format;public uint Id;}
    [StructLayout(LayoutKind.Explicit,Size=24)] private struct PropVariant {[FieldOffset(0)]public ushort Type;[FieldOffset(8)]public nint Pointer;}
    [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index,out PropertyKey key);
        void GetValue(ref PropertyKey key,out PropVariant value);
        void SetValue(ref PropertyKey key,ref PropVariant value);
        void Commit();
    }
    internal static void SetShortcutId(string path)
    {
        var iid=typeof(IPropertyStore).GUID;
        SHGetPropertyStoreFromParsingName(path,0,2,ref iid,out var store);
        var key=new PropertyKey{Format=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),Id=5};
        var value=new PropVariant{Type=31,Pointer=Marshal.StringToCoTaskMemUni(AppId)};
        try {store.SetValue(ref key,ref value);store.Commit();}
        finally {Marshal.FreeCoTaskMem(value.Pointer);Marshal.FinalReleaseComObject(store);}
    }
}
