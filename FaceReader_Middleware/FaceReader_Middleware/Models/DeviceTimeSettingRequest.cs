namespace FaceReader_Middleware.Models
{
    public class DeviceTimeSettingRequest
    {
        public string Pass { get; set; }

        // Unix millisecond timestamp as string
        public string Timestamp { get; set; }
    }
}

