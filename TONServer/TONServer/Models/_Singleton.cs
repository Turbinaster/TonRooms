using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;

namespace TONServer
{
    public static class _Singleton
    {
        public static IHubContext<_Hub> Hub;
        public static string ConnectionString;
        public static bool Development;
        public static string WebRootPath;
        public static string Host;
        public static Random Random = new Random((int)DateTime.Now.Ticks);
        public static string Api = "eyJhbGciOiJFZERTQSIsInR5cCI6IkpXVCJ9.eyJhdWQiOlsiYWxleHh4aW50ZXIxIl0sImV4cCI6MTgxNTMxMjA5OCwiaXNzIjoiQHRvbmFwaV9ib3QiLCJqdGkiOiJKNUZXWlBNUEo0VUlSQTVKUkFDWFdWSzQiLCJzY29wZSI6InNlcnZlciIsInN1YiI6InRvbmFwaSJ9.tp5x0D5Zrx2OKpHL_9vXpkbDY4Fftfy3P_cTU2UX_XNpgjBzDNAoPqTQe2OqlLYGbO7SPJZA65lGLtoATwe2Ag";
        public static ConcurrentDictionary<string, string> Sessions = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, TonConnectPayloadInfo> Payloads = new ConcurrentDictionary<string, TonConnectPayloadInfo>();
        public static string TonApiClientId;
    }
}
