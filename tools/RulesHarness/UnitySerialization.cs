// The portable rule runner implements Unity's public-field JSON contract.
// It deliberately provides no rendering, input, physics, or lifecycle simulation.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
namespace UnityEngine
{
    public static class JsonUtility
    {
        private static JsonSerializerOptions Options(bool pretty = false)
        {
            var resolver = new DefaultJsonTypeInfoResolver();
            resolver.Modifiers.Add(info => {
                if (info.Kind != JsonTypeInfoKind.Object) return;
                for (int i = info.Properties.Count - 1; i >= 0; i--)
                    if (!(info.Properties[i].AttributeProvider is FieldInfo field) || field.IsInitOnly || field.IsDefined(typeof(NonSerializedAttribute)))
                        info.Properties.RemoveAt(i);
            });
            return new JsonSerializerOptions { IncludeFields = true, TypeInfoResolver = resolver, WriteIndented = pretty };
        }
        public static string ToJson(object value, bool pretty = false) { return JsonSerializer.Serialize(value, Options(pretty)); }
        public static T FromJson<T>(string json) { return JsonSerializer.Deserialize<T>(json, Options()); }
    }
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        public static bool HasKey(string key) { return Values.ContainsKey(key); }
        public static string GetString(string key) { return Values.TryGetValue(key, out string v) ? v : ""; }
        public static void SetString(string key, string value) { Values[key] = value; }
        public static void DeleteKey(string key) { Values.Remove(key); }
        public static void Save() { }
    }
    public static class Debug { public static void LogWarning(object message) { Console.Error.WriteLine(message); } }
}
