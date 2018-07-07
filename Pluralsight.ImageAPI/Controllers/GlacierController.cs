using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Amazon;
using Amazon.Glacier;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pluralsight.ImageAPI.Model;
using Pluralsight.ImageAPI.Services;
using Amazon.Glacier.Model;
using Amazon.Runtime;
using Amazon.S3;
using Newtonsoft.Json;

namespace Pluralsight.ImageAPI.Controllers
{
    [Route("glacier")]
    public class GlacierController : Controller
    {
        private ILogger<GlacierController> Logger { get; }
        private S3Settings S3Settings { get; }
        private ImageStore ImageStore { get; }
        public IAmazonGlacier GlacierClient { get; set; }

        public GlacierController(ILogger<GlacierController> logger, IOptions<S3Settings> s3Settings, ImageStore imageStore)
        {
            Logger = logger;
            S3Settings = s3Settings.Value;
            ImageStore = imageStore;

            var credentials = new BasicAWSCredentials(s3Settings.Value.AWSAccessKey, s3Settings.Value.AWSSecretKey);

            var region = RegionEndpoint.GetBySystemName(s3Settings.Value.AWSRegion);

            GlacierClient = new AmazonGlacierClient(credentials, region);
        }

        [HttpGet]
        [Route("start-inventory")]
        public async Task<IActionResult> StartInventory()
        {
            InitiateJobRequest request = new InitiateJobRequest
            {
                VaultName = S3Settings.VaultName,
                JobParameters = new JobParameters
                {
                    Type = "inventory-retrieval",
                    Format = "JSON"
                }
            };

            InitiateJobResponse response = await GlacierClient.InitiateJobAsync(request);

            return Ok(response.JobId);
        }

        [HttpGet]
        [Route("list-jobs")]
        public async Task<IActionResult> ListJobs()
        {
            ListJobsRequest request = new ListJobsRequest
            {
                VaultName = S3Settings.VaultName
            };

            ListJobsResponse response = await GlacierClient.ListJobsAsync(request);

            return new JsonResult(response.JobList);
        }

        [HttpGet]
        [Route("get-inventory/{jobId}")]
        public async Task<IActionResult> GetInventoryOutput(string jobId)
        {
            GetJobOutputRequest request = new GetJobOutputRequest
            {
                VaultName = S3Settings.VaultName,
                JobId = jobId
            };

            var response = await GlacierClient.GetJobOutputAsync(request);

            StreamReader reader = new StreamReader(response.Body);
            string text = reader.ReadToEnd();

            return new JsonResult(JsonConvert.DeserializeObject(text));
        }

        [HttpGet]
        [Route("start-archive-retrieval/{archiveId}")]
        public async Task<IActionResult> StartArchiveRetrieval(string archiveId)
        {
            InitiateJobRequest request = new InitiateJobRequest
            {
                VaultName = S3Settings.VaultName,
                JobParameters = new JobParameters
                {
                    Type = "archive-retrieval",
                    ArchiveId = archiveId
                }
            };

            InitiateJobResponse response = await GlacierClient.InitiateJobAsync(request);

            return Ok(response.JobId);
        }

        [HttpGet]
        [Route("restore-archive/{jobId}")]
        public async Task<IActionResult> RestoreArchive(string jobId)
        {
            GetJobOutputRequest request = new GetJobOutputRequest
            {
                VaultName = S3Settings.VaultName,
                JobId = jobId
            };

            var response = GlacierClient.GetJobOutput(request);

            ArchiveDescription description =
    JsonConvert.DeserializeObject<ArchiveDescription>(response.ArchiveDescription);

            // AWS HashStream doesn't support seeking so we need to copy it back to a MemoryStream
            MemoryStream outputStream = new MemoryStream();
            response.Body.CopyTo(outputStream);

            ImageUploadedModel model = await ImageStore.UploadImage(
                S3Settings.OriginalBucketName,
                S3Settings.OriginalBucketUrl,
                description.ObjectKey,
                S3StorageClass.StandardInfrequentAccess,
                S3CannedACL.Private,
                null,
                new ImageInfo
                {
                    MimeType = description.ContentType,
                    Width = description.Width,
                    Height = description.Height,
                    Image = outputStream
                }
            );

            return Created(model.ObjectLocation, model);
        }
    }
}