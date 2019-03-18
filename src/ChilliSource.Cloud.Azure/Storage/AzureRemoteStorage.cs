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
            CancellationToken cancellationToken = CancellationToken.None;
#else
        public async Task SaveAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
        {
#endif
            var fileRef = _storageContainer.GetBlockBlobReference(fileName);
            if (!String.IsNullOrEmpty(contentType))
            {
                fileRef.Properties.ContentType = contentType;
            }

            await fileRef.UploadFromStreamAsync(stream, cancellationToken)
                  .IgnoreContext();
        }

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
#else
        public async Task<FileStorageResponse> GetContentAsync(string fileName, CancellationToken cancellationToken)
        {
#endif
            var fileRef = _storageContainer.GetBlobReference(fileName);

            return await GetContentFromBlobAsync(fileRef, cancellationToken)
                         .IgnoreContext();
        }

        internal async Task<FileStorageResponse> GetContentFromBlobAsync(CloudBlob fileRef, CancellationToken cancellationToken)
        {
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

        internal async Task<CloudBlob> GetMetadataAsync(string fileName, CancellationToken cancellationToken)
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
            return (await GetMetadataAsync(fileName, cancellationToken).IgnoreContext()) != null;
        }

#if NET_4X
        [Obsolete]
        public string GetPartialFilePath(string fileName)
        {
            return String.IsNullOrEmpty(_azureConfig.Container) ? fileName : $"{_azureConfig.Container}/{fileName}";
        }
#endif
    }
}
