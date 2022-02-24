using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Infrastructure.Runtime;
using Umbraco.Cms.Tests.Integration.Testing;

namespace Umbraco.Cms.Tests.Integration.Umbraco.Infrastructure.Runtime
{
    [TestFixture]
    internal class FileSystemMainDomLockTests : UmbracoIntegrationTest
    {
        private IMainDomKeyGenerator MainDomKeyGenerator { get; set; }

        private IHostingEnvironment HostingEnvironment { get; set; }

        private FileSystemMainDomLock FileSystemMainDomLock { get; set; }

        private string LockFilePath { get; set; }
        private string LockReleaseFilePath { get; set; }

        [SetUp]
        public void SetUp()
        {
            MainDomKeyGenerator = GetRequiredService<IMainDomKeyGenerator>();
            HostingEnvironment = GetRequiredService<IHostingEnvironment>();

            var lockFileName = $"MainDom_{MainDomKeyGenerator.GenerateKey()}.lock";
            LockFilePath = Path.Combine(HostingEnvironment.LocalTempPath, lockFileName);
            LockReleaseFilePath = LockFilePath + "_release";

            var log = GetRequiredService<ILogger<FileSystemMainDomLock>>();
            FileSystemMainDomLock = new FileSystemMainDomLock(log, MainDomKeyGenerator, HostingEnvironment);
        }

        [TearDown]
        public void TearDown()
        {
            while (File.Exists(LockFilePath))
            {
                File.Delete(LockFilePath);
            }
            while (File.Exists(LockReleaseFilePath))
            {
                File.Delete(LockReleaseFilePath);
            }
        }

        [Test]
        public async Task AcquireLockAsync_WhenNoOtherHoldsLockFileHandle_ReturnsTrue()
        {
            using var sut = FileSystemMainDomLock;

            var result = await sut.AcquireLockAsync(1000);

            Assert.True(result);
        }

        [Test]
        public async Task AcquireLockAsync_WhenTimeoutExceeded_ReturnsFalse()
        {
            await using var lockFile = File.Open(LockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            using var sut = FileSystemMainDomLock;

            var result = await sut.AcquireLockAsync(1000);

            Assert.False(result);
        }

        [Test]
        public async Task ListenAsync_WhenLockReleaseSignalFileFound_DropsLockFileHandle()
        {
            using var sut = FileSystemMainDomLock;

            await sut.AcquireLockAsync(1000);

            var before = await sut.AcquireLockAsync(1000);

            await using (_ = File.Open(LockReleaseFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
            }

            await sut.ListenAsync();

            var after = await sut.AcquireLockAsync(1000);

            Assert.Multiple(() =>
            {
                Assert.False(before);
                Assert.True(after);
            });
        }
    }
}
