using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FigmaToUnity.Core
{
    public sealed class NodeAnnotations
    {
        [JsonProperty("tags", ItemConverterType = typeof(StringEnumConverter))]
        public HashSet<NodeTag> Tags { get; } = new();

        [JsonProperty("nodeHash", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public int NodeHash { get; set; }

        [JsonProperty("forceImage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool ForceImage { get; set; }

        [JsonProperty("forceContainer", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool ForceContainer { get; set; }

        [JsonProperty("ignoreNode", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool IgnoreNode { get; set; }

        [JsonProperty("explicitPrefab", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool ExplicitPrefab { get; set; }

        [JsonIgnore]
        public bool IsEmpty =>
            Tags.Count == 0 &&
            NodeHash == 0 &&
            !ForceImage &&
            !ForceContainer &&
            !IgnoreNode &&
            !ExplicitPrefab;

        public bool ShouldSerializeTags() => Tags.Count > 0;
    }
}
