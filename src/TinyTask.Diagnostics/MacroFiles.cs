using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Runtime.InteropServices;
namespace TinyTask;

// Lossless gzip JSON: no event thinning or rewritten timestamps. Legacy JSON remains supported.
internal static class MacroFiles
{
    // Resource-based import guard, not a recording-duration/action cap. Reserve room
    // for UTF-16 JSON, parsing and the action model instead of trusting gzip metadata.
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    { public uint Length,Load;public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,Extended; }
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    private static long ImportCharacterBudget()
    {
        var memory=new MemoryStatus{Length=(uint)Marshal.SizeOf<MemoryStatus>()};
        ulong available=GlobalMemoryStatusEx(ref memory)?memory.AvailablePhysical:(ulong)Math.Max(0,GC.GetGCMemoryInfo().TotalAvailableMemoryBytes-GC.GetTotalMemory(false));
        return (long)Math.Min((ulong)(int.MaxValue-1),available/16);
    }
    internal static string Read(string path,long? characterBudget=null)
    {
        using var file=File.OpenRead(path);
        bool compressed=file.ReadByte()==0x1f && file.ReadByte()==0x8b;file.Position=0;
        using var gzip=compressed?new GZipStream(file,CompressionMode.Decompress):null;
        using var reader=new StreamReader((Stream?)gzip??file);
        var text=new StringBuilder();var buffer=new char[32768];long budget=characterBudget??ImportCharacterBudget();int count;
        while((count=reader.Read(buffer,0,buffer.Length))>0)
        {
            if(text.Length+(long)count>budget)throw new InvalidDataException("This macro needs more available memory to open safely. Close other applications and try again; the file has not been changed.");
            text.Append(buffer,0,count);
        }
        return text.ToString();
    }
    internal static void Write(string path,string json)
    {
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var file=File.Create(temporary))
            {
                if(path.EndsWith(".ttmacro",StringComparison.OrdinalIgnoreCase))
                {using var gzip=new GZipStream(file,CompressionLevel.Fastest,true);using var writer=new StreamWriter(gzip,new UTF8Encoding(false));writer.Write(json);}
                else {using var writer=new StreamWriter(file,new UTF8Encoding(false));writer.Write(json);}
            }
            File.Move(temporary,path,true);
        }
        finally {if(File.Exists(temporary))File.Delete(temporary);}
    }
}
