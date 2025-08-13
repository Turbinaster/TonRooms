using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TONServer
{
    public class _Hub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public static async Task Send(string method, object arg1, object arg2 = null, object arg3 = null, object arg4 = null, object arg5 = null, object arg6 = null, object arg7 = null, object arg8 = null, object arg9 = null, object arg10 = null)
        {
            try
            {
                if (arg2 == null) arg2 = new object();
                if (arg3 == null) arg3 = new object();
                if (arg4 == null) arg4 = new object();
                if (arg5 == null) arg5 = new object();
                if (arg6 == null) arg6 = new object();
                if (arg7 == null) arg7 = new object();
                if (arg8 == null) arg8 = new object();
                if (arg9 == null) arg9 = new object();
                if (arg10 == null) arg10 = new object();
                await _Singleton.Hub.Clients.All.SendAsync(method, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
            }
            catch { }
        }

        public static async Task SendSession(string method, string session, object arg1, object arg2 = null, object arg3 = null, object arg4 = null, object arg5 = null, object arg6 = null, object arg7 = null, object arg8 = null, object arg9 = null)
        {
            try
            {
                if (arg2 == null) arg2 = new object();
                if (arg3 == null) arg3 = new object();
                if (arg4 == null) arg4 = new object();
                if (arg5 == null) arg5 = new object();
                if (arg6 == null) arg6 = new object();
                if (arg7 == null) arg7 = new object();
                if (arg8 == null) arg8 = new object();
                if (arg9 == null) arg9 = new object();
                await _Singleton.Hub.Clients.All.SendAsync(method, session, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
            }
            catch { }
        }
    }
}
