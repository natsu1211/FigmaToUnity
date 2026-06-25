using System;

namespace FigmaToUnity.Core
{
    /// <summary>
    /// Thrown from <see cref="FigmaApiClient"/> for request-level failures
    /// (exhausted retries, malformed response bodies). Lets callers route
    /// these to a Figma-specific exit code without parsing message strings.
    /// HTTP-level failures still surface as <see cref="System.Net.Http.HttpRequestException"/>.
    /// </summary>
    public sealed class FigmaApiException : Exception
    {
        public FigmaApiException(string message)
            : base(message)
        {
        }

        public FigmaApiException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
