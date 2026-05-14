namespace FaceReader_Middleware.Models
{
    public class FaceFindRequest
    {
        public string Pass { get; set; }

        public string PersonId { get; set; }

        // 1: do not get base64 (default), 2: get base64 (overseas devices only)
        public int? Base64Enable { get; set; }

        /// <summary>When set, queries this reader instead of MainGate config.</summary>
        public string DeviceIp { get; set; }
    }
}

