namespace FaceReader_Middleware.Models
{
    public class FindCardRecordQueryRequest
    {
        public string PersonId { get; set; } = "-1";

        public string StartTime { get; set; } = "0";

        public string EndTime { get; set; } = "0";
    }
}
