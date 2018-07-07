using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.Glacier;
using Amazon.Glacier.Model;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Pluralsight.ImageAPI.Model;

namespace Pluralsight.ImageAPI.Services
{
    public class ImageStore
    {
        public IAmazonS3 S3Client { get; set; }
        public IAmazonGlacier GlacierClient { get; set; }

        public ImageStore(IOptions<S3Settings> s3Settings)
        {
            var credentials = new BasicAWSCredentials(s3Settings.Value.AWSAccessKey, s3Settings.Value.AWSSecretKey);

            var region = RegionEndpoint.GetBySystemName(s3Settings.Value.AWSRegion);

            S3Client = new AmazonS3Client(credentials, region);

            GlacierClient = new AmazonGlacierClient(credentials, region);
        }
        
        public async Task<ImageUploadedModel> UploadImage(
            string bucketName,
            string bucketUrl,
            string objectKey,
            S3StorageClass storageClass,
            S3CannedACL permissions,
            string glacierVaultName,
            ImageInfo image)
        {
            ImageUploadedModel model = new ImageUploadedModel();

            try
            {
                PutObjectRequest putRequest = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey,
                    StorageClass = storageClass,
                    CannedACL = permissions,
                    ContentType = image.MimeType,
                    AutoCloseStream = false
                };

                putRequest.Metadata.Add("width", image.Width.ToString());
                putRequest.Metadata.Add("height", image.Height.ToString());

                putRequest.InputStream = image.Image;

                byte[] md5Hash = image.Image.Md5Hash();
                putRequest.MD5Digest = md5Hash.ToBase64String();

                PutObjectResponse response = await S3Client.PutObjectAsync(putRequest);

                string eTag = response.ETag.Trim('"').ToLowerInvariant();
                string expectedETag = md5Hash.ToS3ETagString();

                if (eTag != expectedETag)
                {
                    throw new Exception("The eTag received from S3 doesn't match the eTag computed before uploading. This usually indicates that the image has been corrupted in transit.");
                }

                // upload to Glacier if needed
                if (!string.IsNullOrWhiteSpace(glacierVaultName))
                {
                    ArchiveDescription description = new ArchiveDescription
                    {
                        ObjectKey = objectKey,
                        ContentType = image.MimeType,
                        Width = image.Width,
                        Height = image.Height
                    };

                    // reset stream position in image
                    image.Image.Position = 0;

                    UploadArchiveRequest glacierRequest = new UploadArchiveRequest
                    {
                        ArchiveDescription = JsonConvert.SerializeObject(description, Formatting.None),
                        Body = image.Image,
                        VaultName = glacierVaultName,
                        Checksum = TreeHashGenerator.CalculateTreeHash(image.Image)
                    };

                    UploadArchiveResponse glacierResponse = await GlacierClient.UploadArchiveAsync(glacierRequest);

                    model.ArchiveId = glacierResponse.ArchiveId;
                }

                model.ObjectKey = objectKey;
                model.ETag = eTag;
                model.ObjectLocation = bucketUrl + objectKey;
                model.VersionId = response.VersionId;
            }
            catch (Exception ex)
            {
                model.Exception = ex;
            }

            return model;
        }

        public async Task<bool> ImageExists(string bucketName, string objectKey)
        {
            try
            {
                GetObjectMetadataRequest request = new GetObjectMetadataRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                };

                await S3Client.GetObjectMetadataAsync(request);

                return true;
            }
            catch (AmazonS3Exception)
            {
                return false;
            }
        }

        public async Task<string> GetLatestVersionId(string bucketName, string objectKey)
        {
            GetObjectMetadataRequest request = new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = objectKey
            };

            var response = await S3Client.GetObjectMetadataAsync(request);

            return response.VersionId;
        }

        public async Task<Stream> GetImage(string bucketName, string objectKey, string versionId)
        {
            GetObjectRequest originalRequest = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey
            };

            if (!string.IsNullOrWhiteSpace(versionId))
            {
                originalRequest.VersionId = versionId;
            }

            try
            {
                GetObjectResponse response = await S3Client.GetObjectAsync(originalRequest);

                // AWS HashStream doesn't support seeking so we need to copy it back to a MemoryStream
                MemoryStream outputStream = new MemoryStream();
                response.ResponseStream.CopyTo(outputStream);

                outputStream.Position = 0;

                return outputStream;
            }
            catch (AmazonS3Exception)
            {
                // Not found if we get an exception
                return null;
            }
        }
    }
}
