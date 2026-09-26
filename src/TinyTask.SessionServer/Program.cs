using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TinyTask.Sessions;

var builder=WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=16384);
builder.Services.AddRateLimiter(o=>{
    o.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
    o.RejectionStatusCode=429;
});
var app=builder.Build();app.UseRateLimiter();app.UseWebSockets();
var state=new SessionState(Path.Combine(builder.Configuration["DataDirectory"]??Path.Combine(AppContext.BaseDirectory,"data"),"devices.json"));
app.MapGet("/health",()=>new{protocol=1,status="ready"});
app.Map("/session",async context=>{
    // Cleartext is allowed only on loopback for local development/tests.
    if(!context.Request.IsHttps && (context.Connection.RemoteIpAddress==null||!IPAddress.IsLoopback(context.Connection.RemoteIpAddress))){context.Response.StatusCode=403;return;}
    if(!context.WebSockets.IsWebSocketRequest){context.Response.StatusCode=400;return;}
    using var socket=await context.WebSockets.AcceptWebSocketAsync();
    using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    lifetime.CancelAfter(TimeSpan.FromSeconds(10));
    Peer? peer=null;Task? writer=null;
    try{
        var hello=await Receive(socket,lifetime.Token);
        peer=state.Connect(hello,socket);
        lifetime.CancelAfter(Timeout.InfiniteTimeSpan);
        writer=Task.Run(async()=>{await foreach(var packet in peer.Outbound.Reader.ReadAllAsync(lifetime.Token))await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(packet),WebSocketMessageType.Text,true,lifetime.Token);},lifetime.Token);
        int count=0;double window=SessionState.Now;
        while(socket.State==WebSocketState.Open){
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var command=await Receive(socket,timeout.Token);
            if(SessionState.Now-window>=1){window=SessionState.Now;count=0;}
            if(++count>30)throw new InvalidOperationException("Too many session commands.");
            state.Handle(peer,command);
        }
    }catch(Exception e)when(e is WebSocketException or OperationCanceledException or JsonException or InvalidOperationException or ArgumentException){
        if(peer==null && socket.State==WebSocketState.Open){using var timeout=new CancellationTokenSource(1000);try{await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new{type="error",message=e is OperationCanceledException?"Connection timed out.":e.Message}),WebSocketMessageType.Text,true,timeout.Token);}catch(WebSocketException){}catch(OperationCanceledException){}}
    }finally{
        if(peer!=null)state.Disconnect(peer);
        lifetime.Cancel();socket.Abort();
        if(writer!=null)try{await writer;}catch(OperationCanceledException){}catch(WebSocketException){}
    }
});
await app.RunAsync();

static async Task<JsonElement> Receive(WebSocket socket,CancellationToken token)
{
    var bytes=new byte[16384];int length=0;
    while(true){var received=await socket.ReceiveAsync(new ArraySegment<byte>(bytes,length,bytes.Length-length),token);
        if(received.MessageType!=WebSocketMessageType.Text)throw new InvalidOperationException("Only JSON session messages are accepted.");
        length+=received.Count;if(received.EndOfMessage)break;if(length==bytes.Length)throw new InvalidOperationException("Session message is too large.");}
    using var document=JsonDocument.Parse(bytes.AsMemory(0,length));return document.RootElement.Clone();
}
