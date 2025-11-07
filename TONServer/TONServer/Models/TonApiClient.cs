using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TONServer
{
    public static class TonApiClient
    {
        private static readonly HttpClient Client;

        static TonApiClient()
        {
            Client = new HttpClient
            {
                BaseAddress = new Uri("https://tonapi.io"),
                Timeout = TimeSpan.FromSeconds(15)
            };
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public static void EnsureClientId(string host)
        {
            if (!string.IsNullOrWhiteSpace(_Singleton.TonApiClientId)) return;
            if (string.IsNullOrWhiteSpace(host)) return;
            try
            {
                var uri = host.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? new Uri(host) : new Uri($"https://{host}");
                var domain = uri.Host.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(domain)) return;
                _Singleton.TonApiClientId = Base32Encode(domain);
            }
            catch
            {
            }
        }

        public static async Task<JObject> SendAsync(HttpRequestMessage request)
        {
            AddAuthHeaders(request);

            using (var response = await Client.SendAsync(request))
            {
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"TonAPI error {(int)response.StatusCode}: {content}");
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    return new JObject();
                }

                try
                {
                    return JObject.Parse(content);
                }
                catch
                {
                    throw new Exception($"TonAPI returned invalid JSON: {content}");
                }
            }
        }

        private static void AddAuthHeaders(HttpRequestMessage request)
        {
            var key = _Singleton.Api;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new Exception("TonAPI key is not configured");
            }

            if (key.Contains("."))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                request.Headers.Remove("X-API-Key");
            }
            else
            {
                request.Headers.Authorization = null;
                request.Headers.Remove("Authorization");
                request.Headers.Remove("X-API-Key");
                request.Headers.TryAddWithoutValidation("X-API-Key", key);
            }

            if (!string.IsNullOrWhiteSpace(_Singleton.TonApiClientId))
            {
                request.Headers.Remove("X-Tonapi-Client");
                request.Headers.TryAddWithoutValidation("X-Tonapi-Client", _Singleton.TonApiClientId);
            }
        }

        private static string Base32Encode(string input)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var bytes = Encoding.UTF8.GetBytes(input);
            var output = new StringBuilder((bytes.Length + 7) * 8 / 5);
            int bits = 0;
            int value = 0;

            foreach (var b in bytes)
            {
                value = (value << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    output.Append(alphabet[(value >> (bits - 5)) & 31]);
                    bits -= 5;
                }
            }

            if (bits > 0)
            {
                output.Append(alphabet[(value << (5 - bits)) & 31]);
            }

            return output.ToString();
        }
    }
}
