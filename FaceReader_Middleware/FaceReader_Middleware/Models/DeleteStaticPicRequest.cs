namespace FaceReader_Middleware.Models
{
    public class DeleteStaticPicRequest
    {
        public string Pass { get; set; }

        // Filename(s) to delete. Use "-1" to delete all; for multiple, separate by comma: "file1,file2".
        public string Filename { get; set; }
    }
}

