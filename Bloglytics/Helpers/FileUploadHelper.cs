using Microsoft.AspNetCore.Http;

namespace Bloglytics.Helpers
{
    public static class FileUploadHelper
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
        private static readonly long MaxFileSize = 5 * 1024 * 1024; // 5MB

        public static async Task<string> UploadImageAsync(IFormFile file, string uploadFolder, int userId)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            // Validate file extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Invalid file type. Only JPG, JPEG, PNG, and GIF are allowed.");
            }

            // Validate file size
            if (file.Length > MaxFileSize)
            {
                throw new InvalidOperationException("File size exceeds 5MB limit.");
            }

            // Create unique filename
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var fileName = $"{userId}_{timestamp}_{Guid.NewGuid()}{extension}";

            // Create upload directory if it doesn't exist
            var uploadPath = Path.Combine("wwwroot", uploadFolder);
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            // Save file
            var filePath = Path.Combine(uploadPath, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return relative path for database
            return $"/{uploadFolder}/{fileName}";
        }

        public static bool DeleteImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                return false;
            }

            try
            {
                var fullPath = Path.Combine("wwwroot", imagePath.TrimStart('/'));
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static string GenerateSlug(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return string.Empty;
            }

            // Convert to lowercase
            var slug = title.ToLowerInvariant();

            // Remove invalid chars
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");

            // Convert spaces to hyphens
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");

            // Remove duplicate hyphens
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");

            // Trim hyphens from start and end
            slug = slug.Trim('-');

            // Limit length
            if (slug.Length > 100)
            {
                slug = slug.Substring(0, 100).Trim('-');
            }

            return slug;
        }

        public static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength) + "...";
        }

        public static string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalSeconds < 60)
                return "just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} minute(s) ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hour(s) ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)timeSpan.TotalDays} day(s) ago";
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)} month(s) ago";

            return $"{(int)(timeSpan.TotalDays / 365)} year(s) ago";
        }
    }
}