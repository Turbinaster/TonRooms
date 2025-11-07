using System;

namespace TONServer
{
    public class TonConnectPayloadInfo
    {
        public string Payload { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public string Domain { get; set; }

        public bool IsExpired(DateTimeOffset now)
        {
            return ExpiresAt.HasValue && now >= ExpiresAt.Value;
        }
    }
}
