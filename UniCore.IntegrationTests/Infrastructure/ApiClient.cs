using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniCore.IntegrationTests.Infrastructure
{
    public sealed class ApiClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _http;

        public ApiClient(HttpClient http)
        {
            _http = http;
        }

        public Task<ApiResponse> GetAsync(string url, string? token = null)
            => SendAsync(HttpMethod.Get, url, null, token);

        public Task<ApiResponse> PostAsync(string url, object? body = null, string? token = null)
            => SendAsync(HttpMethod.Post, url, body, token);

        public Task<ApiResponse> PutAsync(string url, object? body = null, string? token = null)
            => SendAsync(HttpMethod.Put, url, body, token);

        public Task<ApiResponse> PatchAsync(string url, object? body = null, string? token = null)
            => SendAsync(HttpMethod.Patch, url, body, token);

        public Task<ApiResponse> DeleteAsync(string url, string? token = null)
            => SendAsync(HttpMethod.Delete, url, null, token);

        public Task<ApiResponse> PostAsync(string url, object? body, string? token, IReadOnlyDictionary<string, string> headers)
            => SendAsync(HttpMethod.Post, url, body, token, headers);

        public async Task<ApiResponse> SendAsync(HttpMethod method, string url, object? body, string? token, IReadOnlyDictionary<string, string>? headers = null)
        {
            using var request = new HttpRequestMessage(method, url);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType(), null, JsonOptions);
            }

            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            if (headers is not null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }


            using var response = await _http.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();

            var element = default(JsonElement);

            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    using var document = JsonDocument.Parse(raw);
                    element = document.RootElement.Clone();
                }
                catch (JsonException)
                {
                    // Not JSON (an empty or text body): tests only look at the status code then.
                }
            }

            return new ApiResponse(response.StatusCode, element, raw);
        }
    }

}
