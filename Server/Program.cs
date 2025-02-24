using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddCors(options => options.AddDefaultPolicy(policyBuilder =>
    policyBuilder.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));
var app = builder.Build();
app.UseCors();
app.MapHub<ChatHub>("chat");
app.Run();

class ChatHub : Hub
{
    private static readonly List<string> m_Rooms = ["General"];
    private static readonly List<(string Name, string ConnectionId)> m_NamesTaken = [];
    private static readonly List<(string RoomName, string ConnectionId)> m_RoomMembers = [];

    [HubMethodName("SetName")]
    public void SetName(string name)
    {
        if (m_NamesTaken.All(n => n.Name != name))
        {
            m_NamesTaken.Add((name, Context.ConnectionId));
            Clients.Caller.SendAsync("NameSetSuc");
            return;
        }

        Clients.Caller.SendAsync("NameSetFail");
    }

    [HubMethodName("GetRooms")]
    public void GetRooms() => Clients.Caller.SendAsync("RoomsUpdate", m_Rooms);

    [HubMethodName("CreateRoom")]
    public void CreateRoom(string roomName)
    {
        if (m_Rooms.Contains(roomName))
        {
            Clients.Caller.SendAsync("CreateRoomFail");
            return;
        }

        m_Rooms.Add(roomName);
        Clients.Caller.SendAsync("CreateRoomSuc");
    }
    
    [HubMethodName("JoinRoom")]
    public void JoinRoom(string roomName)
    {
        if (m_RoomMembers.Any(r => r.ConnectionId == Context.ConnectionId))
        {
            Clients.Caller.SendAsync("JoinRoomFail");
            return;
        }
        
        if (!m_Rooms.Contains(roomName))
        {
            Clients.Caller.SendAsync("JoinRoomFail");
            return;
        }

        m_RoomMembers.Add((roomName, Context.ConnectionId));
        Groups.AddToGroupAsync(Context.ConnectionId, roomName);
        Clients.Caller.SendAsync("JoinRoomSuc");
        Clients.Group(roomName).SendAsync("ReceiveMessage", "[Server]", $"{m_NamesTaken.First(n => n.ConnectionId == Context.ConnectionId).Name} has joined the room.");
    }
    
    [HubMethodName("LeaveRoom")]
    public void LeaveRoom()
    {
        var roomName = m_RoomMembers.FirstOrDefault(r => r.ConnectionId == Context.ConnectionId).RoomName;

        if (roomName is null)
            return;
        
        m_RoomMembers.RemoveAll(r => r.ConnectionId == Context.ConnectionId);
        Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
        Clients.Caller.SendAsync("LeaveRoomSuc");
        Clients.Group(roomName).SendAsync("ReceiveMessage", "[Server]", $"{m_NamesTaken.First(n => n.ConnectionId == Context.ConnectionId).Name} has left the room.");
    }
    
    [HubMethodName("DeleteRoom")]
    public void DeleteRoom(string roomName)
    {
        if (!m_Rooms.Contains(roomName))
        {
            Clients.Caller.SendAsync("DeleteRoomFail");
            return;
        }

        m_Rooms.Remove(roomName);
        m_RoomMembers.RemoveAll(r => r.RoomName == roomName);
        Clients.Caller.SendAsync("DeleteRoomSuc");
    }
    
    [HubMethodName("SendMessage")]
    public void SendMessage(string message)
    {
        if (m_RoomMembers.All(r => r.ConnectionId != Context.ConnectionId))
        {
            Clients.Caller.SendAsync("SendMessageFail");
            return;
        }

        if (m_NamesTaken.All(n => n.ConnectionId != Context.ConnectionId))
        {
            Clients.Caller.SendAsync("SendMessageFail");
            return;
        }

        var group = m_RoomMembers.First(r => r.ConnectionId == Context.ConnectionId).RoomName;

        Clients.Group(group).SendAsync("ReceiveMessage",
            m_NamesTaken.First(n => n.ConnectionId == Context.ConnectionId).Name, message);

        Clients.Caller.SendAsync("SendMessageSuc");
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        m_NamesTaken.RemoveAll(n => n.ConnectionId == Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}