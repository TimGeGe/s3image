using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Pluralsight.ImageAPI.Model;
using Pluralsight.ImageAPI.Services;
using Amazon.S3;

namespace Pluralsight.ImageAPI.Controllers
{
    [Route("images")]
    public class ImageController : Controller
    {
        private ILogger<ImageController> Logger { get; }
        private S3Settings S3Settings { get; }
        private ImageStore ImageStore { get; }

        public ImageController(ILogger<ImageController> logger, IOptions<S3Settings> s3Settings, ImageStore imageStore)
        {
            S3Settings = s3Settings.Value;
            ImageStore = imageStore;
            Logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Post(IFormFile file, string objectKey)
        {
            var fileName = Path.GetFileName(
                            ContentDispositionHeaderValue
                            .Parse(file.ContentDisposition)
                            .FileName
                            .Trim('"'));

            if (string.IsNullOrWhiteSpace(objectKey))
            {
                objectKey = S3KeyGenerator.GenerateObjectKey(fileName);
            }

            using (var stream = file.OpenReadStream())
            {
                ImageInfo info = ImageProcessor.GetImageInfo(stream);

                ImageUploadedModel model = await ImageStore.UploadImage(
                    S3Settings.OriginalBucketName,
                    S3Settings.OriginalBucketUrl,
                    objectKey,
                    S3StorageClass.Standard,
                    S3CannedACL.Private,
                    S3Settings.VaultName,
                    info);

                if (model.Exception != null)
                {
                    Logger.LogError("An error occured while uploading to S3", model.Exception);
                    return StatusCode((int)HttpStatusCode.InternalServerError);
                }

                return Created(model.ObjectLocation, model);
            }
        }

        [HttpGet]
        [Route("{*originalKey}")]
        public async Task<IActionResult> Get(string originalKey, int? width, int? height, string versionId)
        {
            if (string.IsNullOrWhiteSpace(versionId))
            {
                versionId = await ImageStore.GetLatestVersionId(S3Settings.OriginalBucketName, originalKey);

                if (string.IsNullOrWhiteSpace(versionId))
                {
                    // this image doesn't exist
                    return NotFound();
                }
            }

            string resizedKey = S3KeyGenerator.GenerateObjectKeyWithSize(originalKey, width, height, versionId);

            // check if the resized image exists
            if (await ImageStore.ImageExists(S3Settings.ResizedBucketName, resizedKey))
            {
                return Redirect(S3Settings.ResizedBucketUrl + resizedKey);
            }
            
            Stream responseStream = await ImageStore.GetImage(S3Settings.OriginalBucketName, originalKey, versionId);
            
            // resize the image
            ImageInfo info = ImageProcessor.Resize(responseStream, width, height);

            ImageUploadedModel model = await ImageStore.UploadImage(
                S3Settings.ResizedBucketName,
                S3Settings.ResizedBucketUrl,
                resizedKey,
                S3StorageClass.ReducedRedundancy,
                S3CannedACL.PublicRead,
                null,
                info);

            if (model.Exception != null)
            {
                Logger.LogError("An error occured while uploading to S3", model.Exception);
                return StatusCode((int)HttpStatusCode.InternalServerError);
            }

            return Redirect(S3Settings.ResizedBucketUrl + resizedKey);
        }

        [HttpGet]
        [Route("report-missing/{*missingKey}")]
        public IActionResult ReportMissing(string missingKey)
        {
            return NotFound($"Image with key '{missingKey}' as been reported as missing");
        }
    }
}
