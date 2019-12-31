using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using SecureHost.DataProviders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SecureHost.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        public IConfiguration Config { get; }
        public EncryptedBlobProvider Container { get; }
        public BlobContainerClient Client { get; }

        [FromHeader(Name = "If-None-Match")]
        public string IfNoneMatch { get; set; }

        public FilesController(IConfiguration config, EncryptedBlobProvider container, BlobContainerClient client)
        {
            Config = config;
            Container = container;
            Client = client;
        }

        [HttpPost]
        public async Task<IActionResult> Post(List<IFormFile> files)
        {
            foreach (var file in files)
            {
                if (file == null) throw new Exception("File is null");
                if (file.Length == 0) throw new Exception("File is empty");

                var headers = new Azure.Storage.Blobs.Models.BlobHttpHeaders()
                {
                    ContentType = file.ContentType,
                    ContentDisposition = file.ContentDisposition
                };

                var stream = file.OpenReadStream();

                await Container.UploadEncryptedAsync(file.FileName, stream, headers);
            }

            return Accepted();
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Get()
        {
            var items = new ConcurrentBag<string>();

            await foreach (var blob in Client.GetBlobsAsync())
            {
                items.Add(Url.Action(blob.Name, "Files"));
            }

            return Ok(items.ToArray());
        }

        [HttpGet("{file}")]
        public async Task<IActionResult> Get(string file)
        {
            try
            {
                var (content, info, etag) = await Container.DownloadDecryptedAsync(file, IfNoneMatch);

                Response.Headers[HeaderNames.ETag] = new string[] { etag };
                Response.Headers[HeaderNames.Vary] = new string[] { HeaderNames.ETag };

                if (etag == IfNoneMatch)
                {
                    return StatusCode(304);
                }

                return File(content, info);
            }
            catch
            {
                return NotFound();
            }
        }
    }
}