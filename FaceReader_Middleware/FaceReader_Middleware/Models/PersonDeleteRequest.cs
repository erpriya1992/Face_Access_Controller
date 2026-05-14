namespace FaceReader_Middleware.Models
{
    public class PersonDeleteRequest
    {
        public string Pass { get; set; }

        // Person ID or IDs (comma-separated), or "-1" to delete all
        public string Id { get; set; }

        /// <summary>When set, targets this reader instead of MainGate config.</summary>
        public string DeviceIp { get; set; }
    }
}

