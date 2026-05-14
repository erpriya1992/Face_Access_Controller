namespace FaceReader_Middleware.Models
{
    public class RecognitionData
    {
        public PageInfo PageInfo { get; set; }

        public List<RecognitionRecord> Records { get; set; }
    }
}
