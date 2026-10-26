using System.Net.Http.Headers;
using System.Text.Json;

namespace UniCore.IntegrationTests.Infrastructure
{
    // File uploads and downloads: the normal ApiClient only sends and reads JSON.
    public sealed class RawHttp(HttpClient http)
    {
        private readonly HttpClient _http = http;

        public async Task<RawResult> PostFileAsync(string url, string? token, string fileName, byte[] bytes, string? contentType, IReadOnlyDictionary<string, string>? fields = null)
        {
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);

            if (contentType is not null)
            {
                file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }

            form.Add(file, "file", fileName);

            foreach (var field in fields ?? new Dictionary<string, string>())
            {
                form.Add(new StringContent(field.Value), field.Key);
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
            return await SendAsync(request, token);
        }

        public async Task<RawResult> GetAsync(string url, string? token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            return await SendAsync(request, token);
        }

        private async Task<RawResult> SendAsync(HttpRequestMessage request, string? token)
        {
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await _http.SendAsync(request);
            var bytes = await response.Content.ReadAsByteArrayAsync();

            var json = default(JsonElement);

            try
            {
                if (bytes.Length > 0 && response.Content.Headers.ContentType?.MediaType is { } type && type.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    json = JsonDocument.Parse(bytes).RootElement.Clone();
                }
            }
            catch (JsonException)
            {
                // Not JSON after all: the caller looks at the bytes.
            }

            response.Headers.TryGetValues("X-Content-Type-Options", out var noSniff);

            return new RawResult((int)response.StatusCode, bytes, response.Content.Headers.ContentType?.MediaType, response.Content.Headers.ContentDisposition?.ToString(), noSniff?.FirstOrDefault(), json);
        }
    }

}
