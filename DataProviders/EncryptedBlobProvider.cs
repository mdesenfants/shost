using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.WebKey;
using Microsoft.Extensions.Configuration;
using SecureHost.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace SecureHost.DataProviders
{
    public class EncryptedBlobProvider
    {
        private const string EncryptionKeyName = "FileEncryptionKey";

        public BlobContainerClient Container { get; }
        public KeyVaultClient KeyVault { get; }
        public IConfiguration Configuration { get; }

        public EncryptedBlobProvider(IConfiguration config, BlobContainerClient container, KeyVaultClient keyVault)
        {
            Configuration = config;
            Container = container;
            KeyVault = keyVault;
        }

        public async Task<Response<BlobContentInfo>> UploadEncryptedAsync(string blobName, Stream stream, BlobHttpHeaders headers)
        {
            using var all = new MemoryStream();
            await stream.CopyToAsync(all);

            using var aes = new AesManaged();
            aes.GenerateKey();
            aes.GenerateIV();

            var keyIdentifier = new KeyIdentifier($"https://{Configuration["KeyVaultName"].ToLower()}.vault.azure.net", EncryptionKeyName).ToString();

            var keyVersion = (await KeyVault.GetKeyAsync(keyIdentifier)).KeyIdentifier.Version;

            var envelope = new Envelope()
            {
                Key = await WrapKey(aes.Key, keyIdentifier),
                IV = await WrapKey(aes.IV, keyIdentifier),
                Content = Convert.ToBase64String(PerformCryptography(all.ToArray(), aes.CreateEncryptor()))
            };

            using var upstream = new MemoryStream();
            var serializedEnvelope = JsonSerializer.SerializeAsync(upstream, envelope);

            upstream.Position = 0;

            var blob = Container.GetBlobClient(blobName);
            var result = await blob.UploadAsync(upstream,
                metadata: new Dictionary<string, string>()
                {
                    { "EBP_KeyName", EncryptionKeyName },
                    { "EBP_KeyVersion", keyVersion },
                    { "EBP_WrapAlgorithm", JsonWebKeyEncryptionAlgorithm.RSAOAEP256 },
                    { "EBP_ContentType", headers.ContentType }
                });

            return result;
        }

        public async Task<(Stream content, string contentType)> DownloadDecryptedAsync(string blobName)
        {
            var blob = Container.GetBlobClient(blobName);

            var info = (await blob.DownloadAsync()).Value;

            var keyName = info.Details.Metadata["EBP_KeyName"];
            var version = info.Details.Metadata["EBP_KeyVersion"];
            var algo = info.Details.Metadata["EBP_WrapAlgorithm"];
            var contentType = info.Details.Metadata["EBP_ContentType"];

            using var streamReader = new StreamReader(info.Content);

            var text = await streamReader.ReadToEndAsync();

            var envelope = JsonSerializer.Deserialize<Envelope>(text);

            byte[] aesKey = await UnwrapKey(envelope.Key, keyName, version, algo);
            byte[] aesIV = await UnwrapKey(envelope.IV, keyName, version, algo);

            using var aes = new AesManaged
            {
                Key = aesKey,
                IV = aesIV
            };

            var content = new MemoryStream(PerformCryptography(Convert.FromBase64String(envelope.Content), aes.CreateDecryptor()));
            return (content, contentType);
        }

        private async Task<string> WrapKey(byte[] value, string keyIdentifier)
        {
            var encrypted = await KeyVault.EncryptAsync(
                keyIdentifier,
                JsonWebKeyEncryptionAlgorithm.RSAOAEP256,
                value);

            return Convert.ToBase64String(encrypted.Result);
        }

        private async Task<byte[]> UnwrapKey(string value, string keyName, string keyVersion, string algo)
        {
            var converted = Convert.FromBase64String(value);

            var decrypted = await KeyVault.DecryptAsync(
                $"https://{Configuration["KeyVaultName"].ToLower()}.vault.azure.net",
                keyName,
                keyVersion,
                algo,
                converted);

            return decrypted.Result;
        }

        private byte[] PerformCryptography(byte[] data, ICryptoTransform cryptoTransform)
        {
            using var ms = new MemoryStream();
            using var cryptoStream = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write);

            cryptoStream.Write(data, 0, data.Length);
            cryptoStream.FlushFinalBlock();

            return ms.ToArray();
        }
    }
}
