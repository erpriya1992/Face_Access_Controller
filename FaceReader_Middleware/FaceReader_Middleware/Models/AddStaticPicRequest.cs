namespace FaceReader_Middleware.Models
{
    public class AddStaticPicRequest
    {
        public string Pass { get; set; }

        // 1: Replace dynamic screen saver image; 2: Restore default screen saver image
        public int Operator { get; set; }

        // Dynamic image base64 content (required when Operator = 1)
        public string Base64 { get; set; }
    }
}

