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

        /// <summary>
        /// When set (via a <c>#use:&lt;path-or-name&gt;</c> manual tag), the node is not
        /// built from its Figma children; instead an existing project prefab matching this
        /// reference is instantiated in its place. Holds either an asset path
        /// (e.g. <c>Assets/UI/MyButton.prefab</c>) or a bare prefab name (e.g. <c>MyButton</c>).
        /// </summary>
        [JsonProperty("externalPrefabPath", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string? ExternalPrefabPath { get; set; }

        [JsonIgnore]
        public bool IsEmpty =>
            Tags.Count == 0 &&
            NodeHash == 0 &&
            !ForceImage &&
            !ForceContainer &&
            !IgnoreNode &&
            !ExplicitPrefab &&
            string.IsNullOrEmpty(ExternalPrefabPath);

        public bool ShouldSerializeTags() => Tags.Count > 0;
    }
}
