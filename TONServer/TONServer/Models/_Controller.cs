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
            if (request == null) return string.Empty;

            string forwardedHost = GetForwardedComponent(request, "host");
            if (string.IsNullOrWhiteSpace(forwardedHost) && request.Headers.TryGetValue("X-Forwarded-Host", out var xForwardedHost))
            {
                forwardedHost = ExtractFirstHeaderValue(xForwardedHost);
            }

            string host = !string.IsNullOrWhiteSpace(forwardedHost) ? forwardedHost : request.Host.Value;

            string forwardedProto = GetForwardedComponent(request, "proto");
            if (string.IsNullOrWhiteSpace(forwardedProto))
            {
                if (request.Headers.TryGetValue("X-Forwarded-Proto", out var protoValues))
                {
                    forwardedProto = ExtractFirstHeaderValue(protoValues);
                }
                else if (request.Headers.TryGetValue("X-Forwarded-Scheme", out var schemeValues))
                {
                    forwardedProto = ExtractFirstHeaderValue(schemeValues);
                }
            }

            string scheme = !string.IsNullOrWhiteSpace(forwardedProto) ? forwardedProto : request.Scheme;
            if (string.IsNullOrWhiteSpace(scheme)) scheme = Uri.UriSchemeHttps;

            if (string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && !IsLocalHost(host))
            {
                scheme = Uri.UriSchemeHttps;
            }

            return $"{scheme}://{host}";
        }

        public static string NormalizeAssetUrl(HttpRequest request, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;

            var trimmed = url.Trim();

            if (TryUpgradeAssetUrl(request, trimmed, out var upgraded))
            {
                return upgraded;
            }

            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                trimmed.IndexOf("worldofton.ru", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var https = "https://" + trimmed.Substring("http://".Length);
                if (!string.Equals(trimmed, https, StringComparison.Ordinal))
                {
                    LogInformation($"Normalized worldofton asset URL via fallback: '{trimmed}' -> '{https}'.");
                }
                return https;
            }

            return trimmed;
        }

        private static bool TryUpgradeAssetUrl(HttpRequest request, string url, out string upgraded)
        {
            upgraded = url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)) return false;

            var candidateHosts = new List<string>();

            string forwardedHost = GetForwardedComponent(request, "host");
            if (!string.IsNullOrWhiteSpace(forwardedHost)) candidateHosts.Add(NormalizeHostForComparison(forwardedHost));

            if (request?.Headers.TryGetValue("X-Forwarded-Host", out var xForwardedHost) == true)
            {
                var value = ExtractFirstHeaderValue(xForwardedHost);
                if (!string.IsNullOrWhiteSpace(value)) candidateHosts.Add(NormalizeHostForComparison(value));
            }

            if (request != null && request.Host.HasValue)
            {
                candidateHosts.Add(NormalizeHostForComparison(request.Host.Value));
            }

            if (!string.IsNullOrWhiteSpace(_Singleton.Host))
            {
                candidateHosts.Add(NormalizeHostForComparison(_Singleton.Host));
            }

            candidateHosts = candidateHosts.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var uriHostNormalized = NormalizeHostForComparison(uri.Host);

            if (!candidateHosts.Any())
            {
                if (!string.IsNullOrWhiteSpace(uriHostNormalized) &&
                    uriHostNormalized.EndsWith(".worldofton.ru", StringComparison.OrdinalIgnoreCase))
                {
                    candidateHosts.Add(uriHostNormalized);
                }
            }

            if (candidateHosts.Any(host => string.Equals(host, uri.Host, StringComparison.OrdinalIgnoreCase) ||
                                            (!string.IsNullOrWhiteSpace(uriHostNormalized) &&
                                             string.Equals(host, uriHostNormalized, StringComparison.OrdinalIgnoreCase))))
            {
                upgraded = BuildHttpsUrl(uri);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(uriHostNormalized) &&
                uriHostNormalized.IndexOf("worldofton.ru", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                upgraded = BuildHttpsUrl(uri);
                return true;
            }

            return false;
        }

        private static string BuildHttpsUrl(Uri uri)
        {
            if (uri == null) return null;

            var builder = new UriBuilder(uri)
            {
                Scheme = Uri.UriSchemeHttps,
                Port = -1
            };

            return builder.Uri.ToString();
        }

        private static string GetForwardedComponent(HttpRequest request, string key)
        {
            if (request?.Headers.TryGetValue("Forwarded", out var forwardedValues) != true) return null;

            foreach (var rawValue in forwardedValues)
            {
                if (string.IsNullOrWhiteSpace(rawValue)) continue;

                var segments = rawValue.Split(';');
                foreach (var segment in segments)
                {
                    var trimmed = segment.Trim();
                    if (trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                    {
                        var valuePart = trimmed.Substring(key.Length + 1).Trim().Trim('"');
                        if (!string.IsNullOrWhiteSpace(valuePart)) return valuePart;
                    }
                }
            }

            return null;
        }

        private static string ExtractFirstHeaderValue(Microsoft.Extensions.Primitives.StringValues values)
        {
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                var first = value.Split(',').FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first)) return first.Trim();
            }
            return null;
        }

        private static bool IsLocalHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return true;
            var comparisonHost = NormalizeHostForComparison(host);
            if (string.IsNullOrWhiteSpace(comparisonHost)) return true;

            return comparisonHost.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || comparisonHost.Equals("127.0.0.1")
                || comparisonHost.Equals("::1")
                || comparisonHost.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeHostForComparison(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;
            var normalized = host.Trim().Trim('"');
            var commaIndex = normalized.IndexOf(',');
            if (commaIndex >= 0) normalized = normalized.Substring(0, commaIndex);
            normalized = normalized.Trim();
            if (normalized.Length == 0) return null;

            var colonIndex = normalized.IndexOf(':');
            if (colonIndex >= 0) normalized = normalized.Substring(0, colonIndex);

            return normalized;
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
