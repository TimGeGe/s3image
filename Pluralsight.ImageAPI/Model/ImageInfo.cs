using System.IO;

namespace Pluralsight.ImageAPI.Model
{
    public class ImageInfo
    {
        public string MimeType { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Stream Image { get; set; }
    }
}
