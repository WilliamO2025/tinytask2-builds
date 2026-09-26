using System.Net.WebSockets;
using System.Text.Json;
using System.Security.Cryptography;
using System.Diagnostics;
using var socket=new ClientWebSocket();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(65));
await socket.ConnectAsync(new Uri("ws://127.0.0.1:18763/session"),timeout.Token);
long sequence=0;string room=null;bool started=false;double Now()=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
async Task Send(object value)=>await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(value),WebSocketMessageType.Text,true,timeout.Token);
async Task Command(string type,Dictionary<string,object> fields){fields["type"]=type;fields["requestId"]=Guid.NewGuid();fields["commandSequence"]=++sequence;if(room!=null)fields["sessionId"]=room;await Send(fields);}
await Send(new{type="hello",protocol=1,deviceId=Guid.NewGuid(),secret=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),username="Windows protocol peer",publicId="WINDOWS-TEST"});
while(true){
 var bytes=new byte[16384];int size=0;WebSocketReceiveResult result;
 do{
  if(size==bytes.Length)throw new InvalidDataException("Session message exceeds 16KB");
  var receive=socket.ReceiveAsync(new ArraySegment<byte>(bytes,size,bytes.Length-size),timeout.Token);
  while(!receive.IsCompleted){if(await Task.WhenAny(receive,Task.Delay(2000,timeout.Token))!=receive)await Send(new{type="ping",sent=Now()});}
  result=await receive;size+=result.Count;
 }while(!result.EndOfMessage);
 using var doc=JsonDocument.Parse(bytes.AsMemory(0,size));var packet=doc.RootElement;string type=packet.GetProperty("type").GetString();
 if(type=="error")throw new Exception(packet.GetProperty("message").GetString());
 if(type=="invitation")await Command("accept",new(){["inviteId"]=packet.GetProperty("inviteId").GetString()});
 if(type=="state"){
  room=packet.GetProperty("sessionId").GetString();
  if(packet.GetProperty("reason").GetString()=="joined"){
   await Command("quality",new(){["rtt"]=0.01,["jitter"]=0.001,["synced"]=true});
   await Command("ready",new(){["ready"]=true,["prepared"]=true,["task"]="Windows fixture (no input)"});
  }
 }
 if(type=="start"){
  if(packet.GetProperty("participants").GetArrayLength()!=2)throw new Exception("Both platforms must participate");
  double target=packet.GetProperty("target").GetDouble();if(!double.IsFinite(target))throw new Exception("Invalid common target");
  started=true;Console.WriteLine("PASS Windows protocol peer received Mac-hosted synchronized start");
  await Command("finished",new(){["runSequence"]=packet.GetProperty("sequence").GetInt64()});
 }
 if(type=="stop"&&started){Console.WriteLine("PASS Windows protocol peer received Mac session stop");return;}
}
