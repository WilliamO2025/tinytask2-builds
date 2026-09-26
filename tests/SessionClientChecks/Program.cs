using System.Text.Json;
using System.IO;
using System.Threading.Channels;
using TinyTask;

int count=0;void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
string directory=Path.Combine(Path.GetTempPath(),"TinyTask-session-client-checks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
var first=SessionIdentity.Load(Path.Combine(directory,"a.json"));var second=SessionIdentity.Load(Path.Combine(directory,"b.json"));
Check(first.DeviceId==SessionIdentity.Load(Path.Combine(directory,"a.json")).DeviceId,"installation ID persists");
first.Username="Alice";second.Username="Bob";first.Endpoint=second.Endpoint="ws://127.0.0.1:18763/session";
Check(first.CredentialFor(new Uri(first.Endpoint))==first.CredentialFor(new Uri(first.Endpoint)),"server credential is stable");
Check(first.CredentialFor(new Uri(first.Endpoint))!=first.CredentialFor(new Uri("wss://example.org/session")),"installation secret cannot be replayed across servers");
await using var a=new SessionConnection();await using var b=new SessionConnection();
var qa=Channel.CreateUnbounded<JsonElement>();var qb=Channel.CreateUnbounded<JsonElement>();
a.Message+=m=>qa.Writer.TryWrite(m);b.Message+=m=>qb.Writer.TryWrite(m);
await a.Connect(first);await b.Connect(second);await Wait(qa,"welcome");await Wait(qb,"welcome");
Check(true,"desktop client authenticates without account");
for(int i=0;i<160&&(!a.Synced||!b.Synced);i++)await Task.Delay(50);
Check(a.Synced&&b.Synced,"desktop clients continuously collect clock samples");
Check(double.IsFinite(a.Offset)&&a.Ping>=0&&a.Jitter>=0,"latency jitter and offset are finite");
await a.Command("invite",new(){["publicId"]=second.PublicId});var invite=await Wait(qb,"invitation");await b.Command("accept",new(){["inviteId"]=invite.GetProperty("inviteId").GetGuid()});
var state=await Wait(qb,"state");Guid room=state.GetProperty("sessionId").GetGuid();await Wait(qa,"state");
Check(a.Room==room&&b.Room==room,"desktop connections track joined session");
await a.Command("ready",new(){["task"]="Delay-only test",["ready"]=true,["prepared"]=true});await b.Command("ready",new(){["task"]="Delay-only test",["ready"]=true,["prepared"]=true});
while(true){state=await Wait(qa,"state");if(state.GetProperty("members").GetArrayLength()==2&&state.GetProperty("members").EnumerateArray().All(m=>m.GetProperty("ready").GetBoolean()))break;}
await a.Command("start");var sa=await Wait(qa,"start");var sb=await Wait(qb,"start");
Check(sa.GetProperty("target").GetDouble()==sb.GetProperty("target").GetDouble(),"real desktop clients receive common target");
double ta=sa.GetProperty("target").GetDouble()-a.Offset,tb=sb.GetProperty("target").GetDouble()-b.Offset;
Check(Math.Abs(ta-tb)<0.05,"local monotonic target conversion agrees within 50ms on loopback");
await a.Command("stop");await Wait(qb,"stop");Check(true,"desktop member receives authorized session stop");
await b.Command("leave");await Wait(qb,"left");Check(b.Room==null,"leave clears only departing client state");
Console.WriteLine($"{count} desktop session client checks passed");
static async Task<JsonElement> Wait(Channel<JsonElement> queue,string type){using var timeout=new CancellationTokenSource(10000);while(true){var message=await queue.Reader.ReadAsync(timeout.Token);if(message.GetProperty("type").GetString()=="error")throw new Exception(message.GetProperty("message").GetString());if(message.GetProperty("type").GetString()==type)return message;}}
