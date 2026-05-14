namespace FaceReader_Middleware.Models
{
    public class RecognitionRecordQueryRequest
    {
        public string PersonId { get; set; } = "-1";

        public string StartTime { get; set; } = "0";

        public string EndTime { get; set; } = "0";

        public int Length { get; set; } = 1000;

        public int Index { get; set; }

        public int Model { get; set; } = -1;

        public string? Order { get; set; }

        // Optional: allow caller to override which face device to query.
        public string? DeviceIp { get; set; }

        // Optional: allow caller to override device password.
        public string? DevicePassword { get; set; }
    }
}
