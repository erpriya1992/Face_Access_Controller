namespace FaceReader_Middleware.Models
{
    public class HeartbeatCallbackConfigRequest
    {
        public string Pass { get; set; }

        // Callback URL; empty/null means clear callback
        public string Url { get; set; }

        // Heartbeat interval in seconds (optional)
        public int? Interval { get; set; }
    }
}

