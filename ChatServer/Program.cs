using System.Net;
using System.Net.Sockets;
using System.Text;
using ChatServer.HistoryPrivate;
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
    static Dictionary<string, string> users = new();
    static Dictionary<TcpClient, string> loginsByClient = new();
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

        users.Add("Andriy", "3333");
        users.Add("Maxim", "1111");
        users.Add("Anna", "2222");

        rooms.Add(new Room(nextRoomId++, "General", "System"));
        rooms.Add(new Room(nextRoomId++, "Games", "System"));
        rooms.Add(new Room(nextRoomId++, "Programming", "System"));

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

            if (users.ContainsKey(login) && users[login] == password)
            {
                stream.Write(Encoding.UTF8.GetBytes("Congratulations\n"));
                Console.WriteLine($"{login} logged in");
                loginsByClient[client] = login;
                clientRooms[client] = 1;

                SendRoomsList(stream);
                SendUsersList();

                // Send last 50 messages to new user
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


                    if (msg.StartsWith("JOIN_ROOM|"))
                    {
                        string roomIdText =
                            msg.Substring("JOIN_ROOM|".Length);

                        int roomId;

                        if (int.TryParse(roomIdText, out roomId))
                        {
                            clientRooms[client] = roomId;

                            Console.WriteLine(
                                $"{login} joined room {roomId}");

                            stream.Write(
                                Encoding.UTF8.GetBytes(
                                    $"JOINED|{roomId}\n"));
                        }

                        continue;
                    }

                    if (msg.StartsWith("CREATE_ROOM|"))
                    {
                        string roomName =
                            msg.Substring("CREATE_ROOM|".Length).Trim();

                        Room room = new Room(
                            nextRoomId++,
                            roomName,
                            login);

                        rooms.Add(room);

                        Console.WriteLine(
                            $"Room created: {room.Name}");

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

                    if (msg.StartsWith("/msg "))
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
                    else
                    {
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

                        foreach (var c in clients)
                        {
                            try
                            {
                                if (!clientRooms.ContainsKey(c))
                                    continue;

                                if (clientRooms[c] != senderRoom)
                                    continue;

                                c.GetStream().Write(
                                    Encoding.UTF8.GetBytes(
                                        $"MSG|{newMsg.Id}|{newMsg.Sender}|{newMsg.Text}\n"));
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
            clients.Remove(client);

            if (loginsByClient.ContainsKey(client))
                loginsByClient.Remove(client);

            if (clientRooms.ContainsKey(client))
                clientRooms.Remove(client);

            SendUsersList();

            client.Close();
        }

        //fghfgf
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

        stream.Write(
            Encoding.UTF8.GetBytes(sb.ToString()));
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
                client.GetStream().Write(
                    Encoding.UTF8.GetBytes(sb.ToString()));
            }
            catch
            {
            }
        }// лолоололо

    }
}