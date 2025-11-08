using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public static string Api = "AG4U57ADBPA6UBQAAAABDHPHKKYZDKQKKMINM7NBMTST4YOQN63TM77F6UNEIWTUR4IGM5Q";
        public static Dictionary<string, string> Sessions = new Dictionary<string, string>();
    }
}
