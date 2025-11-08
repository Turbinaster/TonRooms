using Libs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace TONServer
{
    public class _Controller : Controller
    {
        public _DbContext db { get { return (_DbContext)HttpContext.RequestServices.GetService(typeof(_DbContext)); } }
        public IHubContext<_Hub> hub { get { return (IHubContext<_Hub>)HttpContext.RequestServices.GetService(typeof(IHubContext<_Hub>)); } }
        public IWebHostEnvironment env { get { return (IWebHostEnvironment)HttpContext.RequestServices.GetService(typeof(IWebHostEnvironment)); } }
        public User user;
        public Lang lang;
        public string session;

        private static readonly HttpClient TonApiHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(300000)
        };

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (_Singleton.Hub == null) _Singleton.Hub = hub;
            _Singleton.WebRootPath = env.WebRootPath;
            _Singleton.Host = Request.Host.ToString();
            if (User.Identity.IsAuthenticated)
            {
                user = GetSession<User>("user");
                if (user == null)
                {
                    user = db.Users.Find(Helper.IntParse(User.Identity.Name));
                    SetSession("user", user);
                }
                ViewBag.user = user;
            }

            var route = Request.Path.Value;
            if (route.StartsWith("/admin"))
            {
                if (user == null || user.Role != "admin")
                {
                    if (Request.Method == "GET") { Response.Redirect(Url.Action("index", "index")); return; }
                    else throw new Exception("Access denied");
                }
            }

            if (!HttpContext.Session.Keys.Contains("lang"))
            {
                SetSession("session", Helper.RandomString(20));
                SetSession("lang", LangRep.Lang(user, Request, out var langS));
                SetSession("langS", langS);
                SetSession("lds", LangRep.LangDescs());
            }
            lang = GetSession<Lang>("lang");
            session = GetSession<string>("session");
            ViewBag.lang = lang;
            ViewBag.session = session;
            ViewBag.langS = GetSession<string>("langS");
            ViewBag.lds = GetSession<List<LangDesc>>("lds");

            base.OnActionExecuting(context);
        }

        protected void SetSession<T>(string key, T value)
        {
            HttpContext.Session.SetString(key, JsonConvert.SerializeObject(value));
        }

        protected T GetSession<T>(string key)
        {
            var value = HttpContext.Session.GetString(key);
            return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
        }

        public static string GetLeftPart(HttpRequest request)
        {
            return $"{request.Scheme}://{request.Host.Value}";
        }

        protected string RenderPartialViewToString(string viewName, object model = null)
        {
            if (string.IsNullOrEmpty(viewName)) viewName = ControllerContext.ActionDescriptor.DisplayName;

            ViewData.Model = model;

            using (StringWriter sw = new StringWriter())
            {
                var engine = (ICompositeViewEngine)HttpContext.RequestServices.GetService(typeof(ICompositeViewEngine));
                ViewEngineResult viewResult = engine.FindView(ControllerContext, viewName, false);

                ViewContext viewContext = new ViewContext(
                    ControllerContext,
                    viewResult.View,
                    ViewData,
                    TempData,
                    sw,
                    new HtmlHelperOptions() //Added this parameter in
                );

                //Everything is async now!
                var t = viewResult.View.RenderAsync(viewContext);
                t.Wait();

                return sw.GetStringBuilder().ToString();
            }
        }

        protected void SetSessionAddress(string sessionKey, string address)
        {
            if (string.IsNullOrWhiteSpace(sessionKey))
            {
                throw new ArgumentException("Session key is required", nameof(sessionKey));
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Wallet address is required", nameof(address));
            }

            if (!_Singleton.Sessions.ContainsKey(sessionKey))
            {
                _Singleton.Sessions.Add(sessionKey, address);
            }
            else
            {
                _Singleton.Sessions[sessionKey] = address;
            }
        }

        protected static async Task<string> ResolveTonAccountIdAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return address ?? string.Empty;
            }

            try
            {
                var (parsed, rawContent) = await GetTonApiJsonAsync($"https://tonapi.io/v2/address/{address}/parse");
                string accountId = parsed?["bounceable"]?["b64url"]?.ToString()
                    ?? parsed?["non_bounceable"]?["b64url"]?.ToString()
                    ?? parsed?["raw_form"]?.ToString();

                if (!string.IsNullOrWhiteSpace(accountId))
                {
                    return accountId;
                }

                if (!string.IsNullOrWhiteSpace(rawContent))
                {
                    Helper.Log($"Unable to resolve TON address '{address}'. Response: {rawContent}");
                }
            }
            catch (Exception ex)
            {
                Helper.Log(ex);
            }

            return address;
        }

        protected static async Task<(JToken Json, string RawContent)> GetTonApiJsonAsync(string url)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _Singleton.Api);

                using var response = await TonApiHttpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Helper.Log($"TON API request to {url} failed with {(int)response.StatusCode} {response.ReasonPhrase}. Body: {content}");
                    return (null, content);
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    Helper.Log($"TON API request to {url} returned empty body.");
                    return (null, content);
                }

                try
                {
                    var json = JToken.Parse(content);
                    return (json, content);
                }
                catch (Exception parseEx)
                {
                    Helper.Log($"Failed to parse TON API response for {url}: {content}");
                    Helper.Log(parseEx);
                    return (null, content);
                }
            }
            catch (Exception ex)
            {
                Helper.Log(ex);
                return (null, null);
            }
        }

        protected static async Task DownloadFileAsync(string url, string path)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var response = await TonApiHttpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Helper.Log($"Failed to download file from {url}: {(int)response.StatusCode} {response.ReasonPhrase}");
                    return;
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = System.IO.File.Create(path);
                await stream.CopyToAsync(fileStream);
            }
            catch (Exception ex)
            {
                Helper.Log(ex);
            }
        }
    }
}
