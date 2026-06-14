using System.Net;
using System.Net.Sockets;
using System.Text;
using ChatServer.HistoryPrivate;
using ChatServer.Models;



namespace ChatServer;

class ChatMessage
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string Sender { get; set; }
    public string Text { get; set; }
    public DateTime Time { get; set; }
}

class Program
{
    static List<TcpClient> clients = new();
    static List<User> users = new(); 
    static Dictionary<TcpClient, string> loginsByClient = new();
    static HashSet<string> mutedUsers = new();
    static HashSet<string> bannedUsers = new();
    static List<Room> rooms = new();
    static Dictionary<TcpClient, int> clientRooms = new();
    static int nextRoomId = 1;
    static List<ChatMessage> messages = new List<ChatMessage>();
    static int nextId = 1;

    static void Main()
    {
        var history = MasHistory.GetLast(1000);
        foreach (var oldMsg in history)
        {
            messages.Add(new ChatMessage
            {
                Id = oldMsg.Id,
                Sender = oldMsg.Sender,
                Text = oldMsg.Text,
                Time = oldMsg.Timestamp
            });
            if (oldMsg.Id >= nextId) nextId = oldMsg.Id + 1;
        }
        Console.WriteLine($"Loaded {messages.Count} messages from history");

        
        users = UserStorage.Load();
        bannedUsers = BanStorage.Load();
        mutedUsers = MuteStorage.Load();

        if (users.Count == 0)
        {
            users.Add(new User
            {
                Login = "Maxim",
                Password = "1111",
                Role = "Admin"
            });

            users.Add(new User
            {
                Login = "Andriy",
                Password = "3333",
                Role = "User"
            });

            users.Add(new User
            {
                Login = "Anna",
                Password = "2222",
                Role = "User"
            });

            UserStorage.Save(users);
        }

        rooms = RoomStorage.Load();

        if (rooms.Count == 0)
        {
            rooms.Add(new Room(nextRoomId++, "General", "System"));
            rooms.Add(new Room(nextRoomId++, "Games", "System"));
            rooms.Add(new Room(nextRoomId++, "Programming", "System"));

            RoomStorage.Save(rooms);
        }
        else
        {
            nextRoomId = rooms.Max(r => r.Id) + 1;
        }

        TcpListener server = new(IPAddress.Any, 5000);
        server.Start();
        Console.WriteLine("Server started on port 5000");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            clients.Add(client);
            Console.WriteLine("Client connected");
            new Thread(() => HandleClient(client)).Start();
        }
    }

    static void HandleClient(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            var buffer = new byte[1024];

            var bytes = stream.Read(buffer);
            var login = Encoding.UTF8.GetString(buffer, 0, bytes);
            bytes = stream.Read(buffer);
            var password = Encoding.UTF8.GetString(buffer, 0, bytes);

            if (login.StartsWith("REGISTER|"))
            {
                string newLogin =
                    login.Substring("REGISTER|".Length);

                if (users.Any(u => u.Login == newLogin))
                {
                    stream.Write(
                        Encoding.UTF8.GetBytes(
                            "REGISTER_ERROR\n"));

                    client.Close();
                    return;
                }

                users.Add(new User
                {
                    Login = newLogin,
                    Password = password,
                    Role = "User"
                });

                UserStorage.Save(users);

                stream.Write(
                    Encoding.UTF8.GetBytes(
                        "REGISTER_OK\n"));

                client.Close();
                return;
            }

            if (bannedUsers.Contains(login))
            {
                stream.Write(Encoding.UTF8.GetBytes("You are banned\n"));
                client.Close();
                return;
            }

            
            var currentUser = users.FirstOrDefault(u => u.Login == login && u.Password == password);

            if (currentUser != null)
            {
                stream.Write(Encoding.UTF8.GetBytes("Congratulations\n"));
                stream.Write(Encoding.UTF8.GetBytes($"ROLE|{currentUser.Role}\n")); 
                Console.WriteLine($"{login} logged in");
                loginsByClient[client] = login;
                clientRooms[client] = 1;

                foreach (var c in clients)
                {
                    try
                    {
                        c.GetStream().Write(Encoding.UTF8.GetBytes($"SYSTEM|{login} joined the chat\n"));
                    }
                    catch
                    {
                    }
                }

                SendRoomsList(stream);
                SendUsersList();

                
                foreach (var oldMsg in MasHistory.GetLast(50))
                {
                    var line = $"MSG|{oldMsg.Id}|{oldMsg.Sender}|{oldMsg.Text}\n";
                    stream.Write(Encoding.UTF8.GetBytes(line));
                }

                while (true)
                {
                    bytes = stream.Read(buffer);
                    if (bytes == 0) break;
                    var msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                    Console.WriteLine($"{login}: {msg}");

                    if (msg.StartsWith("TYPING|"))
                    {
                        int senderRoom = clientRooms[client];

                        foreach (var c in clients)
                        {
                            if (c == client)
                                continue;

                            if (!clientRooms.ContainsKey(c))
                                continue;

                            if (clientRooms[c] != senderRoom)
                                continue;

                            try
                            {
                                c.GetStream().Write(
                                    Encoding.UTF8.GetBytes(msg));
                            }
                            catch
                            {
                            }
                        }

                        continue;
                    }

                    if (msg.StartsWith("JOIN_ROOM|"))
                    {
                        string roomIdText =
                            msg.Substring("JOIN_ROOM|".Length);

                        int roomId;

                        if (int.TryParse(roomIdText, out roomId))
                        {
                            int oldRoomId = clientRooms[client];

                            clientRooms[client] = roomId;

                            string oldRoomName =
                                rooms.First(r => r.Id == oldRoomId).Name;

                            string newRoomName =
                                rooms.First(r => r.Id == roomId).Name;

                            foreach (var c in clients)
                            {
                                try
                                {
                                    c.GetStream().Write(
                                        Encoding.UTF8.GetBytes(
                                            $"SYSTEM|{login} left room {oldRoomName}\n"));

                                    c.GetStream().Write(
                                        Encoding.UTF8.GetBytes(
                                            $"SYSTEM|{login} joined room {newRoomName}\n"));
                                }
                                catch
                                {
                                }
                            }

                            Console.WriteLine(
                                $"{login} joined room {newRoomName}");

                            stream.Write(
                                Encoding.UTF8.GetBytes(
                                    $"JOINED|{roomId}\n"));
                        }

                        continue;
                    }
                    else if (msg.StartsWith("CREATE_ROOM|"))
                    {
                        string roomName = msg.Substring("CREATE_ROOM|".Length).Trim();
                        Room room = new Room(nextRoomId++, roomName, login);
                        rooms.Add(room);
                        RoomStorage.Save(rooms);
                        Console.WriteLine($"Room created: {room.Name}");

                        foreach (var c in clients)
                        {
                            try
                            {
                                SendRoomsList(c.GetStream());
                            }
                            catch
                            {
                            }
                        }
                        continue;
                    }
                    else if (msg.StartsWith("/msg "))
                    {
                        var parts = msg.Substring(5).Split(' ', 2);
                        if (parts.Length == 2)
                        {
                            string targetUser = parts[0];
                            string privateText = parts[1];

                            var targetClient = clients.FirstOrDefault(c => loginsByClient.TryGetValue(c, out var l) && l == targetUser);
                            if (targetClient != null)
                            {
                                try
                                {
                                    targetClient.GetStream().Write(Encoding.UTF8.GetBytes($"[PM from {login}] {privateText}\n"));
                                    stream.Write(Encoding.UTF8.GetBytes($"[PM to {targetUser}] {privateText}\n"));
                                }
                                catch { }
                            }
                            else
                            {
                                stream.Write(Encoding.UTF8.GetBytes($"User '{targetUser}' not found\n"));
                            }
                        }
                    }
                    else if (msg.StartsWith("/del "))
                    {
                        int msgId;
                        if (int.TryParse(msg.Substring(5), out msgId))
                        {
                            var targetMsg = messages.FirstOrDefault(m => m.Id == msgId);
                            if (targetMsg != null && targetMsg.Sender == login)
                            {
                                messages.Remove(targetMsg);
                                Console.WriteLine($"Deleted {msgId}, sending DEL to all");
                                foreach (var c in clients)
                                {
                                    try
                                    {
                                        c.GetStream().Write(Encoding.UTF8.GetBytes($"DEL|{msgId}\n"));
                                        Console.WriteLine($"Sent DEL|{msgId} to client");
                                    }
                                    catch { }
                                }
                            }
                            else
                            {
                                stream.Write(Encoding.UTF8.GetBytes("Error. You can delete only your own messages!\n"));
                            }
                        }
                    }

                    else if (msg == "/serverstats")
                    {
                        string stats =
                            $"Users online: {loginsByClient.Count}\n" +
                            $"Messages: {messages.Count}\n" +
                            $"Rooms: {rooms.Count}\n" +
                            $"Banned: {bannedUsers.Count}\n" +
                            $"Muted: {mutedUsers.Count}\n";

                        stream.Write(
                            Encoding.UTF8.GetBytes(stats));
                    }

                    else if (msg.StartsWith("/mute "))
                    {
                        
                        if (users.First(u => u.Login == login).Role != "Admin")
                        {
                            stream.Write(Encoding.UTF8.GetBytes("Only Admin can mute users\n"));
                            continue;
                        }

                        string targetUser = msg.Substring(6).Trim();

                        if (targetUser == login)
                        {
                            stream.Write(Encoding.UTF8.GetBytes("You cannot mute yourself\n"));
                            continue;
                        }

                        mutedUsers.Add(targetUser);
                        MuteStorage.Save(mutedUsers);

                        foreach (var c in clients)
                        {
                            try
                            {
                                c.GetStream().Write(Encoding.UTF8.GetBytes($"SYSTEM|{targetUser} was muted by {login}\n"));
                            }
                            catch { }
                        }
                    }
                    else if (msg == "/admins")
                    {
                        var admins = users.Where(x => x.Role == "Admin").Select(x => x.Login);
                        stream.Write(Encoding.UTF8.GetBytes($"Admins: {string.Join(", ", admins)}\n"));
                    }
                    else if (msg.StartsWith("/makeadmin "))
                    {
                        
                        if (users.First(u => u.Login == login).Role != "Admin")
                        {
                            stream.Write(Encoding.UTF8.GetBytes("Only Admin can make admins\n"));
                            continue;
                        }

                        string targetUser = msg.Substring(11).Trim();

                        if (targetUser == login)
                        {
                            stream.Write(Encoding.UTF8.GetBytes("You are already admin\n"));
                            continue;
                        }

                        var targetUserObj = users.FirstOrDefault(u => u.Login == targetUser);
                        if (targetUserObj != null)
                        {
                            targetUserObj.Role = "Admin";
                            UserStorage.Save(users); 

                            foreach (var c in clients)
                            {
                                try
                                {
                                    c.GetStream().Write(Encoding.UTF8.GetBytes($"SYSTEM|{targetUser} is now Admin\n"));
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            stream.Write(Encoding.UTF8.GetBytes("User not found\n"));
                        }
                    }

                    else if (msg.StartsWith("/search "))
                    {
                        string searchText =
                            msg.Substring(8).Trim();

                        var found =
                            messages
                            .Where(m =>
                                m.Text.Contains(
                                    searchText,
                                    StringComparison.OrdinalIgnoreCase))
                            .TakeLast(20);

                        foreach (var m in found)
                        {
                            stream.Write(
                                Encoding.UTF8.GetBytes(
                                    $"[SEARCH] {m.Sender}: {m.Text}\n"));
                        }
                    }

                    else if (msg.StartsWith("/ban "))
                    {
                        
                        if (users.First(u => u.Login == login).Role != "Admin")
                        {
                            stream.Write(Encoding.UTF8.GetBytes("Only Admin can ban users\n"));
                            continue;
                        }

                        string targetUser = msg.Substring(5).Trim();
                        bannedUsers.Add(targetUser);
                        BanStorage.Save(bannedUsers);

                        var targetClient = loginsByClient.FirstOrDefault(x => x.Value == targetUser).Key;

                        if (targetClient != null)
                            targetClient.Close();

                        foreach (var c in clients)
                        {
                            try
                            {
                                c.GetStream().Write(Encoding.UTF8.GetBytes($"SYSTEM|{targetUser} was banned by {login}\n"));
                            }
                            catch { }
                        }
                    }
                    else if (msg.StartsWith("/unban "))
                    {
                        
                        if (users.First(u => u.Login == login).Role != "Admin")
                        {
                            stream.Write(Encoding.UTF8.GetBytes("Only Admin can unban users\n"));
                            continue;
                        }

                        string targetUser = msg.Substring(7).Trim();
                        bannedUsers.Remove(targetUser);
                        BanStorage.Save(bannedUsers);

                        foreach (var c in clients)
                        {
                            try
                            {
                                c.GetStream().Write(Encoding.UTF8.GetBytes($"SYSTEM|{targetUser} was unbanned by {login}\n"));
                            }
                            catch { }
                        }
                    }
                    else if (msg.StartsWith("/unmute "))
                    {
                       
                        if (users.First(u => u.Login == login).Role != "Admin")
                        {
                            stream.Write(Encoding.UTF8.GetBytes("Only Admin can unmute users\n"));
                            continue;
                        }

                        string targetUser = msg.Substring(8).Trim();
                        mutedUsers.Remove(targetUser);
                        MuteStorage.Save(mutedUsers);

                        foreach (var c in clients)
                        {
                            try
                            {
                                c.GetStream().Write(Encoding.UTF8.GetBytes($"SYSTEM|{targetUser} was unmuted by {login}\n"));
                            }
                            catch { }
                        }
                    }
                    else if (msg.StartsWith("/kick "))
                    {
                        
                        if (users.First(u => u.Login == login).Role != "Admin")
                        {
                            stream.Write(Encoding.UTF8.GetBytes("Only Admin can kick users\n"));
                            continue;
                        }

                        string targetUser = msg.Substring(6).Trim();

                        if (targetUser == login)
                        {
                            stream.Write(Encoding.UTF8.GetBytes("You cannot kick yourself\n"));
                            continue;
                        }

                        var targetClient = loginsByClient.FirstOrDefault(x => x.Value == targetUser).Key;

                        if (targetClient == null)
                        {
                            stream.Write(Encoding.UTF8.GetBytes("User not found\n"));
                            continue;
                        }

                        try
                        {
                            targetClient.GetStream().Write(Encoding.UTF8.GetBytes("You were kicked by Admin\n"));
                            targetClient.Close();

                            foreach (var c in clients)
                            {
                                try
                                {
                                    c.GetStream().Write(Encoding.UTF8.GetBytes($"SYSTEM|{targetUser} was kicked by {login}\n"));
                                }
                                catch
                                {
                                }
                            }

                            Console.WriteLine($"{targetUser} was kicked by {login}");
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        if (mutedUsers.Contains(login))
                        {
                            stream.Write(Encoding.UTF8.GetBytes("You are muted\n"));
                            continue;
                        }
                        var newMsg = new ChatMessage
                        {
                            Id = nextId++,
                            RoomId = clientRooms[client],
                            Sender = login,
                            Text = msg,
                            Time = DateTime.Now
                        };
                        messages.Add(newMsg);
                        MasHistory.Add(new Message { Id = newMsg.Id, Sender = login, Text = msg, Timestamp = DateTime.Now });

                        int senderRoom = clientRooms[client];

                        foreach (var c in clients.ToList())
                        {
                            try
                            {
                                if (!clientRooms.ContainsKey(c))
                                    continue;

                                if (clientRooms[c] != senderRoom)
                                    continue;

                                c.GetStream().Write(Encoding.UTF8.GetBytes($"MSG|{newMsg.Id}|{newMsg.Sender}|{newMsg.Text}\n"));
                            }
                            catch
                            {
                                clients.Remove(c);
                                c.Close();
                            }
                        }
                    }
                }
            }
            else
            {
                stream.Write(Encoding.UTF8.GetBytes("Error"));
                client.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client disconnected: {ex.Message}");
        }
        finally
        {
            string disconnectedUser = "";

            if (loginsByClient.ContainsKey(client))
            {
                disconnectedUser = loginsByClient[client];
            }

            clients.Remove(client);

            if (loginsByClient.ContainsKey(client))
                loginsByClient.Remove(client);

            if (clientRooms.ContainsKey(client))
                clientRooms.Remove(client);

            SendUsersList();

            if (!string.IsNullOrEmpty(disconnectedUser))
            {
                foreach (var c in clients)
                {
                    try
                    {
                        c.GetStream().Write(Encoding.UTF8.GetBytes($"SYSTEM|{disconnectedUser} left the chat\n"));
                    }
                    catch
                    {
                    }
                }
            }

            client.Close();
        }
    }

    static void SendRoomsList(NetworkStream stream)
    {
        StringBuilder sb = new();
        sb.Append("ROOMS|");

        foreach (var room in rooms)
        {
            sb.Append(room.Id);
            sb.Append(",");
            sb.Append(room.Name);
            sb.Append(";");
        }

        sb.Append("\n");
        stream.Write(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    static void SendUsersList()
    {
        StringBuilder sb = new();
        sb.Append("USERS|");

        foreach (var user in loginsByClient.Values)
        {
            sb.Append(user);
            sb.Append(";");
        }

        sb.Append("\n");

        foreach (var client in clients)
        {
            try
            {
                client.GetStream().Write(Encoding.UTF8.GetBytes(sb.ToString()));
            }
            catch
            {
            }
        }
    }
}