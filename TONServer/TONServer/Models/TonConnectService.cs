using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TONServer
{
    public class TonConnectPayloadResult
    {
        public string Payload { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public TonConnectDeepLinks Links { get; set; }
    }

    public class TonConnectDeepLinks
    {
        public string TonkeeperUniversal { get; set; }
        public string TonkeeperDeeplink { get; set; }
        public string TonDeeplink { get; set; }
    }

    public class TonConnectProofResult
    {
        public string Token { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }

    public static class TonConnectService
    {
        public static async Task<TonConnectPayloadResult> GetPayloadAsync(string manifestUrl)
        {
            TonApiClient.EnsureClientId(manifestUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, "/v2/tonconnect/payload");
            request.Headers.TryAddWithoutValidation("X-Ton-Connect-Manifest-Url", manifestUrl);
            request.Headers.TryAddWithoutValidation("x-ton-connect-manifest-url", manifestUrl);
            var json = await TonApiClient.SendAsync(request);

            var payload = json.Value<string>("payload");
            if (string.IsNullOrEmpty(payload))
            {
                throw new Exception("TonConnect payload is empty");
            }

            var expiresAt = ParseExpiration(json);
            var encoded = BuildConnectRequest(manifestUrl, payload);

            return new TonConnectPayloadResult
            {
                Payload = payload,
                ExpiresAt = expiresAt,
                Links = new TonConnectDeepLinks
                {
                    TonkeeperUniversal = $"https://app.tonkeeper.com/ton-connect/v2/?connect={encoded}",
                    TonkeeperDeeplink = $"tonkeeper://ton-connect/v2/?connect={encoded}",
                    TonDeeplink = $"ton://connect/v2/?connect={encoded}"
                }
            };
        }

        public static async Task<TonConnectProofResult> VerifyProofAsync(string address, JObject proof)
        {
            var body = new JObject
            {
                ["address"] = address,
                ["proof"] = proof
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/v2/wallet/auth/proof")
            {
                Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json")
            };

            var json = await TonApiClient.SendAsync(request);
            var token = json.Value<string>("token");
            if (string.IsNullOrEmpty(token))
            {
                throw new Exception("TonConnect proof verification failed");
            }

            return new TonConnectProofResult
            {
                Token = token,
                ExpiresAt = ParseExpiration(json)
            };
        }

        private static string BuildConnectRequest(string manifestUrl, string payload)
        {
            var manifest = manifestUrl;
            if (!manifest.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("TonConnect manifest URL must be absolute");
            }

            var request = new JObject
            {
                ["manifestUrl"] = manifest,
                ["items"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "ton_proof",
                        ["payload"] = payload
                    }
                }
            };

            var bytes = Encoding.UTF8.GetBytes(request.ToString(Formatting.None));
            return Base64UrlEncode(bytes);
        }

        private static DateTimeOffset? ParseExpiration(JObject json)
        {
            var expiresToken = json["expires_at"] ?? json["expiresAt"] ?? json["expiration"] ?? json["expire_at"] ?? json["expireAt"];
            if (expiresToken == null) return null;

            if (expiresToken.Type == JTokenType.Integer || expiresToken.Type == JTokenType.Float)
            {
                var value = expiresToken.Value<long>();
                if (value > 1000000000)
                {
                    return DateTimeOffset.FromUnixTimeSeconds(value);
                }

                return DateTimeOffset.UtcNow.AddSeconds(value);
            }

            if (DateTimeOffset.TryParse(expiresToken.ToString(), out var dt))
            {
                return dt.ToUniversalTime();
            }

            return null;
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            var value = Convert.ToBase64String(bytes);
            return value.Replace('+', '-').Replace('/', '_').Replace("=", string.Empty);
        }
    }
}
