using System;
using System.IO;
using System.IO.Compression;
using System.Text;
namespace TinyTask;

// Lossless gzip JSON: no event thinning or rewritten timestamps. Legacy JSON remains supported.
internal static class MacroFiles
{
    internal static string Read(string path)
    {
        using var file=File.OpenRead(path);
        bool compressed=file.ReadByte()==0x1f && file.ReadByte()==0x8b;file.Position=0;
        if(!compressed){using var plain=new StreamReader(file);return plain.ReadToEnd();}
        using var gzip=new GZipStream(file,CompressionMode.Decompress);
        using var reader=new StreamReader(gzip);return reader.ReadToEnd();
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
