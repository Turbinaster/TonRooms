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
using System.Threading;
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
        private static readonly HttpClient AssetHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        private static readonly TimeSpan AssetDownloadTimeout = TimeSpan.FromSeconds(30);
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

            AssetHttpClient.DefaultRequestHeaders.Accept.Clear();
            AssetHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            AssetHttpClient.DefaultRequestHeaders.UserAgent.Clear();
            AssetHttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TonRooms", "1.0"));
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
            string host = null;
            string scheme = null;

            if (request != null)
            {
                if (request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost))
                {
                    var forwardedHostValue = forwardedHost.ToString();
                    if (!string.IsNullOrWhiteSpace(forwardedHostValue))
                    {
                        var parts = forwardedHostValue.Split(',');
                        if (parts.Length > 0) host = parts[0].Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(host) && request.Host.HasValue)
                {
                    host = request.Host.Value;
                }

                if (request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto))
                {
                    var forwardedProtoValue = forwardedProto.ToString();
                    if (!string.IsNullOrWhiteSpace(forwardedProtoValue))
                    {
                        var protoParts = forwardedProtoValue.Split(',');
                        if (protoParts.Length > 0) scheme = protoParts[0].Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(scheme))
                {
                    scheme = request.Scheme;
                }
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                host = _Singleton.Host;
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                host = "localhost";
            }

            var normalizedHost = host.Trim().TrimEnd('/');
            var hostLower = normalizedHost.ToLowerInvariant();
            bool isLocal = hostLower.Contains("localhost") || hostLower.StartsWith("127.") || hostLower.StartsWith("::1");

            if (!isLocal)
            {
                scheme = Uri.UriSchemeHttps;
            }
            else if (string.IsNullOrWhiteSpace(scheme))
            {
                scheme = Uri.UriSchemeHttp;
            }

            if (string.IsNullOrWhiteSpace(scheme))
            {
                scheme = Uri.UriSchemeHttps;
            }

            return $"{scheme.ToLowerInvariant()}://{normalizedHost}";
        }

        public static string NormalizeAssetUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;

            var trimmed = url.Trim();
            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                var host = uri.Host;
                var normalizedSingletonHost = NormalizeHostValue(_Singleton.Host);

                if (string.Equals(host, "rooms.worldofton.ru", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(normalizedSingletonHost) &&
                     string.Equals(host, normalizedSingletonHost, StringComparison.OrdinalIgnoreCase)))
                {
                    return "https://" + trimmed.Substring("http://".Length);
                }
            }

            return trimmed;
        }

        private static string NormalizeHostValue(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;
            var value = host.Trim();
            var commaIndex = value.IndexOf(',');
            if (commaIndex >= 0) value = value.Substring(0, commaIndex);
            value = value.Trim();
            if (value.EndsWith("/")) value = value.TrimEnd('/');
            var schemeSeparator = value.IndexOf("://", StringComparison.Ordinal);
            if (schemeSeparator >= 0)
            {
                value = value.Substring(schemeSeparator + 3);
            }

            var portIndex = value.IndexOf(':');
            if (portIndex >= 0)
            {
                value = value.Substring(0, portIndex);
            }

            return value;
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

        protected static async Task<bool> DownloadFileAsync(string url, string path)
        {
            var downloadSucceeded = false;
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var cts = new CancellationTokenSource(AssetDownloadTimeout);
                using var response = await AssetHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    var message = $"Failed to download file from {url}: {(int)response.StatusCode} {response.ReasonPhrase}";
                    LogMessage(LogLevel.Warning, message);
                    Helper.Log(message);
                    return false;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                await using var fileStream = System.IO.File.Create(path);
                await stream.CopyToAsync(fileStream, 81920, cts.Token);
                LogMessage(LogLevel.Information, $"Downloaded asset from {url} to {path} ({response.Content.Headers.ContentLength ?? 0} bytes). ");
                downloadSucceeded = true;
                return true;
            }
            catch (OperationCanceledException oce)
            {
                var message = $"Download timed out for {url}: {oce.Message}";
                LogWarning(message);
                Helper.Log(message);
            }
            catch (Exception ex)
            {
                LogException(ex, $"Failed to download file from {url} to {path}.");
            }

            if (!downloadSucceeded && System.IO.File.Exists(path))
            {
                try { System.IO.File.Delete(path); }
                catch { }
            }

            return false;
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
