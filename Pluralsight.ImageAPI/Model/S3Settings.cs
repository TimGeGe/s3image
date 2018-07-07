namespace Pluralsight.ImageAPI.Model
{
    public class S3Settings
    {
        public string OriginalBucketName { get; set; }
        public string OriginalBucketUrl { get; set; }
        public string ResizedBucketName { get; set; }
        public string ResizedBucketUrl { get; set; }
        public string VaultName { get; set; }
        public string AWSRegion { get; set; }
        public string AWSAccessKey { get; set; }
        public string AWSSecretKey { get; set; }
    }
}
