using Libs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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
            Timeout = TimeSpan.FromMilliseconds(120000)
        };
        private static readonly object LoggerSync = new object();
        private static readonly object LogFileLock = new object();
        private static readonly object LogPathLock = new object();
        private static ILogger<_Controller> _logger;
        private static string _diagnosticLogPath;

        static _Controller()
        {
            TonApiHttpClient.DefaultRequestHeaders.Accept.Clear();
            TonApiHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            TonApiHttpClient.DefaultRequestHeaders.UserAgent.Clear();
            TonApiHttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TonRooms", "1.0"));
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (_Singleton.Hub == null) _Singleton.Hub = hub;
            _Singleton.WebRootPath = env.WebRootPath;
            _Singleton.Host = Request.Host.ToString();
            EnsureLoggerInitialized();
            EnsureLogFilePath();
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
                LogMessage(LogLevel.Information, $"Resolving TON account identifier for address '{address}'.");
                var response = await GetTonApiJsonAsync($"https://tonapi.io/v2/address/{address}/parse");
                var json = response.Json;
                string accountId = json?["bounceable"]?["b64url"]?.ToString()
                    ?? json?["non_bounceable"]?["b64url"]?.ToString()
                    ?? json?["raw_form"]?.ToString();

                if (!string.IsNullOrWhiteSpace(accountId))
                {
                    LogMessage(LogLevel.Information, $"Resolved TON address '{address}' to '{accountId}'.");
                    return accountId;
                }

                if (!string.IsNullOrWhiteSpace(response.RawContent))
                {
                    var truncated = TruncateForLog(response.RawContent);
                    var message = $"Unable to resolve TON address '{address}'. TON API status: {(int?)response.StatusCode} {response.StatusCode}. Body: {truncated}";
                    LogMessage(LogLevel.Warning, message);
                    Helper.Log(message);
                }
            }
            catch (Exception ex)
            {
                LogException(ex, $"Failed to resolve TON account identifier for address '{address}'.");
            }

            return address;
        }

        protected static async Task<TonApiResponse> GetTonApiJsonAsync(string url)
        {
            var responseInfo = new TonApiResponse();
            var sw = Stopwatch.StartNew();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _Singleton.Api);

                using var response = await TonApiHttpClient.SendAsync(request);
                responseInfo.StatusCode = response.StatusCode;
                responseInfo.IsSuccessStatusCode = response.IsSuccessStatusCode;
                var content = await response.Content.ReadAsStringAsync();
                responseInfo.RawContent = content;
                sw.Stop();
                responseInfo.Duration = sw.Elapsed;
                LogMessage(LogLevel.Information, $"TON API GET {url} responded {(int)response.StatusCode} {response.StatusCode} in {sw.ElapsedMilliseconds} ms.");

                if (!response.IsSuccessStatusCode)
                {
                    var truncated = TruncateForLog(content);
                    var message = $"TON API request to {url} failed with {(int)response.StatusCode} {response.ReasonPhrase}. Body: {truncated}";
                    LogMessage(LogLevel.Warning, message);
                    Helper.Log(message);
                    return responseInfo;
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    var message = $"TON API request to {url} returned empty body.";
                    LogMessage(LogLevel.Warning, message);
                    Helper.Log(message);
                    return responseInfo;
                }

                try
                {
                    responseInfo.Json = JToken.Parse(content);
                    return responseInfo;
                }
                catch (Exception parseEx)
                {
                    var truncated = TruncateForLog(content);
                    var message = $"Failed to parse TON API response for {url}: {truncated}";
                    LogMessage(LogLevel.Error, message, parseEx);
                    Helper.Log(message);
                    Helper.Log(parseEx);
                    return responseInfo;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                LogException(ex, $"TON API request to {url} failed after {sw.ElapsedMilliseconds} ms.");
                responseInfo.Duration = sw.Elapsed;
                return responseInfo;
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
                    var message = $"Failed to download file from {url}: {(int)response.StatusCode} {response.ReasonPhrase}";
                    LogMessage(LogLevel.Warning, message);
                    Helper.Log(message);
                    return;
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = System.IO.File.Create(path);
                await stream.CopyToAsync(fileStream);
                LogMessage(LogLevel.Information, $"Downloaded asset from {url} to {path} ({response.Content.Headers.ContentLength ?? 0} bytes). ");
            }
            catch (Exception ex)
            {
                LogException(ex, $"Failed to download file from {url} to {path}.");
            }
        }

        protected static void LogException(Exception exception, string contextMessage = null)
        {
            if (exception == null) return;
            var message = contextMessage ?? exception.Message;
            LogMessage(LogLevel.Error, message, exception);
            try
            {
                Helper.Log(exception);
                if (!string.IsNullOrWhiteSpace(contextMessage)) Helper.Log(contextMessage);
            }
            catch { }
        }

        private void EnsureLoggerInitialized()
        {
            if (_logger != null) return;
            var resolvedLogger = HttpContext?.RequestServices?.GetService(typeof(ILogger<_Controller>)) as ILogger<_Controller>;
            if (resolvedLogger == null) return;
            lock (LoggerSync)
            {
                if (_logger == null) _logger = resolvedLogger;
            }
        }

        private static void EnsureLogFilePath()
        {
            if (!string.IsNullOrEmpty(_diagnosticLogPath) || string.IsNullOrEmpty(_Singleton.WebRootPath)) return;
            try
            {
                lock (LogPathLock)
                {
                    if (!string.IsNullOrEmpty(_diagnosticLogPath)) return;
                    var directory = Path.Combine(_Singleton.WebRootPath, "logs");
                    Directory.CreateDirectory(directory);
                    _diagnosticLogPath = Path.Combine(directory, "tonrooms.log");
                    LogMessage(LogLevel.Information, $"Diagnostic log file initialised at {_diagnosticLogPath}.");
                }
            }
            catch (Exception ex)
            {
                LogMessage(LogLevel.Warning, "Unable to initialise diagnostic log directory.", ex);
            }
        }

        protected static void LogInformation(string message) => LogMessage(LogLevel.Information, message);

        protected static void LogWarning(string message) => LogMessage(LogLevel.Warning, message);

        private static void LogMessage(LogLevel level, string message, Exception exception = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    var line = $"{DateTime.UtcNow:O} [{level}] {message}";
                    Console.WriteLine(line);
                    WriteLogLine(line);
                }

                if (exception != null)
                {
                    Console.WriteLine(exception.ToString());
                    WriteLogLine(exception.ToString());
                }

                _logger?.Log(level, exception, message ?? exception?.Message);
            }
            catch
            {
                // Ignore logging failures to avoid affecting request execution.
            }
        }

        private static void WriteLogLine(string line)
        {
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(_diagnosticLogPath)) return;
            try
            {
                lock (LogFileLock)
                {
                    System.IO.File.AppendAllText(_diagnosticLogPath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Swallow logging IO errors.
            }
        }

        private static string TruncateForLog(string value, int maxLength = 2048)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }

        protected class TonApiResponse
        {
            public JToken Json { get; set; }
            public string RawContent { get; set; }
            public HttpStatusCode? StatusCode { get; set; }
            public bool IsSuccessStatusCode { get; set; }
            public TimeSpan Duration { get; set; }
        }
    }
}
