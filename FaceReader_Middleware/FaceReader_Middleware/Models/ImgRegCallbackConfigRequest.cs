namespace FaceReader_Middleware.Models
{
    public class ImgRegCallbackConfigRequest
    {
        public string Pass { get; set; }

        // Callback URL; empty/null means clear callback
        public string Url { get; set; }

        // 1 = Off (default), 2 = On; optional
        public int? Base64 { get; set; }
    }
}

