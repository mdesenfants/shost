using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using SecureHost.DataProviders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace SecureHost.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ZipController : ControllerBase
    {
        public IConfiguration Config { get; }
        public EncryptedBlobProvider Container { get; }
        public BlobContainerClient Client { get; }

        [FromHeader(Name = "If-None-Match")]
        public string IfNoneMatch { get; set; }

        public ZipController(IConfiguration config, EncryptedBlobProvider container, BlobContainerClient client)
        {
            Config = config;
            Container = container;
            Client = client;
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Get()
        {
            try
            {
                var items = new ConcurrentBag<BlobItem>();

                await foreach (var blob in Client.GetBlobsAsync())
                {
                    items.Add(blob);
                }

                var memoryStream = new MemoryStream();

                using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true);
                foreach (var item in items)
                {
                    var (content, _, _) = await Container.DownloadDecryptedAsync(item.Name, null);
                    var zipFile = archive.CreateEntry(item.Name);
                    using var entryStream = zipFile.Open();
                    await content.CopyToAsync(entryStream);
                }

                memoryStream.Seek(0, SeekOrigin.Begin);

                Response.Headers[HeaderNames.ContentDisposition] = "attachment; filename=download.zip";
                return File(memoryStream.ToArray(), "application/zip", "download.zip");
            }
            catch
            {
                return NotFound();
            }
        }
    }
}