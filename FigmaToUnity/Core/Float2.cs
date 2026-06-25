using Newtonsoft.Json;

namespace FigmaToUnity.Core
{
    public struct Float2
    {
        [JsonProperty("x")]
        public float X;

        [JsonProperty("y")]
        public float Y;

        public Float2(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
