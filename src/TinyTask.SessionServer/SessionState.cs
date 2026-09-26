using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace TinyTask.Sessions;

internal sealed record Device(Guid Id,string SecretHash,string PublicId);
internal sealed class Peer(Guid id,string username,string publicId,WebSocket socket)
{
    internal Guid Id=id;internal string Username=username,PublicId=publicId;
    internal long LastCommand;
    internal Guid? Room;internal bool Ready;internal string Task="";
    internal double Rtt=10,Jitter=10,QualityAt;internal bool ClockReady;
    internal readonly Channel<object> Outbound=Channel.CreateBounded<object>(128);
    internal readonly HashSet<Guid> Requests=new();internal readonly Queue<Guid> RequestOrder=new();
    internal void Send(object value){if(!Outbound.Writer.TryWrite(value))socket.Abort();}
}
internal sealed class Room(Guid host)
{
    internal Guid Id=Guid.NewGuid(),Host=host;internal string Code="";
    internal bool Locked,Everyone,RequireReady=true,Playing,CancelIfSlow;
    internal long Sequence,RunSequence;
    internal HashSet<Guid> Active=new();
    internal List<Guid> Members=new(){host};
}
internal sealed record Invite(Guid Id,Guid From,Guid To,Guid? Room,bool Join,double Expires);

