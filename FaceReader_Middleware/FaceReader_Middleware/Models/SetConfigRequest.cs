namespace FaceReader_Middleware.Models
{
    public class SetConfigRequest
    {
        public string Pass { get; set; } = string.Empty;
        public string? DeviceIp { get; set; }
        public DeviceConfig? Config { get; set; }
    }
}
