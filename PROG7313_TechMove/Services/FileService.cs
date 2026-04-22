namespace PROG7313_TechMove.Services
{
    
    /// Result returned by FileService.Validate().
    /// Keeps validation logic and its outcome entirely inside the service layer.
    
    public sealed class FileValidationResult
    {
        public bool IsValid { get; private init; }
        public string? ErrorMessage { get; private init; }

        public static FileValidationResult Ok()
            => new() { IsValid = true };

        public static FileValidationResult Fail(string message)
            => new() { IsValid = false, ErrorMessage = message };
    }

    public interface IFileService
    {
        
        /// Validates an uploaded file before saving.
        /// Returns a FileValidationResult so the caller never needs to know the rules.
        
        FileValidationResult Validate(IFormFile? file);

        /// Saves a validated signed-agreement PDF and returns its stored path and display name.
        Task<(string filePath, string fileName)> SaveSignedAgreementAsync(IFormFile file);

        /// >Returns the physical path of a stored file for download.
        string GetPhysicalPath(string storedPath);
    }

    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileService> _logger;

        private const string UploadFolder = "uploads/agreements";
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        private static readonly string[] AllowedExtensions = { ".pdf" };

        // Human-readable size for error messages — kept in sync with the constant above.
        private const string MaxFileSizeLabel = "10 MB";

        public FileService(IWebHostEnvironment env, ILogger<FileService> logger)
        {
            _env = env;
            _logger = logger;
        }

        //Validation 
        /// <inheritdoc />
        public FileValidationResult Validate(IFormFile? file)
        {
            // No file uploaded — that is allowed (the field is optional).
            if (file == null || file.Length == 0)
                return FileValidationResult.Ok();

            // Extension check — case-insensitive
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
                return FileValidationResult.Fail(
                    $"Invalid file type '{extension}'. Only PDF files (.pdf) are accepted.");

            // Size check
            if (file.Length > MaxFileSizeBytes)
                return FileValidationResult.Fail(
                    $"The file '{file.FileName}' is too large " +
                    $"({(file.Length / 1024.0 / 1024.0):F1} MB). " +
                    $"Maximum allowed size is {MaxFileSizeLabel}.");

            return FileValidationResult.Ok();
        }

        //Save 

        /// <inheritdoc />
        public async Task<(string filePath, string fileName)> SaveSignedAgreementAsync(IFormFile file)
        {
            // Guard: callers should always call Validate() first, but we double-check here
            // so the service can never silently store an invalid file.
            if (file == null || file.Length == 0)
                throw new InvalidOperationException("No file was provided.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                throw new InvalidOperationException(
                    $"Only PDF files are allowed. Received: '{extension}'.");

            if (file.Length > MaxFileSizeBytes)
                throw new InvalidOperationException(
                    $"File size exceeds the {MaxFileSizeLabel} limit.");

            // Build target directory (wwwroot/uploads/agreements/)
            var uploadDir = Path.Combine(_env.WebRootPath, UploadFolder);
            Directory.CreateDirectory(uploadDir);

            // UUID naming prevents filename collisions
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var physicalPath = Path.Combine(uploadDir, uniqueFileName);

            await using var stream = new FileStream(physicalPath, FileMode.Create);
            await file.CopyToAsync(stream);

            _logger.LogInformation(
                "Saved signed agreement: {OriginalName} → {StoredPath}",
                file.FileName, physicalPath);

            // Relative web path stored in DB; original name kept for display
            return ($"/{UploadFolder}/{uniqueFileName}", file.FileName);
        }

        //  Download helper 

        /// <inheritdoc />
        public string GetPhysicalPath(string storedPath)
            => Path.Combine(
                _env.WebRootPath,
                storedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    }
}