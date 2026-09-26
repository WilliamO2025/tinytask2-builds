using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;

int passed=0;
void Check(bool value,string text){if(!value)throw new Exception(text);passed++;Console.WriteLine("PASS "+text);}
string prefix="TEST-"+Guid.NewGuid().ToString("N")[..8];
await using var a=new Client(prefix+"A");await using var b=new Client(prefix+"B");await using var c=new Client(prefix+"C");
await a.Connect();await b.Connect();await c.Connect();
Check((await a.Wait("welcome")).GetProperty("protocol").GetInt32()==1,"protocol handshake");await b.Wait("welcome");await c.Wait("welcome");
await using(var conflict=new Client(a.PublicId)){await conflict.Connect();Check((await conflict.Wait("error")).GetProperty("message").GetString()!.Contains("already in use"),"public ID uniqueness");}
await using(var impersonator=new Client(a.PublicId,a.Id)){await impersonator.Connect();Check((await impersonator.Wait("error")).GetProperty("message").GetString()!.Contains("credential"),"installation impersonation rejected");}
await a.Send("invite",new(){["publicId"]=b.PublicId});var invite=await b.Wait("invitation");
Check(invite.GetProperty("from").GetString()==a.PublicId,"invitation identifies sender");
await b.Send("invite",new(){["publicId"]=a.PublicId});Check((await b.Wait("invitePending")).GetProperty("inviteId").GetGuid()==invite.GetProperty("inviteId").GetGuid(),"crossed invites preserve first sender");
await b.Send("accept",new(){["inviteId"]=invite.GetProperty("inviteId").GetGuid()});
var state=await b.Wait("state");Guid room=state.GetProperty("sessionId").GetGuid();a.Room=b.Room=room;
Check(state.GetProperty("host").GetGuid()==a.Id&&state.GetProperty("members").GetArrayLength()==2,"approval creates one room with correct Host");
string code=state.GetProperty("code").GetString()!;
await b.Send("settings",new(){["locked"]=true,["everyone"]=true,["requireReady"]=false});Check((await b.Wait("error")).GetProperty("message").GetString()!.Contains("Host"),"member cannot change host settings");
await a.Send("settings",new(){["locked"]=true,["everyone"]=false,["requireReady"]=true});
await c.Send("join",new(){["code"]=code});Check((await c.Wait("error")).GetProperty("message").GetString()!.Contains("locked"),"locked code cannot join");
await a.Send("settings",new(){["locked"]=false,["everyone"]=false,["requireReady"]=true});
await c.Send("join",new(){["code"]=code});var request=await a.Wait("invitation");Check(request.GetProperty("joinRequest").GetBoolean(),"code sends approval request, not automatic membership");
await b.Send("accept",new(){["inviteId"]=request.GetProperty("inviteId").GetGuid()});Check((await b.Wait("error")).GetProperty("message").GetString()!.Contains("invalid"),"wrong recipient cannot accept join request");
await a.Send("accept",new(){["inviteId"]=request.GetProperty("inviteId").GetGuid()});state=await c.Wait("state");c.Room=room;
Check(state.GetProperty("members").GetArrayLength()==3,"host-approved code join");
await c.Send("stop");Check((await c.Wait("error")).GetProperty("message").GetString()!.Contains("Host"),"member cannot stop whole session in Host Only mode");
await a.Send("start");Check((await a.Wait("error")).GetProperty("message").GetString()!.Contains("Ready"),"start requires prepared members");
foreach(var client in new[]{a,b,c}){
 await client.Send("quality",new(){["rtt"]=client==c?0.4:0.01,["jitter"]=0.002,["synced"]=true});
 await client.Send("ready",new(){["task"]="Local test",["ready"]=true,["prepared"]=true});
 await client.Wait("ack");
}
// Wait for c's ready snapshot on the Host, avoiding an assumption about cross-socket arrival order.
await a.WaitState(s=>s.GetProperty("members").EnumerateArray().All(m=>m.GetProperty("ready").GetBoolean()));
await a.Send("start");var startA=await a.Wait("start");var startB=await b.Wait("start");
Check(startA.GetProperty("target").GetDouble()==startB.GetProperty("target").GetDouble(),"host and member receive identical execution timestamp");
Check(startA.GetProperty("participants").GetArrayLength()==2,"Fast Start excludes unstable member");
await c.Wait("excluded");Check(true,"excluded member explicitly notified");
await a.Send("start");Check((await a.Wait("error")).GetProperty("message").GetString()!.Contains("already playing"),"start while running rejected");
await a.Send("finished",new(){["runSequence"]=startA.GetProperty("sequence").GetInt64()});
await b.Send("finished",new(){["runSequence"]=startA.GetProperty("sequence").GetInt64()});
await a.WaitState(s=>s.GetProperty("reason").GetString()=="finished");Check(true,"playback completes only after selected participants finish");
await b.Raw(new{type="stop",sessionId=room,requestId=Guid.NewGuid(),commandSequence=1});Check((await b.Wait("error")).GetProperty("message").GetString()!.Contains("out-of-order"),"old client command sequence rejected");
await b.Send("leave",new(){["sessionId"]=Guid.NewGuid()});Check((await b.Wait("error")).GetProperty("message").GetString()!.Contains("Wrong session"),"wrong-session command rejected");
await a.Send("transfer",new(){["memberId"]=b.Id});state=await b.WaitState(s=>s.GetProperty("host").GetGuid()==b.Id);Check(state.GetProperty("host").GetGuid()==b.Id,"host transfer");
await a.Send("kick",new(){["memberId"]=c.Id});Check((await a.Wait("error")).GetProperty("message").GetString()!.Contains("Host"),"former host loses permissions immediately");
await b.Send("kick",new(){["memberId"]=c.Id});await c.Wait("left");Check(true,"host can remove member");
await b.Send("leave");await b.Wait("left");state=await a.WaitState(s=>s.GetProperty("host").GetGuid()==a.Id&&s.GetProperty("members").GetArrayLength()==1);Check(true,"deterministic replacement host");
await a.Send("leave");await a.Wait("left");c.Room=null;
await c.Send("join",new(){["code"]=code});Check((await c.Wait("error")).GetProperty("message").GetString()!.Contains("expired"),"last member leaving expires code");
await c.Raw(new{type="ping",sent=123.25});Check((await c.Wait("pong")).GetProperty("echo").GetDouble()==123.25,"clock sampling echo");
Console.WriteLine($"{passed} session checks passed");

