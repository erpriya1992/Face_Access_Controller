namespace FaceReader_Middleware.Models
{
    public class RecognitionRecordResponse
    {
        public string Code { get; set; }

        public bool Success { get; set; }

        public RecognitionData Data { get; set; }
    }
}
