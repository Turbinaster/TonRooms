using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TONServer
{
    public static class TonConnectService
    {
        private static readonly HttpClient Client = new HttpClient
        {
            BaseAddress = new Uri("https://tonapi.io")
        };

        public static async Task<string> GetPayloadAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/v2/tonconnect/payload");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _Singleton.Api);

            var response = await Client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"TonConnect payload error: {content}");
            }

            var json = JObject.Parse(content);
            var payload = json["payload"]?.ToString();
            if (string.IsNullOrEmpty(payload))
            {
                throw new Exception("TonConnect payload is empty");
            }

            return payload;
        }

        public static async Task<string> VerifyProofAsync(string address, JObject proof)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/v2/wallet/auth/proof");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _Singleton.Api);

            var body = new JObject
            {
                ["address"] = address,
                ["proof"] = proof
            };

            request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");

            var response = await Client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"TonConnect proof error: {content}");
            }

            var json = JObject.Parse(content);
            return json["token"]?.ToString();
        }
    }
}
