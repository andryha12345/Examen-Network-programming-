using System.Net;
using System.Net.Sockets;
using System.Text;
//
using ChatServer.HistoryPrivate;
// тут підключив історію пірдунів

namespace ChatServer;

class Program
{
    static List<TcpClient> clients = new();
    static Dictionary<string, string> users = new();
    static Dictionary<TcpClient, string> loginsByClient = new();

    static void Main()
    {
        users.Add("admin", "1234");
        users.Add("user", "1111");

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
                stream.Write(Encoding.UTF8.GetBytes("Congratulations"));
                Console.WriteLine($"{login} logged in");
                loginsByClient[client] = login;

                // Надсилаємо останні 50 повідомлень
                foreach (var oldMsg in MasHistory.GetLast(50))
                {
                    var line = $"[{oldMsg.Timestamp:HH:mm}] {oldMsg.Sender}: {oldMsg.Text}\n";
                    stream.Write(Encoding.UTF8.GetBytes(line));
                }
                // Я то так позаначаю але ти андрій провірь якшо шо якщо треба буде щось поміняти міняй або я поміняю 
                // можна доречі в дс сидіти і разом то все писати як моральна підримка 

                // ВСІМ ПРИВІТ ЯКЩО ХТОСЬ ТО БУДЕ ДИВИТИСЬ НАПИШЕТЕ ЩОСЬ НИЩЕ В ПРОСТОРІ


                //ЦІКАВО  ПРОСТО


                while (true)
                {
                    bytes = stream.Read(buffer);
                    if (bytes == 0) break;
                    var msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                    Console.WriteLine($"{login}: {msg}");

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
                    

                    //foreach (var c in clients.Where(c => c != client))
                    //{
                    //    try { c.GetStream().Write(Encoding.UTF8.GetBytes(msg + "\n")); }
                    //    catch { clients.Remove(c); c.Close(); }
                    //}
                    else
                    {
                        MasHistory.Add(new Message { Sender = login, Text = msg, Timestamp = DateTime.Now });

                        foreach (var c in clients.Where(c => c != client))
                        {
                            try { c.GetStream().Write(Encoding.UTF8.GetBytes($"{login}: {msg}\n")); }
                            catch { clients.Remove(c); c.Close(); }
                        }
                    }
                }

            }
            else
            {
                stream.Write(Encoding.UTF8.GetBytes("Error"));
                client.Close();
            }

            //clients.Remove(client);
           // client.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client disconnected: {ex.Message}");
        }
        finally
        {
            clients.Remove(client);
            client.Close();
        }

        //egrgrg
        //egerg
        //hello
        // Hi
        //win lox
    }
}