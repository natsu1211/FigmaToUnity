namespace FigmaToUnity.Core
{
    public sealed class FigmaApiClientOptions
    {
        public int MaxAttempts { get; set; } = 3;
        public int RequestTimeoutSeconds { get; set; } = 60;
    }
}
