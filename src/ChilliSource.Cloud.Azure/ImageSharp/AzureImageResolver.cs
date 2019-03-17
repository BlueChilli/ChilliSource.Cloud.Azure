#if !NET_4X
using Microsoft.WindowsAzure.Storage.Blob;
using SixLabors.ImageSharp.Web;
using SixLabors.ImageSharp.Web.Resolvers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ChilliSource.Cloud.Azure.ImageSharp
{
    internal class AzureImageResolver : IImageResolver
    {
        AzureRemoteStorage _storage;
        string _fileName;
        BlobProperties _azureMetadata;

        public AzureImageResolver(AzureRemoteStorage storage, string fileName, BlobProperties azureMetadata)
        {
            _storage = storage;
            _fileName = fileName;
            _azureMetadata = azureMetadata;
        }

        public Task<ImageMetaData> GetMetaDataAsync()
        {
            CacheControlHeaderValue cacheControl = null;
            CacheControlHeaderValue.TryParse(_azureMetadata.CacheControl, out cacheControl);

            var lastMotified = _azureMetadata.LastModified?.ToUniversalTime().UtcDateTime;
            if (lastMotified == null)
            {
                throw new ApplicationException($"Last modified value not found for file {_fileName}.");
            }

            var metadata = new ImageMetaData(
                lastMotified.Value,
                _azureMetadata.ContentType,
                cacheControl?.MaxAge ?? TimeSpan.MinValue
            );

            return Task.FromResult<ImageMetaData>(metadata);
        }

        public async Task<Stream> OpenReadAsync()
        {
            var response = await _storage.GetContentAsync(_fileName);
            if (response == null)
                throw new ApplicationException("AWSImageResolver.OpenReadAsync failed to find file.");

            return response.Stream;
        }
    }
}
#endif