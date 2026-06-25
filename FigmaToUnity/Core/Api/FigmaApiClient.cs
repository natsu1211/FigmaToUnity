using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FigmaToUnity.Core
{
    public sealed class FigmaApiClient
    {
        private const string BaseUrl = "https://api.figma.com/v1";

        private readonly HttpClient _http;
        private readonly FigmaApiClientOptions _options;

        public FigmaApiClient() : this(new FigmaApiClientOptions())
        {
        }

        public FigmaApiClient(FigmaApiClientOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds))
            };
        }

        /// <summary>
        /// Optional callback invoked with human-readable diagnostic messages
        /// (rate-limit waits, timeouts, retries). Lets callers surface what
        /// the client is doing instead of the user staring at a stuck UI.
        /// </summary>
        public Action<string>? DiagnosticLog { get; set; }

        private void LogDiagnostic(string message)
        {
            DiagnosticLog?.Invoke(message);
        }

        public async Task<FigmaUserResponse> VerifyTokenAsync(string token, CancellationToken cancellationToken)
        {
            return await GetJsonAsync<FigmaUserResponse>($"{BaseUrl}/me", token, cancellationToken);
        }

        public async Task<FigmaFileResponse> FetchFileAsync(string token, string fileKey, int depth, CancellationToken cancellationToken)
        {
            string url = $"{BaseUrl}/files/{fileKey}?depth={depth}";
            return await GetJsonAsync<FigmaFileResponse>(url, token, cancellationToken);
        }

        public async Task<FigmaFileResponse> FetchFileMetadataAsync(string token, string fileKey, CancellationToken cancellationToken)
        {
            return await FetchFileAsync(token, fileKey, 1, cancellationToken);
        }

        public async Task<FigmaFileResponse> FetchNodesAsync(string token, string fileKey, IReadOnlyList<string> nodeIds, CancellationToken cancellationToken)
        {
            string ids = string.Join(",", nodeIds);
            string url = $"{BaseUrl}/files/{fileKey}/nodes?ids={ids}";
            return await GetJsonAsync<FigmaFileResponse>(url, token, cancellationToken);
        }

        public async Task<FigmaImageUrlsResponse> FetchImageUrlsAsync(string token, string fileKey, IReadOnlyList<string> nodeIds, int scale, CancellationToken cancellationToken)
        {
            string ids = string.Join(",", nodeIds);
            string url = $"{BaseUrl}/images/{fileKey}?ids={ids}&format=png&scale={scale.ToString(CultureInfo.InvariantCulture)}";
            return await GetJsonAsync<FigmaImageUrlsResponse>(url, token, cancellationToken);
        }

        public async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken)
        {
            int maxAttempts = Math.Max(1, _options.MaxAttempts);
            string endpoint = TrimEndpoint(url);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using HttpRequestMessage request = new(HttpMethod.Get, url);
                HttpResponseMessage? response = await SendWithRetryAsync(request, endpoint, attempt, maxAttempts, cancellationToken);
                if (response == null)
                {
                    // 429 handled; retry.
                    continue;
                }

                try
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsByteArrayAsync();
                }
                finally
                {
                    response.Dispose();
                }
            }

            throw new FigmaApiException($"Failed to download bytes from '{url}' after {maxAttempts} attempt(s).");
        }

        private async Task<T> GetJsonAsync<T>(string url, string token, CancellationToken cancellationToken)
        {
            int maxAttempts = Math.Max(1, _options.MaxAttempts);
            string endpoint = TrimEndpoint(url);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using HttpRequestMessage request = new(HttpMethod.Get, url);
                request.Headers.Add("X-Figma-Token", token);

                HttpResponseMessage? response = await SendWithRetryAsync(request, endpoint, attempt, maxAttempts, cancellationToken);
                if (response == null)
                {
                    // 429 handled; retry.
                    continue;
                }

                try
                {
                    response.EnsureSuccessStatusCode();
                    string json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        T? result = JsonConvert.DeserializeObject<T>(json);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
                finally
                {
                    response.Dispose();
                }
            }

            throw new FigmaApiException($"Failed to deserialize Figma response from '{url}' after {maxAttempts} attempt(s).");
        }

        /// <summary>
        /// Sends the request. Returns null if the server returned 429 (rate-limited) so the caller loops to the next attempt.
        /// Otherwise returns the response message; the caller is responsible for disposing it.
        /// </summary>
        private async Task<HttpResponseMessage?> SendWithRetryAsync(HttpRequestMessage request, string endpoint, int attempt, int maxAttempts, CancellationToken cancellationToken)
        {
            DateTime started = DateTime.UtcNow;
            HttpResponseMessage response;
            try
            {
                LogDiagnostic($"HTTP {request.Method} {endpoint} attempt {attempt}/{maxAttempts} (timeout={_http.Timeout.TotalSeconds:0}s).");
                response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                double elapsed = (DateTime.UtcNow - started).TotalSeconds;
                LogDiagnostic($"HTTP {request.Method} {endpoint} attempt {attempt}/{maxAttempts} timed out after {elapsed:0.0}s. The Figma server did not respond within {_http.Timeout.TotalSeconds:0}s.");
                throw;
            }
            catch (HttpRequestException ex)
            {
                LogDiagnostic($"HTTP {request.Method} {endpoint} attempt {attempt}/{maxAttempts} network error: {ex.Message}");
                throw;
            }

            if (response.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
            {
                return response;
            }

            TimeSpan waitTime = ExtractRetryAfter(response);
            string limitType = GetHeaderValue(response, "X-Figma-Rate-Limit-Type");
            string planTier = GetHeaderValue(response, "X-Figma-Plan-Tier");
            string upgradeLink = GetHeaderValue(response, "X-Figma-Upgrade-Link");
            response.Dispose();

            // Per Figma docs (developers.figma.com/docs/rest-api/rate-limits):
            //   X-Figma-Rate-Limit-Type: "low"  = View/Collab seat (Tier 1: 6/month)
            //                            "high" = Dev/Full seat   (Tier 1: 10–20/min by plan)
            // Surface this so the user can immediately tell if their token is on
            // a tiny-quota seat versus actually exhausting a per-minute budget.
            string seatHint = limitType switch
            {
                "low" => " — seat=View/Collab (Tier1 budget: ~6/MONTH)",
                "high" => " — seat=Dev/Full (Tier1 budget: 10–20/min depending on plan)",
                _ => " — no X-Figma-Rate-Limit-Type header (likely CloudFront edge limit, not Figma app-level — try slowing requests or different IP/network)"
            };
            string planHint = string.IsNullOrEmpty(planTier) ? string.Empty : $", plan={planTier}";
            string waitHint = waitTime.TotalSeconds >= 60
                ? $" — Retry-After is {waitTime.TotalSeconds:0}s; this usually means the monthly/daily quota is exhausted, not a transient minute-window spike."
                : string.Empty;
            string upgradeHint = string.IsNullOrEmpty(upgradeLink) ? string.Empty : $" Upgrade info: {upgradeLink}";

            LogDiagnostic(
                $"HTTP {request.Method} {endpoint} returned 429 (rate-limited) on attempt {attempt}/{maxAttempts}{seatHint}{planHint}. " +
                $"Waiting {waitTime.TotalSeconds:0}s before retry.{waitHint}{upgradeHint}");

            if (waitTime > TimeSpan.Zero)
            {
                await Task.Delay(waitTime, cancellationToken);
            }

            return null;
        }

        private static string GetHeaderValue(HttpResponseMessage response, string headerName)
        {
            if (response.Headers.TryGetValues(headerName, out IEnumerable<string>? values))
            {
                foreach (string value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }

            return string.Empty;
        }

        private static string TrimEndpoint(string url)
        {
            int queryIdx = url.IndexOf('?');
            string path = queryIdx >= 0 ? url.Substring(0, queryIdx) : url;
            int baseIdx = path.IndexOf("/v1/", StringComparison.Ordinal);
            return baseIdx >= 0 ? path.Substring(baseIdx + 3) : path;
        }

        private static TimeSpan ExtractRetryAfter(HttpResponseMessage response)
        {
            RetryConditionHeaderValue? header = response.Headers.RetryAfter;
            if (header == null)
            {
                return TimeSpan.Zero;
            }

            if (header.Delta.HasValue && header.Delta.Value > TimeSpan.Zero)
            {
                return header.Delta.Value;
            }

            if (header.Date.HasValue)
            {
                TimeSpan diff = header.Date.Value - DateTimeOffset.UtcNow;
                return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
            }

            return TimeSpan.Zero;
        }
    }
}
