using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatServer.HistoryPrivate
{
    internal class Message
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public string Sender { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }

    }
}
