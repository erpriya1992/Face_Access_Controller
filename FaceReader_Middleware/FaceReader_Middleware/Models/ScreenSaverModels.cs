using System.Collections.Generic;

namespace FaceReader_Middleware.Models
{
    public class ScreenSaverFileEntry
    {
        public bool Checked { get; set; }

        public string Filename { get; set; }
    }

    public class ScreenSaverConfig
    {
        public bool? Enable { get; set; }

        public List<ScreenSaverFileEntry> Gif { get; set; }

        public List<ScreenSaverFileEntry> Image { get; set; }

        // Rotation interval (1–30 seconds)
        public int? Span { get; set; }

        // Type field as returned by device (optional when setting)
        public int? Type { get; set; }
    }

    public class SetScreenSaverRequest
    {
        public string Pass { get; set; }

        public ScreenSaverConfig ScreenSaver { get; set; }
    }
}

