using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ChilliSource.Cloud.Azure.Tests
{
    public class CloudAzureTests
    {
        private readonly Mock<CloudBlobContainer> _blobContainerMock;
        private readonly Mock<CloudBlockBlob> _blobItemMock;
        private readonly AzureRemoteStorage _azureStorageFixture;
        public CloudAzureTests()
        {
            var blobUriMock = new Uri("http://bogus/myaccount/blob");
            if (_blobItemMock == null)
                _blobItemMock = new Mock<CloudBlockBlob>(blobUriMock);

            if (_blobContainerMock == null)
                _blobContainerMock = new Mock<CloudBlobContainer>(blobUriMock);

            if (_azureStorageFixture == null)
                _azureStorageFixture = new AzureRemoteStorage(_blobContainerMock.Object);
        }

        //[Fact]
        //public async Task SaveAsync_ShouldNotFailSave_WhenContentIsNotProvided()
        //{

        //}

        [Fact]
        public async Task SaveAsync_ShouldSaveFile()
        {
            var fakePackageFile = new MemoryStream();
            _blobItemMock.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>()))
                .Returns(Task.FromResult(0))
                .Verifiable();

            _blobContainerMock.Setup(x => x.GetBlockBlobReference(It.IsAny<string>()))
                .Returns(_blobItemMock.Object)
                .Verifiable();

            await _azureStorageFixture.SaveAsync(fakePackageFile, "testfile.txt", "text/plain");

            // Assert
            _blobItemMock.Verify();
            _blobContainerMock.Verify();
        }

        [Fact]
        public async Task DeleteAsync_ShouldDeleteFile()
        {
            _blobContainerMock.Setup(x => x.GetBlobReference(It.IsAny<string>()))
                .Returns(_blobItemMock.Object)
                .Verifiable();

            _blobItemMock.Setup(x => x.DeleteIfExistsAsync())
                .Returns(Task<bool>.FromResult<bool>(false))
                .Verifiable();

            await _azureStorageFixture.DeleteAsync("testfile.txt");

            // Assert
            _blobItemMock.Verify();
            _blobContainerMock.Verify();
        }
    }
}
