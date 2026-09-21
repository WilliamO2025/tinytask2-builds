using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace TinyTask;

internal static class Setup
{
    private static string InstallDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs",AppIdentity.InstallFolder);
    private static string InstalledExe=>Path.Combine(InstallDirectory,AppIdentity.ExecutableName);
    private static string RegistryPath=>@"Software\Microsoft\Windows\CurrentVersion\Uninstall\"+AppIdentity.ProductGuid;
    private static string StartShortcut=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),AppIdentity.DisplayName+".lnk");
    private static string DesktopShortcut=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),AppIdentity.DisplayName+".lnk");
    internal static int Install(bool unattended=false)
    {
        if(!unattended && Forms.MessageBox.Show("Install "+AppIdentity.DisplayName+" 0.2 for this user?\n\nIncludes its runtime and creates Start Menu and desktop shortcuts. No drivers are installed.\n\nLocation: "+InstallDirectory,"TinyTask 2.0 setup",Forms.MessageBoxButtons.YesNo,Forms.MessageBoxIcon.Question)!=Forms.DialogResult.Yes)return 0;
        try
        {
            Directory.CreateDirectory(InstallDirectory);File.Copy(AppIdentity.ExecutablePath,InstalledExe,true);
            Shortcut(StartShortcut);Shortcut(DesktopShortcut);
            using var key=Registry.CurrentUser.CreateSubKey(RegistryPath);
            key.SetValue("DisplayName",AppIdentity.DisplayName);key.SetValue("DisplayVersion","0.2.0");key.SetValue("Publisher","TinyTask 2.0 project");key.SetValue("DisplayIcon",InstalledExe);
            key.SetValue("InstallLocation",InstallDirectory);key.SetValue("UninstallString",$"\"{InstalledExe}\" --uninstall");key.SetValue("NoModify",1);key.SetValue("NoRepair",1);
            key.SetValue("AppUserModelID",AppIdentity.AppId);key.SetValue("ProductGuid",AppIdentity.ProductGuid);
            if(unattended)return 0;
            if(Forms.MessageBox.Show("Installed. Open "+AppIdentity.DisplayName+" now?","TinyTask setup",Forms.MessageBoxButtons.YesNo)==Forms.DialogResult.Yes)Process.Start(new ProcessStartInfo(InstalledExe){UseShellExecute=true});return 0;
        }
        catch(Exception e) {Forms.MessageBox.Show("Installation could not finish. Close any running "+AppIdentity.DisplayName+" and retry.\n\n"+e.Message,"TinyTask setup");return 1;}
    }
    private static void Shortcut(string path)
    {
        object shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;object? shortcut=null;
        try {dynamic automation=shell;shortcut=automation.CreateShortcut(path);dynamic link=shortcut;link.TargetPath=InstalledExe;link.WorkingDirectory=InstallDirectory;link.IconLocation=InstalledExe+",0";link.Description=AppIdentity.DisplayName;link.Save();}
        finally {if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);Marshal.FinalReleaseComObject(shell);}
        AppIdentity.SetShortcutId(path);
    }
    internal static int Uninstall()
    {
        if(Forms.MessageBox.Show("Remove "+AppIdentity.DisplayName+" and its shortcuts? Saved macros, settings and reports will be kept.","Uninstall TinyTask",Forms.MessageBoxButtons.YesNo)!=Forms.DialogResult.Yes)return 0;
        try
        {
            string helper=Path.Combine(Path.GetTempPath(),"TinyTask-uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(AppIdentity.ExecutablePath,helper);
            Process.Start(new ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--finish-uninstall",Environment.ProcessId.ToString()}});return 0;
        }
        catch(Exception e){Forms.MessageBox.Show(e.Message,"Uninstall failed");return 1;}
    }
    internal static int FinishUninstall(string pid)
    {
        try
        {
            try{using var parent=Process.GetProcessById(int.Parse(pid));if(!parent.WaitForExit(10000))throw new IOException("Close "+AppIdentity.DisplayName+" before uninstalling.");}catch(ArgumentException){}
            // Delete only known files, never recursively remove a computed directory.
            File.Delete(InstalledExe);File.Delete(StartShortcut);File.Delete(DesktopShortcut);
            if(Directory.Exists(InstallDirectory) && Directory.GetFileSystemEntries(InstallDirectory).Length==0)Directory.Delete(InstallDirectory);
            Registry.CurrentUser.DeleteSubKeyTree(RegistryPath,false);
            Forms.MessageBox.Show(AppIdentity.DisplayName+" was removed. Settings were kept. The temporary uninstall helper can be removed by Windows temporary-file cleanup.","TinyTask");return 0;
        }
        catch(Exception e){Forms.MessageBox.Show("Uninstall could not finish: "+e.Message,"TinyTask");return 1;}
    }
}
