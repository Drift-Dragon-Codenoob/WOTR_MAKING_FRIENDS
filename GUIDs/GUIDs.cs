using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using static UnityModManagerNet.UnityModManager;

namespace WOTR_MAKING_FRIENDS.GUIDs
{
    public static class GetGUID
    {
        public static ModEntry ModEntry;
        public static bool buildOnly = true;
        public static string json;
        private static Dictionary<string, string> registered;

        public static string GUIDByName(string name)
        {
            if (registered == null || !buildOnly)
            {
                var path = Path.Combine(ModEntry.Path, "GUIDs", "GUIDs.json");
                json = File.ReadAllText(path);
                var entries = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var row in JArray.Parse(json))
                {
                    string key = (string)row["name"];
                    string value = (string)row["guid"];
                    if (string.IsNullOrWhiteSpace(key) || !Guid.TryParse(value, out _))
                        throw new InvalidDataException("Invalid Making Friends GUID registry entry: " + key);
                    entries.Add(key, value);
                }
                registered = entries;
            }
            if (registered.TryGetValue(name, out string guid))
                return guid;
            throw new KeyNotFoundException("Missing stable Making Friends blueprint identifier: " + name +
                ". Add it to the source GUID registry; runtime identifiers must not be generated.");
        }
    }
}
