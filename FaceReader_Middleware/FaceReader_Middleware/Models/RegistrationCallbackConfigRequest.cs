namespace FaceReader_Middleware.Models
{
    public class RegistrationCallbackConfigRequest
    {
        public string Pass { get; set; }

        // Callback URL; empty/null means clear callback
        public string Url { get; set; }

        // 1: Photo, 2: Card number, 3: Fingerprint, 4: QR code, 5: Person info (overseas only)
        public int Type { get; set; }

        // 1: Off (default), 2: On (only meaningful when Type = 1)
        public int? Base64Enable { get; set; }
    }
}

