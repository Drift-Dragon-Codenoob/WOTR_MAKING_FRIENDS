using System;
using System.Linq;
using System.IO;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Newtonsoft.Json;

namespace WOTR_MAKING_FRIENDS.Synthesist.Blueprints
{
    internal static class IsolatedCopy
    {
        internal static BlueprintComponent[] Components(BlueprintScriptableObject source, Func<BlueprintComponent, bool> include)
        {
            var previous = Json.BlueprintBeingRead;
            Json.BlueprintBeingRead = new BlueprintJsonWrapper(source);
            try { return source.ComponentsArray.Where(include).Select(Clone).ToArray(); }
            finally { Json.BlueprintBeingRead = previous; }
        }

        private static BlueprintComponent Clone(BlueprintComponent component)
        {
            // Use text readers: the game's enum converter expects the integer width
            // supplied by JsonTextReader, which differs from JTokenReader's boxed values.
            using var buffer = new StringWriter();
            using (var writer = new JsonTextWriter(buffer)) Json.Serializer.Serialize(writer, component, component.GetType());
            using var reader = new JsonTextReader(new StringReader(buffer.ToString()));
            return (BlueprintComponent)Json.Serializer.Deserialize(reader, component.GetType());
        }
    }
}
