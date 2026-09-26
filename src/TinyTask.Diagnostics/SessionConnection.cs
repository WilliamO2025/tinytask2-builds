using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TinyTask;

internal sealed class SessionIdentity
{
    public Guid DeviceId {get;set;}=Guid.NewGuid();
    public string ProtectedSecret {get;set;}="";
    public string Username {get;set;}="";
    public string PublicId {get;set;}="TT-"+Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
    public string Endpoint {get;set;}="wss://";
    internal string Secret=>Convert.ToBase64String(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedSecret),null,DataProtectionScope.CurrentUser));
    internal string CredentialFor(Uri endpoint)
    {
        byte[] root=Convert.FromBase64String(Secret);
        try{return Convert.ToBase64String(HMACSHA256.HashData(root,Encoding.UTF8.GetBytes(endpoint.GetLeftPart(UriPartial.Authority).ToLowerInvariant())));}
        finally{CryptographicOperations.ZeroMemory(root);}
    }
    internal static SessionIdentity Load(string path)
    {
        if(File.Exists(path))return JsonSerializer.Deserialize<SessionIdentity>(File.ReadAllText(path))??throw new InvalidDataException("Could not read the saved installation identity.");
        var identity=new SessionIdentity{ProtectedSecret=Convert.ToBase64String(ProtectedData.Protect(RandomNumberGenerator.GetBytes(32),null,DataProtectionScope.CurrentUser))};identity.Save(path);return identity;
    }
    internal void Save(string path){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this));File.Move(path+".tmp",path,true);}
}

internal sealed class SessionConnection : IAsyncDisposable
{
    private readonly ClientWebSocket socket=new();
    private readonly SemaphoreSlim send=new(1,1);
    private readonly CancellationTokenSource lifetime=new();
    private Task? receiveTask,pingTask;
    private long commandSequence;
    private readonly Queue<(double Rtt,double Offset)> samples=new();
    internal event Action<JsonElement>? Message;
    internal event Action<string>? Disconnected;
    internal Guid? Room {get;private set;}
    internal long Sequence {get;private set;}
    internal double Offset {get;private set;}
    internal double Ping {get;private set;}
    internal double Jitter {get;private set;}
    internal bool Synced {get;private set;}
    internal static double Now=>(double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
    internal async Task Connect(SessionIdentity identity)
    {
        if(!Uri.TryCreate(identity.Endpoint,UriKind.Absolute,out var endpoint)||(endpoint.Scheme!="wss"&&!(endpoint.Scheme=="ws"&&endpoint.IsLoopback))||endpoint.AbsolutePath!="/session")throw new ArgumentException("Use a secure wss:// session server ending in /session. Local tests may use ws://localhost:port/session.");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await socket.ConnectAsync(endpoint,timeout.Token);
        await Send(new{type="hello",protocol=1,deviceId=identity.DeviceId,secret=identity.CredentialFor(endpoint),username=identity.Username,publicId=identity.PublicId});
        receiveTask=Receive();pingTask=PingLoop();
    }
    internal async Task Command(string type,Dictionary<string,object?>? fields=null)
    {
        var message=fields==null?new Dictionary<string,object?>():new Dictionary<string,object?>(fields);message["type"]=type;message["requestId"]=Guid.NewGuid();if(Room!=null)message["sessionId"]=Room.Value;
        await Send(message,true);
    }
    private async Task Send(object value,bool command=false)
    {
        await send.WaitAsync(lifetime.Token);
        try{if(command)((Dictionary<string,object?>)value)["commandSequence"]=++commandSequence;if(socket.State!=WebSocketState.Open)throw new InvalidOperationException("Session is not connected.");await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(value),WebSocketMessageType.Text,true,lifetime.Token);}
        finally{send.Release();}
    }
    private async Task PingLoop()
    {
        try{while(!lifetime.IsCancellationRequested){await Send(new{type="ping",sent=Now});await Task.Delay(2000,lifetime.Token);}}
        catch(Exception e)when(e is OperationCanceledException or WebSocketException or InvalidOperationException){if(!lifetime.IsCancellationRequested){Disconnected?.Invoke(e.Message);lifetime.Cancel();socket.Abort();}}
    }
    private async Task Receive()
    {
        try{
            var buffer=new byte[16384];
            while(!lifetime.IsCancellationRequested){
                int count=0;WebSocketReceiveResult result;
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(TimeSpan.FromSeconds(15));
                do{result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer,count,buffer.Length-count),timeout.Token);count+=result.Count;if(result.MessageType!=WebSocketMessageType.Text||(!result.EndOfMessage&&count==buffer.Length))throw new InvalidDataException("Invalid session message.");}while(!result.EndOfMessage);
                using var document=JsonDocument.Parse(buffer.AsMemory(0,count));var message=document.RootElement.Clone();string? type=message.GetProperty("type").GetString();
                if(type=="pong"){
                    double received=Now,sent=message.GetProperty("echo").GetDouble(),rtt=received-sent;
                    if(!double.IsFinite(rtt)||rtt<0||rtt>10)continue;
                    samples.Enqueue((rtt,message.GetProperty("serverTime").GetDouble()-(sent+received)/2));if(samples.Count>16)samples.Dequeue();
                    var best=samples.MinBy(s=>s.Rtt);Offset=best.Offset;Ping=rtt*1000;Jitter=(samples.Max(s=>s.Rtt)-samples.Min(s=>s.Rtt))*1000;Synced=samples.Count>=3&&double.IsFinite(Offset)&&rtt<=0.2&&Jitter<=50;
                    await Command("quality",new(){["rtt"]=rtt,["jitter"]=Jitter/1000,["synced"]=Synced});
                }
                if(type=="state"){
                    var room=message.GetProperty("sessionId").GetGuid();long sequence=message.GetProperty("sequence").GetInt64();
                    if((Room!=null&&Room!=room)||(Room==room&&sequence<=Sequence))continue;Room=room;Sequence=sequence;
                }
                else if(type is "stop" or "start"){
                    if(Room!=message.GetProperty("sessionId").GetGuid()||message.GetProperty("sequence").GetInt64()<=Sequence)continue;
                    Sequence=message.GetProperty("sequence").GetInt64();
                }
                else if(type=="left"){Room=null;Sequence=0;}
                Message?.Invoke(message);
            }
        }catch(Exception e)when(e is OperationCanceledException or WebSocketException or JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException){if(!lifetime.IsCancellationRequested)Disconnected?.Invoke(e.Message);}
        finally{Synced=false;lifetime.Cancel();socket.Abort();}
    }
    public async ValueTask DisposeAsync(){lifetime.Cancel();socket.Abort();if(receiveTask!=null)await receiveTask;if(pingTask!=null)await pingTask;socket.Dispose();lifetime.Dispose();send.Dispose();}
}
