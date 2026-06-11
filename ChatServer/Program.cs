using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ChatServer;

class Program
{
    static List<TcpClient> clients = new();
    static Dictionary<string, string> users = new();

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

            while (true)
            {
                bytes = stream.Read(buffer);
                if (bytes == 0) break;
                var msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                Console.WriteLine($"{login}: {msg}");

                foreach (var c in clients.Where(c => c != client))
                {
                    try { c.GetStream().Write(Encoding.UTF8.GetBytes(msg)); }
                    catch { clients.Remove(c); c.Close(); }
                }
            }
        }
        else
        {
            stream.Write(Encoding.UTF8.GetBytes("Error"));
            client.Close();
        }

        clients.Remove(client);
        client.Close(); //egrgrg
        //egerg
        //hello
        // Hi
    }
}