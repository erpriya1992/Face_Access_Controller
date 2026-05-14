namespace FaceReader_Middleware.Models
{
    public class FaceUpdateBase64Request
    {
        public string Pass { get; set; }

        public string PersonId { get; set; }

        public string FaceId { get; set; }

        public string ImgBase64 { get; set; }

        public bool IsEasyWay { get; set; }

        /// <summary>When set, updates face on this reader instead of MainGate config.</summary>
        public string DeviceIp { get; set; }
    }
}

