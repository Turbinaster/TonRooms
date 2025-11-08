using System;
using Libs;

namespace TONServer.Components
{
    public static class ServerLog
    {
        public static void Log(Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            Helper.Log(ex);
            Console.Error.WriteLine(ex.ToString());
        }

        public static void Log(string message)
        {
            if (message == null)
            {
                message = string.Empty;
            }

            Helper.Log(message);
            Console.Error.WriteLine(message);
        }
    }
}
