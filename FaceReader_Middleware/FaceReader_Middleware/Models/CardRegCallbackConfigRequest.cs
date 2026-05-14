namespace FaceReader_Middleware.Models
{
    public class CardRegCallbackConfigRequest
    {
        public string Pass { get; set; }

        // Callback URL; empty/null means clear callback
        public string Url { get; set; }
    }
}

