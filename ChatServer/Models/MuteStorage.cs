using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace ChatServer.Models
{
    public static class MuteStorage
    {
        private const string FileName = "mutes.json";

        public static HashSet<string> Load()
        {
            if (!File.Exists(FileName))
                return new HashSet<string>();

            string json = File.ReadAllText(FileName);

            return JsonSerializer.Deserialize<HashSet<string>>(json)
                   ?? new HashSet<string>();
        }

        public static void Save(HashSet<string> mutes)
        {
            File.WriteAllText(
                FileName,
                JsonSerializer.Serialize(
                    mutes,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
        }
    }
}
