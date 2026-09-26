using System;
using System.Globalization;
using System.Text.Json.Nodes;

namespace TinyTask;

internal sealed record PlaybackSettings(double Speed=1,int Loops=1,bool Continuous=false)
{
    internal static bool IsNumericText(string text)
    {
        int dots=0;
        foreach(char c in text)if(c=='.'){if(++dots>1)return false;}else if(c<'0'||c>'9')return false;
        return true;
    }
    internal static double ParseSpeed(string text)
    {
        if(!IsNumericText(text)||!double.TryParse(text,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out double speed)||!double.IsFinite(speed)||speed<0.01||speed>1000)
            throw new ArgumentException("Speed must be a number between 0.01 and 1000.");
        return speed;
    }
    internal static PlaybackSettings? Read(string json)
    {
        var root=JsonNode.Parse(json) as JsonObject??throw new ArgumentException("Invalid macro.");
        if(root["playback"] is not JsonObject profile)return null;
        double speed=profile["speed"]?.GetValue<double>()??1;
        int loops=profile["loops"]?.GetValue<int>()??1;
        if(!double.IsFinite(speed)||speed<0.01||speed>1000||loops<1||loops>1000000)throw new ArgumentException("Invalid saved playback settings.");
        return new(speed,loops,profile["continuous"]?.GetValue<bool>()??false);
    }
    internal string Write(string json,string? name=null)
    {
        _=ParseSpeed(Speed.ToString("0.################",CultureInfo.InvariantCulture));
        if(Loops<1||Loops>1000000)throw new ArgumentException("Loops must be between 1 and 1,000,000.");
        var root=JsonNode.Parse(json) as JsonObject??throw new ArgumentException("Invalid macro.");
        var profile=root["playback"] as JsonObject;
        if(profile==null){profile=new JsonObject();root["playback"]=profile;}
        profile["speed"]=Speed;profile["loops"]=Loops;profile["continuous"]=Continuous;
        if(name!=null)root["name"]=name;
        return root.ToJsonString(MacroDocument.Json);
    }
}
