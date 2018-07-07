using System;

namespace Pluralsight.ImageAPI.Model
{
    public class ImageUploadedModel
    {
        public string ObjectKey { get; set; }
        public string ObjectLocation { get; set; }
        public string VersionId { get; set; }
        public string ETag { get; set; }
        public string ArchiveId { get; set; }
        public Exception Exception { get; set; }
    }
}
