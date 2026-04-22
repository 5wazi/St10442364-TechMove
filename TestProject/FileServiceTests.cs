using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using PROG7313_TechMove.Services;

namespace TestProject
{
    public class FileServiceTests : IDisposable
    {
        // ── per-test temp folder so real file I/O works without ASP.NET host ──

        private readonly string _tempRoot;
        private readonly FileService _svc;

        public FileServiceTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), $"TechMove_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempRoot);

            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.WebRootPath).Returns(_tempRoot);

            _svc = new FileService(env.Object, Mock.Of<ILogger<FileService>>());
        }

        public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

        // ── helpers ───────────────────────────────────────────────────────────

        private static IFormFile MakeFile(string fileName, string content = "dummy")
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            var file = new Mock<IFormFile>();
            file.Setup(f => f.FileName).Returns(fileName);
            file.Setup(f => f.Length).Returns(stream.Length);
            file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>((dest, _) => { stream.Position = 0; stream.CopyTo(dest); })
                .Returns(Task.CompletedTask);
            return file.Object;
        }

        private static IFormFile MakeFileWithSize(string fileName, long sizeBytes)
        {
            var file = new Mock<IFormFile>();
            file.Setup(f => f.FileName).Returns(fileName);
            file.Setup(f => f.Length).Returns(sizeBytes);
            file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return file.Object;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Validate() — new method from the file validation refactor.
        // This is the primary path the controller now uses. All rules live here.
        // ═══════════════════════════════════════════════════════════════════════

        // ── null / empty → optional field, so both are valid ─────────────────

        [Fact]
        public void Validate_ReturnsValid_WhenFileIsNull()
        {
            var result = _svc.Validate(null);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_ReturnsValid_WhenFileIsEmpty()
        {
            var result = _svc.Validate(MakeFileWithSize("empty.pdf", 0));
            Assert.True(result.IsValid);
        }

        // ── wrong extension ───────────────────────────────────────────────────

        [Theory]
        [InlineData("malware.exe")]
        [InlineData("script.js")]
        [InlineData("archive.zip")]
        [InlineData("spreadsheet.xlsx")]
        [InlineData("image.png")]
        [InlineData("document.docx")]
        [InlineData("hack.bat")]
        [InlineData("noextension")]
        public void Validate_ReturnsInvalid_WhenExtensionIsNotPdf(string fileName)
        {
            var result = _svc.Validate(MakeFile(fileName));
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void Validate_ErrorMessage_ContainsDotExe_ForExeFile()
        {
            var result = _svc.Validate(MakeFile("virus.exe"));
            Assert.False(result.IsValid);
            Assert.Contains(".exe", result.ErrorMessage);
        }

        [Fact]
        public void Validate_ErrorMessage_MentionsPdf_ForWrongExtension()
        {
            var result = _svc.Validate(MakeFile("report.docx"));
            Assert.False(result.IsValid);
            Assert.Contains("PDF", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Validate_ReturnsValid_ForUpperCasePdfExtension()
        {
            // .PDF must be normalised to .pdf — should pass
            var result = _svc.Validate(MakeFile("contract.PDF"));
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_ReturnsValid_ForLowerCasePdf()
        {
            var result = _svc.Validate(MakeFile("agreement.pdf"));
            Assert.True(result.IsValid);
        }

        // ── file size ─────────────────────────────────────────────────────────

        [Fact]
        public void Validate_ReturnsInvalid_WhenFileSizeExceedsTenMb()
        {
            const long elevenMb = 11L * 1024 * 1024;
            var result = _svc.Validate(MakeFileWithSize("big.pdf", elevenMb));
            Assert.False(result.IsValid);
            Assert.Contains("10 MB", result.ErrorMessage);
        }

        [Fact]
        public void Validate_ReturnsInvalid_WhenFileSizeIsOneByteOverLimit()
        {
            const long overLimit = 10L * 1024 * 1024 + 1;
            var result = _svc.Validate(MakeFileWithSize("border.pdf", overLimit));
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_ReturnsValid_WhenFileSizeIsExactlyTenMb()
        {
            const long exactLimit = 10L * 1024 * 1024;
            var result = _svc.Validate(MakeFileWithSize("exact.pdf", exactLimit));
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_ErrorMessage_ContainsMbUnit_WhenFileTooLarge()
        {
            // Message should state the user's file size so they know what to fix
            const long elevenMb = 11L * 1024 * 1024;
            var result = _svc.Validate(MakeFileWithSize("big.pdf", elevenMb));
            Assert.False(result.IsValid);
            Assert.Contains("MB", result.ErrorMessage);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SaveSignedAgreementAsync() — defensive internal guards.
        // These fire if Save() is ever called without going through Validate() first.
        // ═══════════════════════════════════════════════════════════════════════

        // ── null / empty guards ───────────────────────────────────────────────

        [Fact]
        public async Task Save_Throws_WhenFileIsNull()
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _svc.SaveSignedAgreementAsync(null!));
            // Refactored message — "No file was provided."
            Assert.Contains("No file was provided", ex.Message);
        }

        [Fact]
        public async Task Save_Throws_WhenFileIsEmpty()
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _svc.SaveSignedAgreementAsync(MakeFileWithSize("empty.pdf", 0)));
            Assert.Contains("No file was provided", ex.Message);
        }

        // ── extension guard inside Save ───────────────────────────────────────

        [Theory]
        [InlineData("malware.exe")]
        [InlineData("script.js")]
        [InlineData("archive.zip")]
        [InlineData("spreadsheet.xlsx")]
        [InlineData("image.png")]
        [InlineData("document.docx")]
        [InlineData("hack.bat")]
        public async Task Save_Throws_WhenExtensionIsNotPdf(string fileName)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _svc.SaveSignedAgreementAsync(MakeFile(fileName)));
            Assert.Contains("Only PDF files are allowed", ex.Message);
        }

        [Fact]
        public async Task Save_Throws_ForExeFile_MessageContainsDotExe()
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _svc.SaveSignedAgreementAsync(MakeFile("virus.exe")));
            Assert.Contains(".exe", ex.Message);
        }

        [Fact]
        public async Task Save_Succeeds_WhenExtensionIsUpperCasePdf()
        {
            var (path, name) = await _svc.SaveSignedAgreementAsync(MakeFile("contract.PDF"));
            Assert.EndsWith(".pdf", path);
            Assert.Equal("contract.PDF", name);
        }

        // ── size guard inside Save ────────────────────────────────────────────

        [Fact]
        public async Task Save_Throws_WhenFileSizeExceedsTenMb()
        {
            const long elevenMb = 11L * 1024 * 1024;
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _svc.SaveSignedAgreementAsync(MakeFileWithSize("big.pdf", elevenMb)));
            Assert.Contains("10 MB", ex.Message);
        }

        [Fact]
        public async Task Save_Throws_WhenFileSizeIsOneByteOverLimit()
        {
            const long overLimit = 10L * 1024 * 1024 + 1;
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _svc.SaveSignedAgreementAsync(MakeFileWithSize("border.pdf", overLimit)));
        }

        [Fact]
        public async Task Save_Succeeds_WhenFileSizeIsExactlyTenMb()
        {
            const long exactLimit = 10L * 1024 * 1024;
            var (path, _) = await _svc.SaveSignedAgreementAsync(MakeFileWithSize("exact.pdf", exactLimit));
            Assert.NotEmpty(path);
        }

        // ── happy path ────────────────────────────────────────────────────────

        [Fact]
        public async Task Save_ReturnsPath_StartingWithUploadsAgreements()
        {
            var (path, _) = await _svc.SaveSignedAgreementAsync(MakeFile("agreement.pdf"));
            Assert.StartsWith("/uploads/agreements/", path);
        }

        [Fact]
        public async Task Save_ReturnsOriginalDisplayName()
        {
            var (_, name) = await _svc.SaveSignedAgreementAsync(MakeFile("client_contract.pdf"));
            Assert.Equal("client_contract.pdf", name);
        }

        [Fact]
        public async Task Save_GeneratesUniqueStoredPaths_ForTwoUploads()
        {
            var (path1, _) = await _svc.SaveSignedAgreementAsync(MakeFile("same.pdf"));
            var (path2, _) = await _svc.SaveSignedAgreementAsync(MakeFile("same.pdf"));
            Assert.NotEqual(path1, path2);
        }

        [Fact]
        public async Task Save_CreatesUploadDirectory_WhenItDoesNotExist()
        {
            var dir = Path.Combine(_tempRoot, "uploads", "agreements");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);

            await _svc.SaveSignedAgreementAsync(MakeFile("new.pdf"));
            Assert.True(Directory.Exists(dir));
        }

        // ── GetPhysicalPath ───────────────────────────────────────────────────

        [Fact]
        public void GetPhysicalPath_StripsLeadingSlash_AndCombinesWithWebRoot()
        {
            var physical = _svc.GetPhysicalPath("/uploads/agreements/some-file.pdf");
            Assert.StartsWith(_tempRoot, physical);
            Assert.Contains("some-file.pdf", physical);
        }

        [Fact]
        public void GetPhysicalPath_WorksWithoutLeadingSlash()
        {
            var physical = _svc.GetPhysicalPath("uploads/agreements/another.pdf");
            Assert.Contains("another.pdf", physical);
        }
    }
}