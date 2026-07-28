using Microsoft.AspNetCore.Http;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Products.Shared
{
    /// <summary>
    /// Single source of truth for validating uploaded product media.
    ///
    /// Before this merge, Products/Create.cshtml.cs and Products/Edit.cshtml.cs
    /// each hand-rolled an identical copy of extension/MIME/magic-byte checking
    /// for images (a duplication the Edit.cshtml.cs comments explicitly flagged
    /// as "worth extracting"), and Explore/Create.cshtml.cs had its own
    /// similar-but-not-identical version that also handled video and never
    /// checked magic bytes at all. Now there is exactly one place that knows
    /// what a valid product image or video looks like.
    /// </summary>
    public static class ProductMediaValidator
    {
        public static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        public static readonly string[] AllowedImageMimeTypes = { "image/jpeg", "image/png", "image/webp" };
        public static readonly string[] AllowedVideoExtensions = { ".mp4", ".webm" };

        /// <summary>
        /// Validates an image's declared extension, MIME type, and size.
        /// Does not read the file — pair with <see cref="HasValidImageSignatureAsync"/>
        /// for the actual content check, since extension/Content-Type are both
        /// client-supplied and easily spoofed (e.g. renaming a script to .jpg).
        /// </summary>
        public static string? ValidateImageBasics(IFormFile file, long maxSizeBytes)
        {
            if (file.Length <= 0)
                return $"'{file.FileName}': the image is empty.";

            if (file.Length > maxSizeBytes)
                return $"'{file.FileName}' is too large. Maximum size per image is {maxSizeBytes / (1024 * 1024)} MB.";

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var contentType = file.ContentType?.ToLowerInvariant();

            if (!AllowedImageExtensions.Contains(extension) ||
                (contentType != null && !AllowedImageMimeTypes.Contains(contentType)))
            {
                return $"'{file.FileName}' isn't a supported image type. Use JPG, PNG, or WEBP.";
            }

            return null;
        }

        /// <summary>
        /// Validates a video's declared extension and size (no magic-byte check —
        /// video container signatures are more varied; extension + size limit is
        /// the same bar Explore's Reel upload used previously).
        /// </summary>
        public static string? ValidateVideoBasics(IFormFile file, long maxSizeBytes)
        {
            if (file.Length <= 0)
                return $"{file.FileName}: the video is empty.";

            if (file.Length > maxSizeBytes)
                return $"{file.FileName}: video size cannot exceed {maxSizeBytes / (1024 * 1024)} MB.";

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedVideoExtensions.Contains(extension))
                return $"{file.FileName}: only MP4 and WEBM videos are allowed.";

            return null;
        }

        /// <summary>
        /// Reads the first bytes of the file and checks them against known
        /// JPEG/PNG/WEBP magic-byte signatures, so a renamed non-image file
        /// can't slip past extension/Content-Type checks alone.
        /// </summary>
        public static async Task<bool> HasValidImageSignatureAsync(IFormFile file)
        {
            var buffer = new byte[12];
            int bytesRead;

            using (var stream = file.OpenReadStream())
            {
                bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
            }

            if (bytesRead < 4) return false;

            bool StartsWith(byte[] signature, int offset = 0)
            {
                if (buffer.Length < offset + signature.Length) return false;
                for (int i = 0; i < signature.Length; i++)
                {
                    if (buffer[offset + i] != signature[i]) return false;
                }
                return true;
            }

            if (StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return true;                 // JPEG
            if (StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 })) return true;            // PNG
            if (bytesRead >= 12 &&
                StartsWith(new byte[] { 0x52, 0x49, 0x46, 0x46 }, 0) &&                    // "RIFF"
                StartsWith(new byte[] { 0x57, 0x45, 0x42, 0x50 }, 8))                       // "WEBP"
                return true;

            return false;
        }
    }
}
