namespace FaceReader_Middleware.Models
{
    public class FaceInfo
    {
        public string FaceId { get; set; }

        public string Url { get; set; }

        public string Base64 { get; set; }

        public bool? IsEasyWay { get; set; }

        public FaceBBox Bbox { get; set; }
    }
}
