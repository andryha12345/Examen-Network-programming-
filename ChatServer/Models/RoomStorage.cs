using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace ChatServer.Models
{
    public static class RoomStorage
    {
        private const string FileName = "rooms.json";

        public static List<Room> Load()
        {
            if (!File.Exists(FileName))
                return new List<Room>();

            string json = File.ReadAllText(FileName);

            return JsonSerializer.Deserialize<List<Room>>(json)
                   ?? new List<Room>();
        }

        public static void Save(List<Room> rooms)
        {
            string json =
                JsonSerializer.Serialize(
                    rooms,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

            File.WriteAllText(FileName, json);
        }
    }
}
