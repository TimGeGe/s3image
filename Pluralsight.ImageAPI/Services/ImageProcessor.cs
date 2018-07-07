using System.Collections.Generic;
using System.IO;
using ImageResizer;
using Pluralsight.ImageAPI.Model;

namespace Pluralsight.ImageAPI.Services
{
    public static class ImageProcessor
    {
        public static ImageInfo GetImageInfo(Stream input)
        {
            input.Position = 0;

            ImageJob job = new ImageJob(input, new List<string> {"source.width", "source.height", "result.mime"})
            {
                ResetSourceStream = true,
                DisposeSourceObject = false
            };

            ImageBuilder.Current.Build(job);

            ImageInfo result = new ImageInfo
            {
                MimeType = job.ResultMimeType,
                Width = (int)job.ResultInfo["source.width"],
                Height = (int)job.ResultInfo["source.height"],
                Image = input
            };

            return result;
        }

        public static ImageInfo Resize(Stream input, int? desiredWidth, int? desiredHeight)
        {
            input.Position = 0;

            MemoryStream outputStream = new MemoryStream();
            Instructions instructions = new Instructions
            {
                Width = desiredWidth,
                Height = desiredHeight,
                Mode = FitMode.Max,
                Scale = ScaleMode.DownscaleOnly,
                OutputFormat = OutputFormat.Jpeg,
                JpegQuality = 90
            };

            ImageJob job = new ImageJob(input, outputStream, instructions)
            {
                ResetSourceStream = true,
                DisposeSourceObject = false,

            };

            ImageBuilder.Current.Build(job);

            ImageInfo result = new ImageInfo
            {
                MimeType = job.ResultMimeType,
                Width = (int)job.ResultInfo["final.width"],
                Height = (int)job.ResultInfo["final.height"],
                Image = outputStream
            };

            return result;
        }
    }
}
