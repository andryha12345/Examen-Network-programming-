using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatServer.HistoryPrivate
{
    internal class MasHistory
    {

        // джей сон створюється там де і езе файл 
        private static readonly string FilePath = "messages.json";
        private static readonly object _lock = new object();
        private static List<Message> _messages = new List<Message>();

        static MasHistory()
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _messages = JsonSerializer.Deserialize<List<Message>>(json) ?? new List<Message>();
            }
        }

        public static void Add(Message message)
        {
            lock (_lock)
            {
                _messages.Add(message);
                var json = JsonSerializer.Serialize(_messages, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
        }

        public static List<Message> GetLast(int count = 50)
        {
            lock (_lock)
            {
                return _messages.Skip(Math.Max(0, _messages.Count - count)).ToList();
            }
        }

    }
}
