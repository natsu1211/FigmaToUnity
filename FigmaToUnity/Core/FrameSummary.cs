namespace FigmaToUnity.Core
{
    public readonly struct FrameSummary
    {
        public FrameSummary(string id, string name, float width, float height)
        {
            Id = id;
            Name = name;
            Width = width;
            Height = height;
        }

        public string Id { get; }
        public string Name { get; }
        public float Width { get; }
        public float Height { get; }
    }
}
