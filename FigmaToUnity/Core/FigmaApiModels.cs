using System.Collections.Generic;
using Newtonsoft.Json;

namespace FigmaToUnity.Core
{
    public sealed class FigmaFileResponse
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("lastModified")]
        public string? LastModified { get; set; }

        [JsonProperty("version")]
        public string? Version { get; set; }

        [JsonProperty("document")]
        public DesignNode Document { get; set; } = new();

        [JsonProperty("nodes")]
        public Dictionary<string, FigmaNodeContainer>? Nodes { get; set; }

        [JsonProperty("components")]
        public Dictionary<string, FigmaComponent>? Components { get; set; }
    }

    public sealed class FigmaNodeContainer
    {
        [JsonProperty("document")]
        public DesignNode Document { get; set; } = new();
    }

    public sealed class FigmaComponent
    {
        [JsonProperty("key")]
        public string? Key { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }
    }

    public sealed class FigmaUserResponse
    {
        [JsonProperty("handle")]
        public string? Handle { get; set; }

        [JsonProperty("email")]
        public string? Email { get; set; }
    }

    public sealed class FigmaImageUrlsResponse
    {
        [JsonProperty("images")]
        public Dictionary<string, string>? Images { get; set; }
    }
}
