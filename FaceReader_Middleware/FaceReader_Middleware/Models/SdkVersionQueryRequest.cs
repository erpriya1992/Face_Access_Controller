namespace FaceReader_Middleware.Models
{
    public class SdkVersionQueryRequest
    {
        public string Pass { get; set; }

        /// <summary>
        /// Algorithm type: 1 = Face SDK (default), 2 = Fingerprint SDK
        /// </summary>
        public int? Type { get; set; }
    }
}

