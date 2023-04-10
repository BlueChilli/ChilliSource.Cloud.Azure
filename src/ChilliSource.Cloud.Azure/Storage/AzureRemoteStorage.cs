using ChilliSource.Cloud.Core;
using ChilliSource.Core.Extensions;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChilliSource.Cloud.Azure
{
    /// <summary>
    /// IRemoteStorage implementation for Azure File Storage.
    /// </summary>
    public class AzureRemoteStorage : IRemoteStorage
    {
        private AzureStorageConfiguration _azureConfig;
        private CloudBlobContainer _storageContainer;

        public AzureRemoteStorage(AzureStorageConfiguration azureConfig)
        {
            if (azureConfig == null)
                throw new ArgumentNullException("azureConfig is required.");

            _azureConfig = azureConfig;

            var storageAccount = new CloudStorageAccount(new StorageCredentials(_azureConfig.AccountName, _azureConfig.AccountKey), useHttps: true);
            var blobClient = storageAccount.CreateCloudBlobClient();

            _storageContainer = String.IsNullOrWhiteSpace(_azureConfig.Container) ?
                                    blobClient.GetRootContainerReference() :
                                    blobClient.GetContainerReference(_azureConfig.Container);
        }
        internal AzureRemoteStorage(CloudBlobContainer container)
        {
            _storageContainer = container;
        }

#if NET_4X
        public async Task SaveAsync(Stream stream, string fileName, string contentType)
        {
            var fileRef = _storageContainer.GetBlockBlobReference(fileName);
            if (!String.IsNullOrEmpty(contentType))
            {
                fileRef.Properties.ContentType = contentType;
            }

            await fileRef.UploadFromStreamAsync(stream, CancellationToken.None)
                  .IgnoreContext();
        }
#else
        public async Task SaveAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
        {
            await SaveAsync(stream, new FileStorageMetadataInfo()
            {
                FileName = fileName,
                ContentType = contentType
            }, cancellationToken);
        }

        public async Task SaveAsync(Stream stream, FileStorageMetadataInfo metadata, CancellationToken cancellationToken)
        {
            var fileRef = _storageContainer.GetBlockBlobReference(metadata.FileName);
            if (!String.IsNullOrEmpty(metadata.CacheControl))
            {
                fileRef.Properties.CacheControl = metadata.CacheControl;
            }

            if (!String.IsNullOrEmpty(metadata.ContentDisposition))
            {
                fileRef.Properties.ContentDisposition = metadata.ContentDisposition;
            }

            if (!String.IsNullOrEmpty(metadata.ContentEncoding))
            {
                fileRef.Properties.ContentEncoding = metadata.ContentEncoding;
            }

            if (!String.IsNullOrEmpty(metadata.ContentType))
            {
                fileRef.Properties.ContentType = metadata.ContentType;
            }

            await fileRef.UploadFromStreamAsync(stream, cancellationToken)
                  .IgnoreContext();
        }
#endif

#if NET_4X
        public async Task DeleteAsync(string fileToDelete)
        {
            CancellationToken cancellationToken = CancellationToken.None;
#else
        public async Task DeleteAsync(string fileToDelete, CancellationToken cancellationToken)
        {
#endif
            var fileRef = _storageContainer.GetBlobReference(fileToDelete);
            await fileRef.DeleteIfExistsAsync(cancellationToken)
                  .IgnoreContext();
        }

#if NET_4X
        public async Task<FileStorageResponse> GetContentAsync(string fileName)
        {
            CancellationToken cancellationToken = CancellationToken.None;
            var fileRef = _storageContainer.GetBlobReference(fileName);
            Stream blobStream = null;

            try
            {
                blobStream = await fileRef.OpenReadAsync(cancellationToken)
                                   .IgnoreContext();

                var contentLength = fileRef.Properties.Length;
                var contentType = fileRef.Properties.ContentType;

                var readonlyStream = ReadOnlyStreamWrapper.Create(blobStream, (s) => s?.Dispose(), contentLength);

                return FileStorageResponse.Create(fileRef.Name, contentLength, contentType, readonlyStream);
            }
            catch
            {
                blobStream?.Dispose();
                throw;
            }
        }
#else
        public async Task<FileStorageResponse> GetContentAsync(string fileName, CancellationToken cancellationToken)
        {
            var fileRef = _storageContainer.GetBlobReference(fileName);
            Stream blobStream = null;

            try
            {
                blobStream = await fileRef.OpenReadAsync(cancellationToken)
                                   .IgnoreContext();

                var metadata = MapMetadata(fileRef);
                var readonlyStream = ReadOnlyStreamWrapper.Create(blobStream, (s) => s?.Dispose(), metadata.ContentLength);

                return FileStorageResponse.Create(metadata, readonlyStream);
            }
            catch
            {
                blobStream?.Dispose();
                throw;
            }
        }
#endif

        private async Task<CloudBlob> GetMetadataInternalAsync(string fileName, CancellationToken cancellationToken)
        {
            try
            {
                var fileRef = _storageContainer.GetBlobReference(fileName);

                await fileRef.FetchAttributesAsync(cancellationToken)
                      .IgnoreContext();

                return fileRef;
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotFound)
                {
                    return null;
                }
                throw;
            }
        }

#if NET_4X
        public async Task<bool> ExistsAsync(string fileName)
        {
            CancellationToken cancellationToken = CancellationToken.None;
#else
        public async Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken)
        {
#endif
            return (await GetMetadataInternalAsync(fileName, cancellationToken).IgnoreContext()) != null;
        }

#if NET_4X
        [Obsolete]
        public string GetPartialFilePath(string fileName)
        {
            return String.IsNullOrEmpty(_azureConfig.Container) ? fileName : $"{_azureConfig.Container}/{fileName}";
        }
#else
        private IFileStorageMetadataResponse MapMetadata(CloudBlob blob)
        {
            var properties = blob.Properties;
            var lastMotified = properties.LastModified?.ToUniversalTime().UtcDateTime;

            var metadata = new FileStorageMetadataResponse()
            {
                FileName = blob.Name,
                CacheControl = properties.CacheControl,
                ContentDisposition = properties.ContentDisposition,
                ContentEncoding = properties.ContentEncoding,
                ContentLength = properties.Length,
                ContentType = properties.ContentType,
                LastModifiedUtc = lastMotified ?? new DateTime(0, DateTimeKind.Utc)
            };

            return metadata;
        }

        public async Task<IFileStorageMetadataResponse> GetMetadataAsync(string fileName, CancellationToken cancellationToken)
        {
            var azureMetadata = await GetMetadataInternalAsync(fileName, cancellationToken);
            return MapMetadata(azureMetadata);
        }

        public string GetPreSignedUrl(string fileName, TimeSpan expiresIn)
        {
            throw new NotImplementedException();
        }
#endif
    }
}
