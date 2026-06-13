using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatServer
{
    public class Room
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string CreatedBy { get; set; }

        public Room(int id, string name, string createdBy)
        {
            Id = id;
            Name = name;
            CreatedBy = createdBy;
        }
    }
}
