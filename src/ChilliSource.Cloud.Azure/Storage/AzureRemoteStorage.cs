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

        public async Task SaveAsync(Stream stream, string fileName, string contentType)
        {
            var fileRef = _storageContainer.GetBlockBlobReference(fileName);
            if (!String.IsNullOrEmpty(contentType))
            {
                fileRef.Properties.ContentType = contentType;
            }

            await fileRef.UploadFromStreamAsync(stream)
                  .IgnoreContext();
        }

        public async Task DeleteAsync(string fileToDelete)
        {
            var fileRef = _storageContainer.GetBlobReference(fileToDelete);
            await fileRef.DeleteIfExistsAsync()
                  .IgnoreContext();
        }

        public async Task<FileStorageResponse> GetContentAsync(string fileName)
        {
            Stream blobStream = null;
            var fileRef = _storageContainer.GetBlobReference(fileName);

            try
            {
                blobStream = await fileRef.OpenReadAsync()
                               .IgnoreContext();

                var contentLength = fileRef.Properties.Length;
                var contentType = fileRef.Properties.ContentType;

                var readonlyStream = ReadOnlyStreamWrapper.Create(blobStream, (s) => s?.Dispose(), contentLength);

                return FileStorageResponse.Create(fileName, contentLength, contentType, readonlyStream);
            }
            catch
            {
                blobStream?.Dispose();
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string fileName)
        {
            try
            {
                var fileRef = await _storageContainer.GetBlobReferenceFromServerAsync(fileName)
                              .IgnoreContext();
                return true;
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotFound)
                {
                    return false;
                }
                throw;
            }
        }

        public string GetPartialFilePath(string fileName)
        {
            return String.IsNullOrEmpty(_azureConfig.Container) ? fileName : $"{_azureConfig.Container}/{fileName}";
        }
    }
}
