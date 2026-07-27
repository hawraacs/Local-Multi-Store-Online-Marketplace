using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Managers;
using Multi_Store.Services.Dtos;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner
{
    public class HomeModel : PageModel
    {
        private readonly StoreManager _storeManager;
        private readonly CustomerManager _customerManager;
        private readonly UserManager<User> _userManager;
        private readonly StoryManager _storyManager;
        private readonly MessagingManager _messagingManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<HomeModel> _logger;

        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedImageMimeTypes = { "image/jpeg", "image/png", "image/webp" };
        private static readonly string[] AllowedVideoExtensions = { ".mp4", ".webm", ".mov" };
        private static readonly string[] AllowedVideoMimeTypes = { "video/mp4", "video/webm", "video/quicktime" };
        private const long MaxStoryFileSizeBytes = 25 * 1024 * 1024; // 25 MB (higher than images alone, to allow short video clips)

        // Story duration is read client-side from video.duration and sent back to us —
        // clamp it server-side so a tampered/bogus value can't be persisted.
        private const int MinStoryDurationSeconds = 1;
        private const int MaxStoryDurationSeconds = 120;

        public HomeModel(
            StoreManager storeManager,
            CustomerManager customerManager,
            UserManager<User> userManager,
            StoryManager storyManager,
            MessagingManager messagingManager,
            IWebHostEnvironment webHostEnvironment,
            ILogger<HomeModel> logger)
        {
            _storeManager = storeManager;
            _customerManager = customerManager;
            _userManager = userManager;
            _storyManager = storyManager;
            _messagingManager = messagingManager;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public int? ProductId { get; set; }
        public Store Store { get; set; }
        public List<Product> Products { get; set; } = new();

        public int FollowersCount { get; set; }

        // Moved out of the .cshtml — this is business logic, not presentation.
        public int TotalReviews { get; set; }
        public double AverageRating { get; set; }

        // New: this Store Owner's own active (non-expired) stories, for the story bar at the top of Home
        public List<Story> OwnStories { get; set; } = new();

        // NEW: same stories, enriched with view/like/reply counts for the "My Stories" card
        public List<StoryDTO> OwnStoriesWithStats { get; set; } = new();

        [BindProperty]
        public IFormFile? StoryMedia { get; set; }

        [BindProperty]
        public string? StoryCaption { get; set; }

        // Set client-side (JS reads video.duration on file selection) so the viewer's
        // progress bar can sync to the real video length instead of guessing.
        [BindProperty]
        public int? StoryDurationSeconds { get; set; }

        [TempData]
        public string? StoryUploadError { get; set; }

        // General page-load error surfaced to the view (e.g. a manager call failing),
        // so the Store Owner sees a friendly message instead of an unhandled exception page.
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToPage("/Login");

                // ?? get store of current owner
                Store = await _storeManager.GetByUserIdAsync(user.Id);

                if (Store == null)
                    return RedirectToPage("/StoreOwner/Dashboard");

                Products = await _storeManager.GetStoreProductsAsync(Store.StoreID);
                FollowersCount = await _storeManager.GetFollowersCountAsync(Store.StoreID);
                OwnStories = await _storyManager.GetOwnStoriesAsync(Store.StoreID);

                OwnStoriesWithStats = new List<StoryDTO>();
                foreach (var story in OwnStories)
                {
                    // NOTE (perf): this issues 3 separate awaited queries per story (N+1).
                    // For a store with many active stories this adds up fast. Consider
                    // adding a single batch method, e.g.
                    //   StoryManager.GetStatsForStoriesAsync(IEnumerable<int> storyIds)
                    // that returns view/like/reply counts for all of them in one query
                    // (a GROUP BY on storyId), then map the results here instead of
                    // awaiting three calls per story in the loop.
                    var views = await _storyManager.GetViewsForStoryAsync(story.StoryID);
                    var likeCount = await _storyManager.GetLikeCountAsync(story.StoryID);
                    var replies = await _messagingManager.GetStoryRepliesAsync(story.StoryID);

                    OwnStoriesWithStats.Add(new StoryDTO
                    {
                        StoryID = story.StoryID,
                        StoreID = story.StoreID,
                        MediaType = story.MediaType,
                        ImageUrl = story.ImageUrl,
                        VideoUrl = story.VideoUrl,
                        DurationSeconds = story.DurationSeconds,
                        Caption = story.Caption,
                        CreatedAt = story.CreatedAt,
                        ViewCount = views.Count,
                        LikeCount = likeCount,
                        ReplyCount = replies.Count
                    });
                }

                TotalReviews = Products.Sum(p => p.Reviews?.Count ?? 0);
                var totalRatingSum = Products.Sum(p => p.Reviews?.Sum(r => r.Rating) ?? 0);
                AverageRating = TotalReviews > 0 ? (double)totalRatingSum / TotalReviews : 0;

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Store Owner Home page for user {UserId}.", User?.Identity?.Name);

                Products ??= new List<Product>();
                OwnStories ??= new List<Story>();
                OwnStoriesWithStats ??= new List<StoryDTO>();
                ErrorMessage = "Something went wrong while loading your store. Please refresh the page or try again shortly.";

                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteReviewAsync(
    int reviewId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Login");

            await _storeManager.DeleteProductReviewAsync(
                reviewId,
                user.Id);

            return RedirectToPage();
        }

        // ================= UPLOAD STORY (NEW) - image OR video =================
        public async Task<IActionResult> OnPostUploadStoryAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            var store = await _storeManager.GetByUserIdAsync(user.Id);
            if (store == null)
                return RedirectToPage("/StoreOwner/Dashboard");

            if (StoryMedia == null || StoryMedia.Length == 0)
            {
                StoryUploadError = "Please choose an image or video to upload.";
                return RedirectToPage();
            }

            if (StoryMedia.Length > MaxStoryFileSizeBytes)
            {
                StoryUploadError = "File is too large. Maximum size is 25 MB.";
                return RedirectToPage();
            }

            var extension = Path.GetExtension(StoryMedia.FileName).ToLower();
            var contentType = StoryMedia.ContentType?.ToLower();

            var isImage = AllowedImageExtensions.Contains(extension) && AllowedImageMimeTypes.Contains(contentType);
            var isVideo = AllowedVideoExtensions.Contains(extension) && AllowedVideoMimeTypes.Contains(contentType);

            if (!isImage && !isVideo)
            {
                StoryUploadError = "Only JPG, PNG, WEBP images or MP4, WEBM, MOV videos are allowed.";
                return RedirectToPage();
            }

            // Extension + declared Content-Type are both client-supplied and can be
            // spoofed (e.g. renaming a .exe to .jpg). Peek at the actual file header
            // (magic bytes) so we're not trusting the client's word for it.
            if (!await HasValidFileSignatureAsync(StoryMedia, isVideo))
            {
                StoryUploadError = "That file doesn't look like a valid image or video. Please try a different file.";
                return RedirectToPage();
            }

            // Clamp the client-reported duration instead of trusting it outright.
            int? clampedDuration = null;
            if (isVideo && StoryDurationSeconds.HasValue)
            {
                clampedDuration = Math.Clamp(StoryDurationSeconds.Value, MinStoryDurationSeconds, MaxStoryDurationSeconds);
            }

            var mediaType = isVideo ? "Video" : "Image";
            var savedUrl = await SaveStoryMediaAsync(store.StoreID, StoryMedia);

            await _storyManager.CreateStoryAsync(
                store.StoreID,
                mediaType,
                imageUrl: isImage ? savedUrl : null,
                videoUrl: isVideo ? savedUrl : null,
                durationSeconds: isVideo ? clampedDuration : null,
                caption: StoryCaption);

            return RedirectToPage();
        }

        // ================= DELETE / DEACTIVATE OWN STORY (NEW) =================
        public async Task<IActionResult> OnPostDeleteStoryAsync(int storyId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            var store = await _storeManager.GetByUserIdAsync(user.Id);
            if (store == null)
                return RedirectToPage("/StoreOwner/Dashboard");

            // Ownership-checked inside the manager/repository - a store owner can only
            // deactivate their own stories, never someone else's.
            await _storyManager.DeactivateStoryAsync(storyId, user.Id);

            return RedirectToPage();
        }

        // ================= STORY INSIGHTS (NEW) - Store Owner only, read-only =================
        public async Task<IActionResult> OnGetStoryInsightsAsync(int storyId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false, error = "Not authenticated." });

            // Ownership-checked: a Store Owner can only see Insights for their own stories.
            var story = await _storyManager.GetStoryForOwnerAsync(storyId, user.Id);
            if (story == null)
                return new JsonResult(new { success = false, error = "Story not found." });

            var views = await _storyManager.GetViewsForStoryAsync(storyId);
            var likes = await _storyManager.GetLikesForStoryAsync(storyId);
            var replies = await _messagingManager.GetStoryRepliesAsync(storyId);

            var insights = new
            {
                storyId = storyId,
                totalViews = views.Count,
                totalLikes = likes.Count,
                totalReplies = replies.Count,
                viewers = views.Select(v => new
                {
                    customerId = v.CustomerID,
                    fullName = v.Customer?.User?.FullName ?? "Customer",
                    userName = v.Customer?.User?.UserName,
                    actionAt = v.ViewedAt
                }),
                likes = likes.Select(l => new
                {
                    customerId = l.CustomerID,
                    fullName = l.Customer?.User?.FullName ?? "Customer",
                    userName = l.Customer?.User?.UserName,
                    actionAt = l.CreatedAt
                }),
                // Strip the "📷 Replied to your Story\n" prefix added by SendStoryReplyAsync
                // so the Insights panel shows just the customer's actual reply text.
                replies = replies.Select(r => new
                {
                    customerId = r.SenderID,
                    fullName = r.Sender?.FullName ?? "Customer",
                    userName = r.Sender?.UserName,
                    replyText = StripStoryReplyPrefix(r.MessageText),
                    sentAt = r.SentAt
                })
            };

            return new JsonResult(new { success = true, insights });
        }

        private static string StripStoryReplyPrefix(string messageText)
        {
            const string prefix = "📷 Replied to your Story\n";
            return messageText.StartsWith(prefix, StringComparison.Ordinal)
                ? messageText.Substring(prefix.Length)
                : messageText;
        }

        // Checks the first bytes of the uploaded file against known magic numbers for
        // the formats we claim to accept, instead of trusting the file extension and
        // the browser-reported Content-Type alone (both are easily spoofed).
        private static async Task<bool> HasValidFileSignatureAsync(IFormFile file, bool isVideo)
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

            if (!isVideo)
            {
                if (StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return true;                 // JPEG
                if (StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 })) return true;            // PNG
                if (bytesRead >= 12 &&
                    StartsWith(new byte[] { 0x52, 0x49, 0x46, 0x46 }, 0) &&                    // "RIFF"
                    StartsWith(new byte[] { 0x57, 0x45, 0x42, 0x50 }, 8))                       // "WEBP"
                    return true;

                return false;
            }
            else
            {
                if (StartsWith(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 })) return true;            // WEBM/MKV (EBML header)
                if (bytesRead >= 8 && StartsWith(new byte[] { 0x66, 0x74, 0x79, 0x70 }, 4))     // "ftyp" — MP4 and most modern MOV files
                    return true;

                return false;
            }
        }

        private async Task<string> SaveStoryMediaAsync(int storeId, IFormFile mediaFile)
        {
            string uploadFolder = Path.Combine(
                _webHostEnvironment.WebRootPath,
                "uploads",
                "stories",
                storeId.ToString());

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            string extension = Path.GetExtension(mediaFile.FileName);
            string uniqueFileName = $"story_{Guid.NewGuid()}{extension}";
            string filePath = Path.Combine(uploadFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await mediaFile.CopyToAsync(stream);
            }

            return $"/uploads/stories/{storeId}/{uniqueFileName}";
        }
    }
}