internal sealed class SessionState
{
    private readonly object gate=new();
    private readonly Dictionary<Guid,Device> devices;
    private readonly Dictionary<Guid,Peer> peers=new();
    private readonly Dictionary<Guid,Room> rooms=new();
    private readonly Dictionary<Guid,Invite> invites=new();
    private readonly string path;
    internal static double Now=>(double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
    internal SessionState(string path){this.path=path;devices=File.Exists(path)?JsonSerializer.Deserialize<Dictionary<Guid,Device>>(File.ReadAllText(path))??throw new InvalidOperationException("Invalid identity registry."):new();}
    internal Peer Connect(JsonElement hello,WebSocket socket)
    {
        lock(gate){
            if(Text(hello,"type")!="hello"||hello.GetProperty("protocol").GetInt32()!=1)throw new InvalidOperationException("Your TinyTask versions are not compatible for synchronized sessions.");
            var id=Guid.Parse(Text(hello,"deviceId"));if(id==Guid.Empty)throw new ArgumentException("Invalid installation identity.");
            byte[] secret=Convert.FromBase64String(Text(hello,"secret"));if(secret.Length!=32)throw new ArgumentException("Invalid installation credential.");
            string hash=Convert.ToHexString(SHA256.HashData(secret));
            if(devices.TryGetValue(id,out var existing)&&!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(existing.SecretHash),Convert.FromHexString(hash)))throw new InvalidOperationException("Installation credential rejected.");
            string username=Name(Text(hello,"username"));string publicId=Text(hello,"publicId").ToUpperInvariant();
            if(!Regex.IsMatch(publicId,"^[A-Z0-9][A-Z0-9_-]{2,31}$"))throw new ArgumentException("Public ID must contain 3-32 letters, numbers, underscores or hyphens.");
            if(devices.Values.Any(d=>d.Id!=id&&d.PublicId==publicId))throw new InvalidOperationException("Public ID is already in use.");
            if(peers.ContainsKey(id))throw new InvalidOperationException("This installation is already connected.");
            var next=new Dictionary<Guid,Device>(devices){[id]=new(id,hash,publicId)};
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string temporary=path+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(next));File.Move(temporary,path,true);
            devices[id]=next[id];var peer=new Peer(id,username,publicId,socket);peers.Add(id,peer);
            peer.Send(new{type="welcome",protocol=1,publicId,serverTime=Now});return peer;
        }
    }
    internal void Handle(Peer peer,JsonElement command)
    {
        lock(gate){
            try{
                if(!peers.TryGetValue(peer.Id,out var connected)||!ReferenceEquals(peer,connected))throw new InvalidOperationException("Connection expired.");
                string type=Text(command,"type");
                if(type=="ping"){peer.Send(new{type="pong",echo=command.GetProperty("sent").GetDouble(),serverTime=Now});return;}
                long sequence=command.GetProperty("commandSequence").GetInt64();if(sequence<=peer.LastCommand)throw new InvalidOperationException("Old or out-of-order command.");peer.LastCommand=sequence;
                Guid request=Guid.Parse(Text(command,"requestId"));if(request==Guid.Empty)throw new ArgumentException("Request ID required.");
                if(!peer.Requests.Add(request)){peer.Send(new{type="duplicate",requestId=request});return;}
                peer.RequestOrder.Enqueue(request);if(peer.RequestOrder.Count>256)peer.Requests.Remove(peer.RequestOrder.Dequeue());
                foreach(var expired in invites.Values.Where(i=>i.Expires<Now).ToArray())invites.Remove(expired.Id);
                switch(type){
                    case "create":if(peer.Room!=null)throw new InvalidOperationException("Leave your current session first.");Create(peer);break;
                    case "invite":{
                        var target=peers.Values.FirstOrDefault(p=>p.PublicId==Text(command,"publicId").ToUpperInvariant())??throw new InvalidOperationException("Public ID is offline or unavailable.");
                        if(target.Id==peer.Id||target.Room!=null)throw new InvalidOperationException("That device cannot be invited now.");
                        if(peer.Room!=null)RequireHost(peer,GetRoom(peer));
                        var pending=invites.Values.FirstOrDefault(i=>!i.Join&&((i.From==peer.Id&&i.To==target.Id)||(i.From==target.Id&&i.To==peer.Id)));
                        if(pending!=null){peer.Send(new{type="invitePending",inviteId=pending.Id});break;}
                        AddInvite(peer,target,peer.Room,false);break;
                    }
                    case "join":{
                        if(peer.Room!=null)throw new InvalidOperationException("Leave your current session first.");
                        var room=rooms.Values.FirstOrDefault(r=>r.Code==Text(command,"code"))??throw new InvalidOperationException("Session code is invalid or expired.");
                        if(room.Locked)throw new InvalidOperationException("Session is locked.");
                        AddInvite(peer,peers[room.Host],room.Id,true);break;
                    }
                    case "accept":case "decline":{
                        var inviteId=Guid.Parse(Text(command,"inviteId"));
                        if(!invites.TryGetValue(inviteId,out var invite)||invite.To!=peer.Id||invite.Expires<Now)throw new InvalidOperationException("Invitation is invalid or expired.");
                        invites.Remove(inviteId);
                        if(type=="decline"){if(peers.TryGetValue(invite.From,out var declined))declined.Send(new{type="declined",inviteId});break;}
                        if(!peers.TryGetValue(invite.From,out var from))throw new InvalidOperationException("Inviting device disconnected.");
                        Room room;Peer member;
                        if(invite.Join){room=GetRoom(peer);if(room.Id!=invite.Room)throw new InvalidOperationException("Invitation belongs to an old session.");RequireHost(peer,room);member=from;}
                        else{room=invite.Room==null?(from.Room==null?Create(from):throw new InvalidOperationException("Invitation is stale.")):GetRoom(from);if(invite.Room!=null&&room.Id!=invite.Room)throw new InvalidOperationException("Invitation belongs to an old session.");RequireHost(from,room);member=peer;}
                        if(room.Locked||member.Room!=null||room.Members.Count>=16)throw new InvalidOperationException("Session is locked, full, or device already joined.");
                        member.Room=room.Id;member.Ready=false;room.Members.Add(member.Id);Snapshot(room,"joined");break;
                    }
                    case "leave":ValidatedRoom(peer,command);Remove(peer);break;
                    case "snapshot":Snapshot(GetRoom(peer),"state");break;
                    case "quality":{
                        double rtt=command.GetProperty("rtt").GetDouble(),jitter=command.GetProperty("jitter").GetDouble();
                        if(!double.IsFinite(rtt)||!double.IsFinite(jitter)||rtt<0||jitter<0||rtt>10||jitter>10)throw new ArgumentException("Invalid connection sample.");
                        peer.Rtt=rtt;peer.Jitter=jitter;peer.ClockReady=command.GetProperty("synced").GetBoolean();peer.QualityAt=Now;break;
                    }
                    case "start":{
                        var room=ValidatedRoom(peer,command);if(!room.Everyone)RequireHost(peer,room);
                        if(room.Playing)throw new InvalidOperationException("This session is already playing. Stop before restarting.");
                        var participants=room.Members.Select(id=>peers[id]).ToArray();
                        if(room.RequireReady&&participants.Any(p=>!p.Ready))throw new InvalidOperationException("Everyone must prepare their task and become Ready.");
                        var eligible=participants.Where(p=>p.Ready&&p.ClockReady&&Now-p.QualityAt<6&&p.Rtt<=0.2&&p.Jitter<=0.05).ToArray();
                        if(eligible.Length==0||room.CancelIfSlow&&eligible.Length!=participants.Length)throw new InvalidOperationException("Unable to synchronize all selected devices in time.");
                        double delay=command.TryGetProperty("delay",out var delayValue)?delayValue.GetDouble():0;
                        if(!double.IsFinite(delay)||delay<0||delay>3600)throw new ArgumentException("Scheduled start must be between 0 and 3600 seconds.");
                        double buffer=Math.Max(0.04,eligible.Max(p=>p.Rtt+2*p.Jitter)+0.02);
                        room.Playing=true;room.Active=eligible.Select(p=>p.Id).ToHashSet();room.RunSequence=++room.Sequence;
                        Broadcast(room,new{type="start",sessionId=room.Id,sequence=room.RunSequence,target=Now+Math.Max(delay,buffer),scheduled=delay>0,participants=eligible.Select(p=>p.Id).ToArray()});
                        foreach(var p in participants){p.Ready=false;if(!eligible.Contains(p))p.Send(new{type="excluded",message="Unable to synchronize in time. This playback started without your device."});}
                        break;
                    }
                    case "ready":{
                        var room=ValidatedRoom(peer,command);peer.Task=Name(Text(command,"task"));
                        // Client must only claim prepared=true after actual preparation; server cannot verify native input readiness.
                        peer.Ready=command.GetProperty("ready").GetBoolean()&&command.GetProperty("prepared").GetBoolean();Snapshot(room,"ready");break;
                    }
                    case "settings":{
                        var room=ValidatedRoom(peer,command);RequireHost(peer,room);
                        room.Locked=command.GetProperty("locked").GetBoolean();room.Everyone=command.GetProperty("everyone").GetBoolean();room.RequireReady=command.GetProperty("requireReady").GetBoolean();room.CancelIfSlow=command.TryGetProperty("cancelIfSlow",out var cancelSlow)&&cancelSlow.GetBoolean();Snapshot(room,"settings");break;
                    }
                    case "transfer":case "kick":{
                        var room=ValidatedRoom(peer,command);RequireHost(peer,room);Guid target=Guid.Parse(Text(command,"memberId"));
                        if(target==peer.Id||!room.Members.Contains(target))throw new InvalidOperationException("Select another session member.");
                        if(type=="transfer"){room.Host=target;Snapshot(room,"hostChanged");}else Remove(peers[target]);break;
                    }
                    case "finished":{
                        var room=ValidatedRoom(peer,command);
                        if(command.GetProperty("runSequence").GetInt64()!=room.RunSequence)throw new InvalidOperationException("Old playback completion.");
                        room.Active.Remove(peer.Id);if(room.Active.Count==0){room.Playing=false;Snapshot(room,"finished");}break;
                    }
                    case "stop":{
                        var room=ValidatedRoom(peer,command);if(!room.Everyone)RequireHost(peer,room);
                        room.Playing=false;room.Active.Clear();Broadcast(room,new{type="stop",sessionId=room.Id,sequence=++room.Sequence});break;
                    }
                    default:throw new InvalidOperationException("Unsupported session command.");
                }
                peer.Send(new{type="ack",requestId=request});
            }catch(Exception e)when(e is InvalidOperationException or ArgumentException or KeyNotFoundException or FormatException){peer.Send(new{type="error",message=e.Message});}
        }
    }
    private Room Create(Peer peer){var room=new Room(peer.Id);do{room.Code=RandomNumberGenerator.GetInt32(100000,1000000).ToString();}while(rooms.Values.Any(r=>r.Code==room.Code));rooms.Add(room.Id,room);peer.Room=room.Id;Snapshot(room,"created");return room;}
    private void AddInvite(Peer from,Peer to,Guid? room,bool join){
        if(invites.Values.Count(i=>i.From==from.Id)>=8)throw new InvalidOperationException("Wait for pending invitations to finish.");
        var invite=new Invite(Guid.NewGuid(),from.Id,to.Id,room,join,Now+60);invites.Add(invite.Id,invite);
        to.Send(new{type="invitation",inviteId=invite.Id,from=from.Username,publicId=from.PublicId,joinRequest=join});from.Send(new{type="inviteSent",inviteId=invite.Id});
    }
    private Room GetRoom(Peer peer)=>peer.Room is Guid id&&rooms.TryGetValue(id,out var room)&&room.Members.Contains(peer.Id)?room:throw new InvalidOperationException("You are not in that session.");
    private Room ValidatedRoom(Peer peer,JsonElement message){var room=GetRoom(peer);if(Guid.Parse(Text(message,"sessionId"))!=room.Id)throw new InvalidOperationException("Wrong session.");return room;}
    private static void RequireHost(Peer peer,Room room){if(room.Host!=peer.Id)throw new InvalidOperationException("Only the current Host can do that.");}
    private void Snapshot(Room room,string reason)=>Broadcast(room,new{type="state",reason,sessionId=room.Id,sequence=++room.Sequence,host=room.Host,code=room.Code,locked=room.Locked,everyone=room.Everyone,requireReady=room.RequireReady,cancelIfSlow=room.CancelIfSlow,members=room.Members.Select(id=>new{id,username=peers[id].Username,publicId=peers[id].PublicId,ready=peers[id].Ready,task=peers[id].Task,ping=peers[id].Rtt*1000,jitter=peers[id].Jitter*1000}).ToArray()});
    private void Broadcast(Room room,object message){foreach(var id in room.Members)if(peers.TryGetValue(id,out var peer))peer.Send(message);}
    private void Remove(Peer peer){if(peer.Room==null)return;var room=GetRoom(peer);room.Members.Remove(peer.Id);room.Active.Remove(peer.Id);if(room.Active.Count==0)room.Playing=false;peer.Room=null;peer.Ready=false;peer.Send(new{type="left",sessionId=room.Id});if(room.Members.Count==0)rooms.Remove(room.Id);else{if(room.Host==peer.Id)room.Host=room.Members[0];Snapshot(room,"left");}}
    internal void Disconnect(Peer peer){lock(gate){if(!peers.TryGetValue(peer.Id,out var current)||!ReferenceEquals(current,peer))return;Remove(peer);peers.Remove(peer.Id);peer.Outbound.Writer.TryComplete();foreach(var invite in invites.Values.Where(i=>i.From==peer.Id||i.To==peer.Id).ToArray())invites.Remove(invite.Id);}}
    private static string Text(JsonElement value,string property)=>value.GetProperty(property).GetString()??throw new ArgumentException("Missing "+property);
    private static string Name(string text){text=text.Trim();if(text.Length<1||text.Length>64||text.Any(char.IsControl))throw new ArgumentException("Names must contain 1-64 printable characters.");return text;}
}
