namespace FaceReader_Middleware.Models
{
    public class DynamicScreenSaverGifRequest
    {
        public string Pass { get; set; }

        // Dynamic screensaver GIF image base64 content (without data: header)
        public string Base64 { get; set; }
    }
}

