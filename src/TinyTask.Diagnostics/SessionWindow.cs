using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TinyTask;

internal sealed class SessionWindow : Window
{
    private readonly SessionIdentity identity;
    private readonly string identityPath=Path.Combine(MainWindow.DataDirectory,"session-identity.json");
    private readonly TextBox username=new(),publicId=new(),endpoint=new(),recipient=new(),code=new();
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap},roomCode=new();
    private readonly ListBox members=new(),invitations=new();
    private readonly TextBox activity=new(){IsReadOnly=true,AcceptsReturn=true,Height=110,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private readonly CheckBox locked=new(){Content="Lock session"},everyone=new(){Content="Everyone can control"},requireReady=new(){Content="Require everyone Ready",IsChecked=true};
    private SessionConnection? connection;private Guid host;private bool closing;
    private readonly Action localStop;
    private readonly Func<string> prepare;
    private readonly Func<double,Task> play;
    private bool ready;
    private readonly TextBox scheduledDelay=new(){Text="0",Width=80};
    private readonly CheckBox cancelIfSlow=new(){Content="Cancel start if a member cannot synchronize"};
    private sealed record Member(Guid Id,string Username,string PublicId,bool Host,bool Ready,string Task,double Ping,double Jitter){public override string ToString()=>Username+" · "+PublicId+(Host?" · Host":" · Member")+"\n"+(Ready?"Ready":"Not Ready")+" - "+Task+$" - {Ping:0} ms (jitter {Jitter:0} ms)";}
    private sealed record Invitation(Guid Id,string From,string PublicId,bool Join){public override string ToString()=>From+" · "+PublicId+(Join?" requests to join":" invited you");}
    internal SessionWindow(Window owner,Action stop,Func<string> prepareTask,Func<double,Task> playTask)
    {
        identity=SessionIdentity.Load(identityPath);localStop=stop;prepare=prepareTask;play=playTask;
        Owner=owner;Title="TinyTask sessions";Width=620;Height=740;MinWidth=560;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;UiTheme.Inherit(this,owner);
        foreach(var list in new[]{members,invitations}){list.SetResourceReference(Control.BackgroundProperty,"Surface");list.SetResourceReference(Control.ForegroundProperty,"Ink");list.SetResourceReference(Control.BorderBrushProperty,"Line");list.MinHeight=55;}
        var panel=new StackPanel{Margin=new Thickness(20)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        panel.Children.Add(new TextBlock{Text="Sessions",FontSize=24});
        panel.Children.Add(new TextBlock{Text="Choose the name other users will see. No account or sign-in is needed. A session server is required; online hosting is not configured yet.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,12)});
        void Field(string label,TextBox box,string initial){panel.Children.Add(new TextBlock{Text=label});box.Text=initial;box.Margin=new Thickness(0,4,0,10);panel.Children.Add(box);}
        Field("Username",username,identity.Username);Field("Public ID",publicId,identity.PublicId);Field("Session server",endpoint,identity.Endpoint);
        var controls=new WrapPanel();panel.Children.Add(controls);
        Add(controls,"Connect",Connect);Add(controls,"Disconnect",Disconnect);Add(controls,"Create session",()=>Command("create"));Add(controls,"Leave",()=>Command("leave"));
        panel.Children.Add(status);panel.Children.Add(roomCode);Add(panel,"Copy invite",()=>{if(!string.IsNullOrEmpty(roomCode.Text))Clipboard.SetText("Join my TinyTask session: "+roomCode.Text);return Task.CompletedTask;});
        Field("Invite by public ID",recipient,"");Add(panel,"Invite",async()=>{if(Confirm("Send a session invitation to "+recipient.Text+"?"))await Command("invite",new(){["publicId"]=recipient.Text});});
        Field("Join with code",code,"");Add(panel,"Request to join",()=>Command("join",new(){["code"]=code.Text.Replace("-","").Trim()}));
        panel.Children.Add(new TextBlock{Text="Invitations / join requests"});panel.Children.Add(invitations);
        var responses=new WrapPanel();panel.Children.Add(responses);
        Add(responses,"Accept",()=>Reply("accept"));Add(responses,"Decline",()=>Reply("decline"));
        panel.Children.Add(new TextBlock{Text="Members"});members.MinHeight=70;panel.Children.Add(members);
        var actions=new WrapPanel();panel.Children.Add(actions);
        Add(actions,"Make Host",()=>MemberCommand("transfer"));Add(actions,"Kick",()=>MemberCommand("kick"));
        panel.Children.Add(locked);panel.Children.Add(everyone);panel.Children.Add(requireReady);panel.Children.Add(cancelIfSlow);
        Add(panel,"Apply session settings",()=>Command("settings",new(){["locked"]=locked.IsChecked==true,["everyone"]=everyone.IsChecked==true,["requireReady"]=requireReady.IsChecked==true,["cancelIfSlow"]=cancelIfSlow.IsChecked==true}));
        var playback=new WrapPanel();panel.Children.Add(playback);
        Add(playback,"Prepare / Ready",async()=>{
            if(connection?.Room==null||!connection.Synced)throw new InvalidOperationException("Join a session and wait for a stable clock sample first.");
            if(!Confirm("Prepare your selected Classic macro? The Host can then start it, controlling your mouse and keyboard. F10 always stops locally."))return;
            string task=prepare();await Command("ready",new(){["task"]=task,["ready"]=true,["prepared"]=true});ready=true;status.Text="Ready \u00b7 Synced";
        });
        Add(playback,"Not Ready",async()=>{localStop();await SetNotReady();});
        Add(playback,"Start together",()=>Command("start",new(){["delay"]=0}));
        Add(playback,"Stop session",async()=>{localStop();await Command("stop");});
        var scheduling=new WrapPanel();panel.Children.Add(scheduling);scheduling.Children.Add(new TextBlock{Text="Start in seconds (optional): ",VerticalAlignment=VerticalAlignment.Center});scheduling.Children.Add(scheduledDelay);
        foreach(int seconds in new[]{3,5,10})Add(scheduling,seconds+" seconds",()=>Command("start",new(){["delay"]=seconds}));
        Add(scheduling,"Schedule",()=>{if(!double.TryParse(scheduledDelay.Text,System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out double delay)||!double.IsFinite(delay)||delay<=0||delay>3600)throw new ArgumentException("Enter a delay between 0 and 3600 seconds.");return Command("start",new(){["delay"]=delay});});
        panel.Children.Add(new TextBlock{Text="Fast Start uses measured clock samples. Unstable devices are excluded unless Cancel start is selected. F10 stops only your local task.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,8)});
        panel.Children.Add(new Expander{Header="Activity",Content=activity,IsExpanded=false});
        Closed+=async(_,_)=>{closing=true;await Disconnect();};
    }
    private void Add(Panel panel,string text,Func<Task> action){var button=new Button{Content=text,Margin=new Thickness(0,3,8,5)};button.Click+=async(_,_)=>{button.IsEnabled=false;try{await action();}catch(Exception e){status.Text=e.Message;}finally{button.IsEnabled=true;}};panel.Children.Add(button);}
    private bool Confirm(string text)=>MessageBox.Show(this,text,"TinyTask session",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
    private async Task Connect()
    {
        if(connection!=null)throw new InvalidOperationException("Disconnect before changing your session identity or server.");
        if(string.IsNullOrWhiteSpace(username.Text))throw new ArgumentException("Choose a username. This is the name others see in sessions.");
        identity.Username=username.Text.Trim();identity.PublicId=publicId.Text.Trim().ToUpperInvariant();identity.Endpoint=endpoint.Text.Trim();
        var next=new SessionConnection();connection=next;
        next.Message+=message=>Dispatcher.BeginInvoke(()=>{if(!closing&&ReferenceEquals(connection,next))Receive(message);});
        next.Disconnected+=message=>Dispatcher.BeginInvoke(async()=>{if(ReferenceEquals(connection,next)){status.Text="Connection lost: "+message;localStop();await Disconnect();}});
        try{await next.Connect(identity);status.Text="Connecting…";}catch{await Disconnect();throw;}
    }
    private async Task Disconnect(){var previous=connection;connection=null;if(previous!=null)await previous.DisposeAsync();members.Items.Clear();invitations.Items.Clear();roomCode.Text="";host=Guid.Empty;ready=false;localStop();if(!closing)status.Text="Disconnected";}
    private Task Command(string type,Dictionary<string,object?>? values=null)=>connection?.Command(type,values)??throw new InvalidOperationException("Connect to a session server first.");
    private async Task Reply(string type){if(invitations.SelectedItem is not Invitation invite)throw new InvalidOperationException("Select an invitation first.");await Command(type,new(){["inviteId"]=invite.Id});invitations.Items.Remove(invite);}
    private async Task MemberCommand(string type){if(host!=identity.DeviceId)throw new InvalidOperationException("Only the Host can do that.");if(members.SelectedItem is not Member member)throw new InvalidOperationException("Select a member first.");if(Confirm((type=="transfer"?"Make Host: ":"Remove member: ")+member.Username+" ("+member.PublicId+")?"))await Command(type,new(){["memberId"]=member.Id});}
    internal async Task SetNotReady()
    {
        if(!ready)return;ready=false;
        if(connection?.Room!=null)try{await Command("ready",new(){["task"]="No prepared task",["ready"]=false,["prepared"]=false});}catch(Exception e){status.Text=e.Message;}
    }
    private Task FinishDeclined(long sequence)=>connection?.Room!=null?Command("finished",new(){["runSequence"]=sequence}):Task.CompletedTask;
    private async void Receive(JsonElement message)
    {
        try{
            string? type=message.GetProperty("type").GetString();
            switch(type){
                case "welcome":identity.Save(identityPath);status.Text="Connected. Create a session or invite someone.";break;
                case "error":status.Text=message.GetProperty("message").GetString();break;
                case "pong":status.Text=$"Connected · {connection!.Ping:0} ms · jitter {connection.Jitter:0} ms · clock {(connection.Synced?"sampled":"sampling…")}";return;
                case "invitation":invitations.Items.Add(new Invitation(message.GetProperty("inviteId").GetGuid(),message.GetProperty("from").GetString()!,message.GetProperty("publicId").GetString()!,message.GetProperty("joinRequest").GetBoolean()));break;
                case "state":
                    host=message.GetProperty("host").GetGuid();roomCode.Text=message.GetProperty("code").GetString();members.Items.Clear();
                    foreach(var member in message.GetProperty("members").EnumerateArray())members.Items.Add(new Member(member.GetProperty("id").GetGuid(),member.GetProperty("username").GetString()!,member.GetProperty("publicId").GetString()!,member.GetProperty("id").GetGuid()==host,member.GetProperty("ready").GetBoolean(),member.GetProperty("task").GetString()!,member.GetProperty("ping").GetDouble(),member.GetProperty("jitter").GetDouble()));
                    locked.IsChecked=message.GetProperty("locked").GetBoolean();everyone.IsChecked=message.GetProperty("everyone").GetBoolean();requireReady.IsChecked=message.GetProperty("requireReady").GetBoolean();
                    cancelIfSlow.IsChecked=message.GetProperty("cancelIfSlow").GetBoolean();
                    locked.IsEnabled=everyone.IsEnabled=requireReady.IsEnabled=cancelIfSlow.IsEnabled=host==identity.DeviceId;break;
                case "start":
                    bool included=false;foreach(var participant in message.GetProperty("participants").EnumerateArray())included|=participant.GetGuid()==identity.DeviceId;
                    if(!included){ready=false;localStop();status.Text="This playback started without your device.";break;}
                    long runSequence=message.GetProperty("sequence").GetInt64();
                    if(!ready||connection==null||!connection.Synced){localStop();status.Text="Start rejected: task or clock is not ready.";await FinishDeclined(runSequence);break;}
                    ready=false;double target=message.GetProperty("target").GetDouble()-connection.Offset;
                    if(!double.IsFinite(target)||target-SessionConnection.Now < -0.05){localStop();status.Text="Start arrived too late. Playback was not started.";await FinishDeclined(runSequence);break;}
                    status.Text=message.GetProperty("scheduled").GetBoolean()?"Scheduled start armed":"Playing together";
                    var runningConnection=connection;
                    try{await play(target);}finally{if(ReferenceEquals(connection,runningConnection)&&connection?.Room==message.GetProperty("sessionId").GetGuid())await Command("finished",new(){["runSequence"]=runSequence});}
                    break;
                case "excluded":ready=false;localStop();status.Text=message.GetProperty("message").GetString();break;
                case "stop":ready=false;localStop();break;
                case "left":members.Items.Clear();roomCode.Text="";host=Guid.Empty;localStop();break;
            }
            if(type!="ack"){activity.AppendText(DateTime.Now.ToString("HH:mm:ss")+" — "+type+(message.TryGetProperty("reason",out var reason)?": "+reason.GetString():"")+Environment.NewLine);if(activity.Text.Length>12000)activity.Text=activity.Text[^8000..];activity.ScrollToEnd();}
        }catch(Exception e){status.Text="Session message could not be handled: "+e.Message;localStop();}
    }
}