sealed class Client(string publicId,Guid? identity=null):IAsyncDisposable
{
    internal Guid Id=identity??Guid.NewGuid();internal string PublicId=publicId;internal Guid? Room;
    private long sequence;
    private readonly ClientWebSocket socket=new();private readonly string secret=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    internal async Task Connect(){await socket.ConnectAsync(new Uri("ws://127.0.0.1:18763/session"),CancellationToken.None);await Raw(new{type="hello",protocol=1,deviceId=Id,secret,username=PublicId,publicId=PublicId});}
    internal Task Send(string type,Dictionary<string,object?>? values=null){values??=new();values["type"]=type;values["requestId"]=Guid.NewGuid();values["commandSequence"]=++sequence;if(Room!=null&&!values.ContainsKey("sessionId"))values["sessionId"]=Room;return Raw(values);}
    internal Task Raw(object message)=>socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message),WebSocketMessageType.Text,true,CancellationToken.None);
    internal async Task<JsonElement> Wait(string type){using var timeout=new CancellationTokenSource(5000);while(true){var bytes=new byte[16384];int count=0;WebSocketReceiveResult result;do{result=await socket.ReceiveAsync(new ArraySegment<byte>(bytes,count,bytes.Length-count),timeout.Token);count+=result.Count;if(result.MessageType!=WebSocketMessageType.Text)throw new Exception("Unexpected socket closure waiting for "+type);}while(!result.EndOfMessage);using var doc=JsonDocument.Parse(bytes.AsMemory(0,count));if(doc.RootElement.GetProperty("type").GetString()==type)return doc.RootElement.Clone();}}
    internal async Task<JsonElement> WaitState(Func<JsonElement,bool> match){for(int i=0;i<20;i++){var state=await Wait("state");if(match(state))return state;}throw new Exception("Expected state was not delivered.");}
    public ValueTask DisposeAsync(){socket.Abort();socket.Dispose();return ValueTask.CompletedTask;}
}
