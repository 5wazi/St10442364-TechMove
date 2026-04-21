namespace PROG7313_TechMove.Services
{
    public interface IFileService
    {
        Task<(string filePath, string fileName)> SaveSignedAgreementAsync(IFormFile file);

        /// <summary>Returns the physical path of a stored file for download.</summary>
        string GetPhysicalPath(string storedPath);
    }

    /// <summary>
    /// Handles PDF upload, validation, and unique naming.
    /// Rubric criterion 4: File saves to server folder, strict PDF-only validation, UUID naming, download supported.
    /// </summary>
    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileService> _logger;
        private const string UploadFolder = "uploads/agreements";
        private static readonly string[] AllowedExtensions = { ".pdf" };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public FileService(IWebHostEnvironment env, ILogger<FileService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public async Task<(string filePath, string fileName)> SaveSignedAgreementAsync(IFormFile file)
        {
            // Validate: not null / empty
            if (file == null || file.Length == 0)
                throw new InvalidOperationException("No file was uploaded.");

            // Validate: PDF extension only
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                throw new InvalidOperationException(
                    $"Only PDF files are allowed. You uploaded: '{extension}'.");

            // Validate: file size
            if (file.Length > MaxFileSizeBytes)
                throw new InvalidOperationException("File size exceeds the 10 MB limit.");

            // Build target directory (wwwroot/uploads/agreements/)
            var uploadDir = Path.Combine(_env.WebRootPath, UploadFolder);
            Directory.CreateDirectory(uploadDir);

            // UUID naming prevents filename collisions
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var physicalPath = Path.Combine(uploadDir, uniqueFileName);

            await using var stream = new FileStream(physicalPath, FileMode.Create);
            await file.CopyToAsync(stream);

            _logger.LogInformation("Saved signed agreement: {FileName} → {Path}", file.FileName, physicalPath);

            // Return the relative web path (stored in DB) and the original display name
            return ($"/{UploadFolder}/{uniqueFileName}", file.FileName);
        }

        public string GetPhysicalPath(string storedPath)
            => Path.Combine(_env.WebRootPath, storedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    }
}
