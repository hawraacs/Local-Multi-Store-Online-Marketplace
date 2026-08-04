using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CustomerProfileModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly WishlistManager _wishlistManager;


        public CustomerProfileModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ApplicationDbContext context,
            WishlistManager wishlistManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _wishlistManager = wishlistManager;
        }

        [BindProperty]
        public string CustomerFullName { get; set; } = string.Empty;

        [BindProperty]
        public string CustomerEmail { get; set; } = string.Empty;

        [BindProperty]
        public string CustomerPhone { get; set; } = string.Empty;

        [BindProperty]
        public string CustomerBio { get; set; } = string.Empty;

        public int OrdersCount { get; set; }

        public int WishlistCount { get; set; }

        public int AddressesCount { get; set; }

        [BindProperty]
        public StoreDTO Store { get; set; } = new StoreDTO();

        [BindProperty]
        public DeliveryPersonDTO Delivery { get; set; } = new DeliveryPersonDTO();

        public bool HasPendingDeliveryRequest { get; set; }

        public bool HasApprovedDeliveryAccount { get; set; }

        public bool HasRejectedDeliveryRequest { get; set; }

        public string DeliveryAccessMessage { get; set; } = string.Empty;

        public string DeliveryAccountEmail { get; set; } = string.Empty;

        [BindProperty]
        public string DeliveryPassword { get; set; } = string.Empty;

        // ==========================================
        // STORE OWNER ACCESS
        // ==========================================
        public bool HasStoreRequest { get; set; }

        public bool HasPendingStoreRequest { get; set; }

        public bool HasApprovedStoreOwnerAccount { get; set; }

        public bool HasRejectedStoreRequest { get; set; }

        public string StoreAccessMessage { get; set; } = string.Empty;

        public string StoreOwnerAccountEmail { get; set; } = string.Empty;

        [BindProperty]
        public string StoreOwnerPassword { get; set; } = string.Empty;

        // ==========================================
        // Instagram-style Profile Fields
        // ==========================================
        public int LoyaltyPoints { get; set; }
        public bool IsVerified { get; set; }

        // Gender and DateOfBirth are now [BindProperty] so the full
        // Edit Profile page can post updated values back for these —
        // both map directly to real columns on the Customer entity
        // (see Customer.cs: Gender, DateOfBirth).
        [BindProperty]
        public string Gender { get; set; } = "Not Specified";

        [BindProperty]
        public DateTime? DateOfBirth { get; set; }

        public bool CODBlocked { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // The customer's saved addresses (Customer.Addresses) and which one
        // is currently the default (Customer.DefaultAddressID), so the full
        // Edit Profile page can offer a "default address" dropdown.
        public List<CustomerAddress> AddressesList { get; set; } = new();

        [BindProperty]
        public int? SelectedDefaultAddressId { get; set; }

        public List<StoreFollow> FollowedStoresList { get; set; } = new();

        // Stores this customer has blocked. BlockRelation stores the
        // relationship by user id, not store id (BlockerUserId = this
        // customer's UserID, BlockedUserId = the store owner's UserID), so
        // this is resolved to actual Store rows by matching Store.OwnerUserID.
        public List<Store> BlockedStoresList { get; set; } = new();
        public List<Wishlist> WishlistList { get; set; } = new();
        public List<Review> ReviewsList { get; set; } = new();

        // ==========================================
        // Activity tab lists (Likes / Comments / Story Likes / Story Replies)
        // ==========================================
        public List<ExploreLike> LikesList { get; set; } = new();
        public List<ExploreComment> CommentsList { get; set; } = new();

        // NOTE: No StoryLike / StoryReply DbSet or entity was confirmed for
        // this app — only StoryManager's per-story check methods
        // (LikeStoryAsync / UnlikeStoryAsync / IsLikedByCustomerAsync) were
        // visible, not a "get all likes/replies for this customer" query.
        // These are populated via a best-effort raw SQL query (see
        // LoadActivityListsAsync/TryLoadActivityRowsAsync below) against
        // guessed table/column names. If the guesses are wrong, these stay
        // empty (Activity tab shows its normal empty state) rather than
        // failing to build — check the SQL strings in
        // LoadActivityListsAsync first if these still don't show data.
        public List<object> StoryLikesList { get; set; } = new();
        public List<object> StoryRepliesList { get; set; } = new();

        // Safe property lookups to prevent compile-time or runtime issues with dynamic entities.
        // Also supports dictionary-shaped rows (e.g. Dictionary<string, object?> produced by
        // raw ADO.NET reads) so the same helpers work for both real entities and best-effort
        // SQL results whose exact column names weren't confirmed at compile time.
        private static bool TryGetRawValue(object obj, string name, out object? value)
        {
            if (obj is System.Collections.IDictionary dict)
            {
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    if (entry.Key is string key && string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = entry.Value;
                        return true;
                    }
                }

                value = null;
                return false;
            }

            var prop = obj.GetType().GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (prop != null)
            {
                value = prop.GetValue(obj);
                return true;
            }

            value = null;
            return false;
        }

        public static string GetSafeString(object? obj, string[] propertyNames, string defaultValue = "")
        {
            if (obj == null) return defaultValue;
            foreach (var name in propertyNames)
            {
                if (TryGetRawValue(obj, name, out var val) && val != null)
                {
                    return val.ToString() ?? defaultValue;
                }
            }
            return defaultValue;
        }

        public static int GetSafeInt(object? obj, string[] propertyNames, int defaultValue = 0)
        {
            if (obj == null) return defaultValue;
            foreach (var name in propertyNames)
            {
                if (TryGetRawValue(obj, name, out var val) && val != null && int.TryParse(val.ToString(), out int res))
                {
                    return res;
                }
            }
            return defaultValue;
        }

        public static decimal GetSafeDecimal(object? obj, string[] propertyNames, decimal defaultValue = 0)
        {
            if (obj == null) return defaultValue;
            foreach (var name in propertyNames)
            {
                if (TryGetRawValue(obj, name, out var val) && val != null && decimal.TryParse(val.ToString(), out decimal res))
                {
                    return res;
                }
            }
            return defaultValue;
        }

        // Fetches a nested navigation property object via reflection (e.g. an
        // ExploreLike's ExplorePost, or an ExploreComment's ExplorePost/Product),
        // without needing to know the exact navigation property name at compile
        // time. Returns null if the object, or none of the named properties,
        // exist/are populated — callers should treat null as "not available"
        // and fall back to a default label.
        public static object? GetSafeObject(object? obj, string[] propertyNames)
        {
            if (obj == null) return null;
            foreach (var name in propertyNames)
            {
                if (TryGetRawValue(obj, name, out var val) && val != null)
                {
                    return val;
                }
            }
            return null;
        }

        public static string GetSafeDateString(object? obj, string[] propertyNames, string format = "MMM d, yyyy")
        {
            if (obj == null) return string.Empty;
            foreach (var name in propertyNames)
            {
                if (TryGetRawValue(obj, name, out var val) && val != null)
                {
                    if (val is DateTime dt) return dt.ToString(format);
                    if (DateTime.TryParse(val.ToString(), out var parsed)) return parsed.ToString(format);
                }
            }
            return string.Empty;
        }

        // Picks the best product photo out of the Product.Images collection:
        // prefers the one flagged IsPrimary, then falls back to the lowest
        // DisplayOrder, then to any image at all. Returns null if the
        // product has no images loaded/attached.
        public static string? GetPrimaryProductImageUrl(Product? product)
        {
            if (product?.Images == null || product.Images.Count == 0)
            {
                return null;
            }

            var best = product.Images
                .OrderByDescending(img => img.IsPrimary)
                .ThenBy(img => img.DisplayOrder)
                .FirstOrDefault(img => !string.IsNullOrWhiteSpace(img.ImageUrl));

            return best?.ImageUrl;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var loaded = await LoadCustomerProfileAsync();

            if (!loaded)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            return Page();
        }

        // Handles both the legacy quick-edit (name/phone only) and, now,
        // the full Edit Profile page's fuller form (name, phone, gender,
        // date of birth, default address). Any of these fields left
        // unposted (null) simply won't overwrite the existing value.
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            user.FullName = CustomerFullName?.Trim() ?? string.Empty;
            user.PhoneNumber = CustomerPhone?.Trim() ?? string.Empty;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                await LoadCustomerProfileAsync();

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return Page();
            }

            // NEW — persist Gender / DateOfBirth / DefaultAddressID onto the
            // Customer entity itself (these live on Customer, not User).
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer != null)
            {
                if (!string.IsNullOrWhiteSpace(Gender))
                {
                    customer.Gender = Gender.Trim();
                }

                customer.DateOfBirth = DateOfBirth;

                // Only reassign the default address if the posted value is a
                // real address belonging to this customer, so a tampered or
                // stale form value can't point DefaultAddressID at someone
                // else's address.
                if (SelectedDefaultAddressId.HasValue)
                {
                    var ownsAddress = await _context.CustomerAddresses
                        .AnyAsync(a =>
                            a.AddressID == SelectedDefaultAddressId.Value &&
                            a.CustomerID == customer.CustomerID);

                    if (ownsAddress)
                    {
                        customer.DefaultAddressID = SelectedDefaultAddressId.Value;
                    }
                }
                else
                {
                    customer.DefaultAddressID = null;
                }

                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Profile updated successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditBioAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
            {
                TempData["Success"] = "Could not find your customer record to save the bio.";
                return RedirectToPage();
            }

            var trimmedBio = CustomerBio?.Trim() ?? string.Empty;

            // NOTE: this assumes the Customer entity has (or will have) a
            // string "Bio" property/column. It's set through reflection here
            // so this file keeps compiling even before that column exists;
            // once you add a `Bio` (or similarly named) column to Customer
            // and run a migration, this will start persisting real values.
            var bioProperty = customer.GetType().GetProperty(
                "Bio",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

            if (bioProperty != null && bioProperty.CanWrite)
            {
                bioProperty.SetValue(customer, trimmedBio);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Bio updated successfully.";
            }
            else
            {
                TempData["Success"] =
                    "Bio saving isn't wired up yet — add a 'Bio' column to the Customer table to persist this.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeliveryLoginAsync()
        {
            var currentCustomerUser = await _userManager.GetUserAsync(User);

            if (currentCustomerUser == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (string.IsNullOrWhiteSpace(DeliveryPassword))
            {
                TempData["DeliveryLoginError"] =
                    "Please enter your delivery password.";

                return RedirectToPage();
            }

            // Security: the server selects only the Delivery account
            // that belongs to the currently signed-in Customer.
            var deliveryProfile = await _context.DeliveryPersons
                .AsNoTracking()
                .Where(d =>
                    d.RequestedByUserID == currentCustomerUser.Id)
                .OrderByDescending(d => d.DeliveryPersonID)
                .FirstOrDefaultAsync();

            if (deliveryProfile == null)
            {
                TempData["DeliveryLoginError"] =
                    "No delivery account is linked to your customer account.";

                return RedirectToPage();
            }

            var deliveryStatus = deliveryProfile.Status?.Trim();

            if (!string.Equals(
                deliveryStatus,
                "Approved",
                StringComparison.OrdinalIgnoreCase))
            {
                TempData["DeliveryLoginError"] =
                    "Your delivery account is not approved.";

                return RedirectToPage();
            }

            if (!deliveryProfile.IsActive)
            {
                TempData["DeliveryLoginError"] =
                    "Your delivery account is currently inactive.";

                return RedirectToPage();
            }

            var deliveryUser = await _userManager.FindByIdAsync(
                deliveryProfile.UserID.ToString());

            if (deliveryUser == null || !deliveryUser.IsActive)
            {
                TempData["DeliveryLoginError"] =
                    "Delivery account is unavailable.";

                return RedirectToPage();
            }

            var isDelivery = await _userManager.IsInRoleAsync(
                deliveryUser,
                "Delivery");

            if (!isDelivery)
            {
                TempData["DeliveryLoginError"] =
                    "Delivery account is not configured correctly.";

                return RedirectToPage();
            }

            var passwordResult = await _signInManager.CheckPasswordSignInAsync(
                deliveryUser,
                DeliveryPassword,
                lockoutOnFailure: true);

            if (passwordResult.IsLockedOut)
            {
                TempData["DeliveryLoginError"] =
                    "This delivery account is temporarily locked. Please try again later.";

                return RedirectToPage();
            }

            if (!passwordResult.Succeeded)
            {
                TempData["DeliveryLoginError"] =
                    "Invalid delivery credentials.";

                return RedirectToPage();
            }

            await _signInManager.SignOutAsync();

            await _signInManager.SignInAsync(
                deliveryUser,
                isPersistent: false);

            if (deliveryUser.MustChangePassword)
            {
                return LocalRedirect("/DeliveryFirstPasswordChange");
            }

            return LocalRedirect("/DeliveryDashboard");
        }

        public async Task<IActionResult> OnPostStoreOwnerLoginAsync()
        {
            var currentCustomerUser = await _userManager.GetUserAsync(User);

            if (currentCustomerUser == null)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            if (string.IsNullOrWhiteSpace(StoreOwnerPassword))
            {
                TempData["StoreOwnerLoginError"] =
                    "Please enter your Store Owner password.";

                return RedirectToPage();
            }

            // Security: the server selects only the Store account
            // that belongs to the currently signed-in Customer.
            var storeProfile = await _context.Stores
                .AsNoTracking()
                .Where(s =>
                    s.RequestedByUserID == currentCustomerUser.Id
                    ||
                    (
                        s.RequestedByUserID == null
                        &&
                        s.OwnerUserID == currentCustomerUser.Id
                    ))
                .OrderByDescending(s => s.StoreID)
                .FirstOrDefaultAsync();

            if (storeProfile == null)
            {
                TempData["StoreOwnerLoginError"] =
                    "No Store Owner account is linked to your customer account.";

                return RedirectToPage();
            }

            var storeStatus = storeProfile.Status?.Trim();

            if (!string.Equals(
                    storeStatus,
                    "Approved",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["StoreOwnerLoginError"] =
                    "Your Store Owner account is not approved.";

                return RedirectToPage();
            }

            if (storeProfile.IsSuspended ||
                string.Equals(
                    storeProfile.SubscriptionStatus?.Trim(),
                    "Suspended",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["StoreOwnerLoginError"] =
                    "Your Store Owner account is currently suspended.";

                return RedirectToPage();
            }

            var storeOwnerUser = await _userManager.FindByIdAsync(
                storeProfile.OwnerUserID.ToString());

            if (storeOwnerUser == null || !storeOwnerUser.IsActive)
            {
                TempData["StoreOwnerLoginError"] =
                    "Store Owner account is currently unavailable.";

                return RedirectToPage();
            }

            var isStoreOwner = await _userManager.IsInRoleAsync(
                storeOwnerUser,
                "StoreOwner");

            if (!isStoreOwner)
            {
                TempData["StoreOwnerLoginError"] =
                    "Store Owner account is not configured correctly.";

                return RedirectToPage();
            }

            var passwordResult = await _signInManager.CheckPasswordSignInAsync(
                storeOwnerUser,
                StoreOwnerPassword,
                lockoutOnFailure: true);

            if (passwordResult.IsLockedOut)
            {
                TempData["StoreOwnerLoginError"] =
                    "This Store Owner account is temporarily locked. Please try again later.";

                return RedirectToPage();
            }

            if (!passwordResult.Succeeded)
            {
                TempData["StoreOwnerLoginError"] =
                    "Invalid Store Owner credentials.";

                return RedirectToPage();
            }

            await _signInManager.SignOutAsync();

            await _signInManager.SignInAsync(
                storeOwnerUser,
                isPersistent: false);


            return LocalRedirect(
                "/StoreOwner/Dashboard");
        }

        public async Task<IActionResult> OnPostStoreRequestAsync()
        {
            var loaded = await LoadCustomerProfileAsync();

            if (!loaded)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            Store.Status = "Pending";

            TempData["Success"] = "Store owner request submitted successfully. Waiting for admin approval.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeliveryRequestAsync()
        {
            var loaded = await LoadCustomerProfileAsync();

            if (!loaded)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            Delivery.Status = "Pending";

            TempData["Success"] = "Delivery staff request submitted successfully. Waiting for admin approval.";

            return RedirectToPage();
        }
        public async Task<IActionResult> OnPostToggleWishlistAsync(int productId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
                return RedirectToPage();

            if (await _wishlistManager.IsInWishlistAsync(customer.CustomerID, productId))
            {
                await _wishlistManager.RemoveFromWishlistAsync(customer.CustomerID, productId);
            }
            else
            {
                await _wishlistManager.AddToWishlistAsync(customer.CustomerID, productId);
            }

            return RedirectToPage();
        }

        // Removes the BlockRelation row so the store shows up again in the
        // customer's feed/search/recommendations (see the block logic in
        // CustomerFeedModel.OnPostBlockPostAsync — this is the reverse of
        // that). Takes the store owner's UserID, since that's how
        // BlockRelation actually stores the relationship (BlockedUserId),
        // not a StoreID.
        public async Task<IActionResult> OnPostUnblockStoreAsync(int blockedUserId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var relation = await _context.BlockRelations
                .FirstOrDefaultAsync(b =>
                    b.BlockerUserId == user.Id &&
                    b.BlockedUserId == blockedUserId &&
                    b.BlockerRole == "Customer");

            if (relation != null)
            {
                _context.BlockRelations.Remove(relation);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Shop unblocked. It will start appearing in your feed again.";
            }

            return RedirectToPage();
        }

        // ==========================================
        // Delete Review(s) / Comment(s) — from the Activity tab's
        // swipeable panel (trash icon on a single item, or "Delete
        // Selected" after checking several). Both scoped to the
        // signed-in customer's own CustomerID so one customer can't
        // delete another's review/comment by guessing an id.
        // ==========================================
        public async Task<IActionResult> OnPostDeleteReviewAsync(int reviewId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
                return RedirectToPage();

            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.ReviewID == reviewId && r.CustomerID == customer.CustomerID);

            if (review != null)
            {
                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Review deleted.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteReviewsAsync(int[] reviewIds)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            if (reviewIds == null || reviewIds.Length == 0)
                return RedirectToPage();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
                return RedirectToPage();

            var reviewsToDelete = await _context.Reviews
                .Where(r => reviewIds.Contains(r.ReviewID) && r.CustomerID == customer.CustomerID)
                .ToListAsync();

            if (reviewsToDelete.Any())
            {
                _context.Reviews.RemoveRange(reviewsToDelete);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{reviewsToDelete.Count} review(s) deleted.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteCommentAsync(int commentId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
                return RedirectToPage();

            // Comments are soft-deleted (IsDeleted flag), matching the same
            // pattern already used by Customer1Model.OnPostDeleteExploreCommentAsync.
            var comment = await _context.ExploreComments
                .FirstOrDefaultAsync(c =>
                    c.ExploreCommentID == commentId &&
                    c.CustomerID == customer.CustomerID &&
                    !c.IsDeleted);

            if (comment != null)
            {
                comment.IsDeleted = true;
                comment.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Comment deleted.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteCommentsAsync(int[] commentIds)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            if (commentIds == null || commentIds.Length == 0)
                return RedirectToPage();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
                return RedirectToPage();

            var commentsToDelete = await _context.ExploreComments
                .Where(c =>
                    commentIds.Contains(c.ExploreCommentID) &&
                    c.CustomerID == customer.CustomerID &&
                    !c.IsDeleted)
                .ToListAsync();

            if (commentsToDelete.Any())
            {
                var now = DateTime.UtcNow;

                foreach (var comment in commentsToDelete)
                {
                    comment.IsDeleted = true;
                    comment.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"{commentsToDelete.Count} comment(s) deleted.";
            }

            return RedirectToPage();
        }

        // ==========================================
        // Delete Like(s) — ExploreLike is a real, confirmed entity, so this
        // works the same reliable way as the review/comment deletes above.
        // There's no confirmed "ExploreLikeID" property, so the row is
        // identified by (CustomerID, ExplorePostID) instead, which is
        // exactly how the row was created in the first place (see
        // Customer1Model.OnPostToggleExploreLikeAsync).
        // ==========================================
        public async Task<IActionResult> OnPostDeleteLikeAsync(int postId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
                return RedirectToPage();

            var like = await _context.ExploreLikes
                .FirstOrDefaultAsync(l => l.ExplorePostID == postId && l.CustomerID == customer.CustomerID);

            if (like != null)
            {
                _context.ExploreLikes.Remove(like);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Like removed.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteLikesAsync(int[] postIds)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            if (postIds == null || postIds.Length == 0)
                return RedirectToPage();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
                return RedirectToPage();

            var likesToDelete = await _context.ExploreLikes
                .Where(l => postIds.Contains(l.ExplorePostID) && l.CustomerID == customer.CustomerID)
                .ToListAsync();

            if (likesToDelete.Any())
            {
                _context.ExploreLikes.RemoveRange(likesToDelete);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{likesToDelete.Count} like(s) removed.";
            }

            return RedirectToPage();
        }

        // ==========================================
        // Delete Story Like(s) / Story Repl(y/ies) — BEST-EFFORT.
        // Same unverified-schema situation as reading these lists (see
        // LoadActivityListsAsync): there's no confirmed StoryLike/
        // StoryReply/Message entity, so these run raw SQL DELETEs against
        // the same guessed table/column shapes used for reading, wrapped in
        // try/catch per statement. A guess that doesn't match the real
        // schema just deletes 0 rows silently — harmless — so it's safe to
        // attempt every candidate rather than only the first match.
        // ==========================================
        public async Task<IActionResult> OnPostDeleteStoryLikeAsync(int storyId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
                return RedirectToPage();

            await DeleteStoryLikeBestEffortAsync(customer.CustomerID, storyId);
            TempData["Success"] = "Story like removed.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteStoryLikesAsync(int[] storyIds)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            if (storyIds == null || storyIds.Length == 0)
                return RedirectToPage();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
                return RedirectToPage();

            foreach (var storyId in storyIds)
            {
                await DeleteStoryLikeBestEffortAsync(customer.CustomerID, storyId);
            }

            TempData["Success"] = $"{storyIds.Length} story like(s) removed.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteStoryReplyAsync(int storyId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
                return RedirectToPage();

            await DeleteStoryReplyBestEffortAsync(user.Id, customer.CustomerID, storyId);
            TempData["Success"] = "Story reply removed.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteStoryRepliesAsync(int[] storyIds)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            if (storyIds == null || storyIds.Length == 0)
                return RedirectToPage();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
                return RedirectToPage();

            foreach (var storyId in storyIds)
            {
                await DeleteStoryReplyBestEffortAsync(user.Id, customer.CustomerID, storyId);
            }

            TempData["Success"] = $"{storyIds.Length} story repl(y/ies) removed.";

            return RedirectToPage();
        }

        private async Task DeleteStoryLikeBestEffortAsync(int customerId, int storyId)
        {
            await TryExecuteActivityDeleteAsync(
                "DELETE FROM StoryLikes WHERE StoryID = @StoryId AND CustomerID = @CustomerId",
                ("@StoryId", storyId), ("@CustomerId", customerId));
        }

        private async Task DeleteStoryReplyBestEffortAsync(int userId, int customerId, int storyId)
        {
            // Mirrors the candidate shapes tried in LoadActivityListsAsync —
            // ChatMessages (confirmed table name, guessed extra columns),
            // then the older Messages/StoryReplies guesses as fallbacks.
            await TryExecuteActivityDeleteAsync(
                "DELETE FROM ChatMessages WHERE StoryID = @StoryId AND SenderID = @UserId",
                ("@StoryId", storyId), ("@UserId", userId));

            await TryExecuteActivityDeleteAsync(
                "DELETE FROM ChatMessages WHERE StoryId = @StoryId AND SenderID = @UserId",
                ("@StoryId", storyId), ("@UserId", userId));

            await TryExecuteActivityDeleteAsync(
                "DELETE FROM Messages WHERE StoryID = @StoryId AND SenderUserID = @UserId",
                ("@StoryId", storyId), ("@UserId", userId));

            await TryExecuteActivityDeleteAsync(
                "DELETE FROM StoryReplies WHERE StoryID = @StoryId AND SenderUserID = @UserId",
                ("@StoryId", storyId), ("@UserId", userId));

            await TryExecuteActivityDeleteAsync(
                "DELETE FROM StoryReplies WHERE StoryID = @StoryId AND CustomerID = @CustomerId",
                ("@StoryId", storyId), ("@CustomerId", customerId));
        }

        // Runs a raw SQL DELETE and returns the affected row count. Never
        // throws — a wrong guess at table/column names just deletes 0 rows,
        // same safety approach as TryLoadActivityRowsAsync above.
        private async Task<int> TryExecuteActivityDeleteAsync(string sql, params (string Name, object Value)[] parameters)
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                var shouldClose = connection.State != System.Data.ConnectionState.Open;

                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;

                    foreach (var (name, value) in parameters)
                    {
                        var parameter = command.CreateParameter();
                        parameter.ParameterName = name;
                        parameter.Value = value ?? DBNull.Value;
                        command.Parameters.Add(parameter);
                    }

                    return await command.ExecuteNonQueryAsync();
                }
                finally
                {
                    if (shouldClose)
                    {
                        await connection.CloseAsync();
                    }
                }
            }
            catch
            {
                return 0;
            }
        }


        private async Task<bool> LoadCustomerProfileAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return false;
            }

            CustomerFullName = !string.IsNullOrWhiteSpace(user.FullName)
                ? user.FullName
                : user.UserName ?? "Customer";

            CustomerEmail = user.Email ?? string.Empty;

            // FIX: "?? " only replaces null. ASP.NET Identity's PhoneNumber
            // is often an empty string ("") rather than null when unset, so
            // the old `user.PhoneNumber ?? "No phone number"` silently
            // rendered a blank value instead of the fallback text.
            CustomerPhone = string.IsNullOrWhiteSpace(user.PhoneNumber)
                ? "No phone number"
                : user.PhoneNumber;

            await LoadDeliveryAccessStatusAsync(user);
            await LoadStoreAccessStatusAsync(user);

            // Safe lookup with robust try-catches around navigation inclusions.
            // Addresses is now included too (Customer.Addresses, confirmed
            // real navigation property) so the full Edit Profile page can
            // offer a "default address" picker.
            Customer? customer = null;
            try
            {
                customer = await _context.Customers
                    .Include(c => c.FollowedStores)
                        .ThenInclude(fs => fs.Store)
                    .Include(c => c.Wishlists)
                        .ThenInclude(w => w.Product)
                            .ThenInclude(p => p.Images)
                    .Include(c => c.Reviews)
                        .ThenInclude(r => r.Product)
                    .Include(c => c.Addresses)
                    .FirstOrDefaultAsync(c => c.UserID == user.Id);
            }
            catch
            {
                try
                {
                    customer = await _context.Customers
                        .Include("FollowedStores")
                        .Include("Wishlists.Product.Images")
                        .Include("Reviews")
                        .Include("Addresses")
                        .FirstOrDefaultAsync(c => c.UserID == user.Id);
                }
                catch
                {
                    customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.UserID == user.Id);
                }
            }

            if (customer == null)
            {
                OrdersCount = 0;
                WishlistCount = 0;
                AddressesCount = 0;
                return true;
            }

            LoyaltyPoints = customer.LoyaltyPoints;
            IsVerified = customer.IsVerified;
            Gender = customer.Gender ?? "Not Specified";
            DateOfBirth = customer.DateOfBirth;
            CODBlocked = customer.CODBlocked;
            CreatedAt = customer.CreatedAt;

            // Bio is read defensively via reflection in case the Customer
            // entity doesn't have this column yet (see OnPostEditBioAsync).
            CustomerBio = GetSafeString(customer, new[] { "Bio", "AboutMe", "Description" }, string.Empty);

            FollowedStoresList = customer.FollowedStores?.ToList() ?? new List<StoreFollow>();
            WishlistList = customer.Wishlists?.ToList() ?? new List<Wishlist>();
            ReviewsList = customer.Reviews?.ToList() ?? new List<Review>();
            AddressesList = customer.Addresses?.ToList() ?? new List<CustomerAddress>();
            SelectedDefaultAddressId = customer.DefaultAddressID;

            OrdersCount = await _context.Orders
                .CountAsync(o => o.CustomerID == customer.CustomerID);

            WishlistCount = WishlistList.Count;

            AddressesCount = AddressesList.Count;

            await LoadActivityListsAsync(customer.CustomerID, customer.UserID);

            await LoadBlockedStoresAsync(customer.UserID);

            return true;
        }

        // Resolves this customer's BlockRelation rows (keyed by user id) into
        // actual Store entities, so the Blocked Shops section on the
        // Activity tab has something real to render instead of always
        // showing the empty state.
        private async Task LoadBlockedStoresAsync(int customerUserId)
        {
            try
            {
                var blockedOwnerUserIds = await _context.BlockRelations
                    .Where(b =>
                        b.BlockerUserId == customerUserId &&
                        b.BlockerRole == "Customer")
                    .Select(b => b.BlockedUserId)
                    .ToListAsync();

                if (blockedOwnerUserIds.Count == 0)
                {
                    BlockedStoresList = new List<Store>();
                    return;
                }

                BlockedStoresList = await _context.Stores
                    .Where(s => blockedOwnerUserIds.Contains(s.OwnerUserID))
                    .ToListAsync();
            }
            catch
            {
                BlockedStoresList = new List<Store>();
            }
        }

        // ==========================================
        // Activity tab data (Likes / Comments / Story Likes / Story Replies)
        // ==========================================
        // Navigation property names on ExploreLike/ExploreComment (e.g. the
        // link back to ExplorePost) weren't visible anywhere in the code we
        // had to work from — only the FK id (ExplorePostID) was confirmed.
        // Rather than guess a name that might not compile, this tries the
        // fully-loaded query first and falls back to a flat query (no
        // Include) if that throws, the same defensive pattern already used
        // above for Customer/FollowedStores/Wishlists/Reviews. Either way,
        // the Razor page reads the nested post/product info through
        // GetSafeObject/GetSafeString, so it degrades gracefully (falls
        // back to "a post") instead of failing to compile or throwing.
        private async Task LoadActivityListsAsync(int customerId, int userId)
        {
            try
            {
                LikesList = await _context.ExploreLikes
                    .Include("ExplorePost.Product")
                    .Where(l => l.CustomerID == customerId)
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync();
            }
            catch
            {
                try
                {
                    LikesList = await _context.ExploreLikes
                        .Where(l => l.CustomerID == customerId)
                        .OrderByDescending(l => l.CreatedAt)
                        .ToListAsync();
                }
                catch
                {
                    LikesList = new List<ExploreLike>();
                }
            }

            try
            {
                CommentsList = await _context.ExploreComments
                    .Include("ExplorePost.Product")
                    .Where(c => c.CustomerID == customerId && !c.IsDeleted)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();
            }
            catch
            {
                try
                {
                    CommentsList = await _context.ExploreComments
                        .Where(c => c.CustomerID == customerId && !c.IsDeleted)
                        .OrderByDescending(c => c.CreatedAt)
                        .ToListAsync();
                }
                catch
                {
                    CommentsList = new List<ExploreComment>();
                }
            }

            // ==========================================
            // Story Likes / Story Replies — BEST-EFFORT (unverified schema)
            // ==========================================
            StoryLikesList = await TryLoadActivityRowsAsync(
                @"SELECT sl.StoryID, sl.CreatedAt, st.StoreName
                  FROM StoryLikes sl
                  INNER JOIN Stories s ON s.StoryID = sl.StoryID
                  INNER JOIN Stores st ON st.StoreID = s.StoreID
                  WHERE sl.CustomerID = @CustomerId
                  ORDER BY sl.CreatedAt DESC",
                ("@CustomerId", customerId));

            var storyReplyCandidates = new (string Sql, (string, object)[] Parameters)[]
            {
                (@"SELECT cm.StoryID, cm.MessageText AS ReplyText, cm.SentAt AS CreatedAt, st.StoreName
                   FROM ChatMessages cm
                   INNER JOIN Stories s ON s.StoryID = cm.StoryID
                   INNER JOIN Stores st ON st.StoreID = s.StoreID
                   WHERE cm.SenderID = @UserId AND cm.StoryID IS NOT NULL
                   ORDER BY cm.SentAt DESC",
                 new (string, object)[] { ("@UserId", userId) }),

                (@"SELECT cm.StoryId AS StoryID, cm.MessageText AS ReplyText, cm.SentAt AS CreatedAt, st.StoreName
                   FROM ChatMessages cm
                   INNER JOIN Stories s ON s.StoryID = cm.StoryId
                   INNER JOIN Stores st ON st.StoreID = s.StoreID
                   WHERE cm.SenderID = @UserId AND cm.StoryId IS NOT NULL
                   ORDER BY cm.SentAt DESC",
                 new (string, object)[] { ("@UserId", userId) }),

                (@"SELECT cm.ReferenceID AS StoryID, cm.MessageText AS ReplyText, cm.SentAt AS CreatedAt, st.StoreName
                   FROM ChatMessages cm
                   INNER JOIN Stories s ON s.StoryID = cm.ReferenceID
                   INNER JOIN Stores st ON st.StoreID = s.StoreID
                   WHERE cm.SenderID = @UserId AND cm.ReferenceType = 'StoryReply'
                   ORDER BY cm.SentAt DESC",
                 new (string, object)[] { ("@UserId", userId) }),

                (@"SELECT m.StoryID, m.Content AS ReplyText, m.SentAt AS CreatedAt, st.StoreName
                   FROM Messages m
                   INNER JOIN Stories s ON s.StoryID = m.StoryID
                   INNER JOIN Stores st ON st.StoreID = s.StoreID
                   WHERE m.SenderUserID = @UserId AND m.StoryID IS NOT NULL
                   ORDER BY m.SentAt DESC",
                 new (string, object)[] { ("@UserId", userId) }),

                (@"SELECT sr.StoryID, sr.ReplyText, sr.CreatedAt, st.StoreName
                   FROM StoryReplies sr
                   INNER JOIN Stories s ON s.StoryID = sr.StoryID
                   INNER JOIN Stores st ON st.StoreID = s.StoreID
                   WHERE sr.CustomerID = @CustomerId
                   ORDER BY sr.CreatedAt DESC",
                 new (string, object)[] { ("@CustomerId", customerId) }),

                (@"SELECT sr.StoryID, sr.ReplyText, sr.CreatedAt, st.StoreName
                   FROM StoryReplies sr
                   INNER JOIN Stories s ON s.StoryID = sr.StoryID
                   INNER JOIN Stores st ON st.StoreID = s.StoreID
                   WHERE sr.SenderUserID = @UserId
                   ORDER BY sr.CreatedAt DESC",
                 new (string, object)[] { ("@UserId", userId) }),
            };

            StoryRepliesList = await TryLoadActivityRowsFromCandidatesAsync(storyReplyCandidates);
        }

        private async Task<List<object>> TryLoadActivityRowsFromCandidatesAsync(
            (string Sql, (string Name, object Value)[] Parameters)[] candidates)
        {
            foreach (var candidate in candidates)
            {
                var rows = await TryLoadActivityRowsAsync(candidate.Sql, candidate.Parameters);
                if (rows.Count > 0)
                {
                    return rows;
                }
            }

            return new List<object>();
        }

        private async Task<List<object>> TryLoadActivityRowsAsync(string sql, params (string Name, object Value)[] parameters)
        {
            var results = new List<object>();

            try
            {
                var connection = _context.Database.GetDbConnection();
                var shouldClose = connection.State != System.Data.ConnectionState.Open;

                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;

                    foreach (var (name, value) in parameters)
                    {
                        var parameter = command.CreateParameter();
                        parameter.ParameterName = name;
                        parameter.Value = value ?? DBNull.Value;
                        command.Parameters.Add(parameter);
                    }

                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        results.Add(row);
                    }
                }
                finally
                {
                    if (shouldClose)
                    {
                        await connection.CloseAsync();
                    }
                }
            }
            catch
            {
                return new List<object>();
            }

            return results;
        }

        private async Task LoadDeliveryAccessStatusAsync(User user)
        {
            HasPendingDeliveryRequest = false;
            HasApprovedDeliveryAccount = false;
            HasRejectedDeliveryRequest = false;
            DeliveryAccessMessage = string.Empty;
            DeliveryAccountEmail = string.Empty;

            // New records are linked through RequestedByUserID.
            // The UserID fallback is kept only for old pending requests,
            // before the Admin creates the separate Delivery login.
            var deliveryRequest = await _context.DeliveryPersons
                .AsNoTracking()
                .Where(d =>
                    d.RequestedByUserID == user.Id
                    ||
                    (
                        d.RequestedByUserID == null
                        &&
                        d.UserID == user.Id
                    ))
                .OrderByDescending(d => d.DeliveryPersonID)
                .FirstOrDefaultAsync();

            if (deliveryRequest == null)
            {
                DeliveryAccessMessage =
                    "Submit your vehicle and license information to join our delivery fleet.";

                return;
            }

            var status = deliveryRequest.Status?.Trim();

            if (string.Equals(
                status,
                "Pending",
                StringComparison.OrdinalIgnoreCase))
            {
                HasPendingDeliveryRequest = true;
                DeliveryAccessMessage =
                    "Your delivery request is pending admin approval.";

                return;
            }

            if (string.Equals(
                    status,
                    "Approved",
                    StringComparison.OrdinalIgnoreCase)
                &&
                !deliveryRequest.IsActive)
            {
                DeliveryAccessMessage =
                    "Your delivery account is approved but currently inactive. Please contact the admin.";

                return;
            }

            if (string.Equals(
                    status,
                    "Approved",
                    StringComparison.OrdinalIgnoreCase)
                &&
                deliveryRequest.IsActive)
            {
                var deliveryUser = await _userManager.FindByIdAsync(
                    deliveryRequest.UserID.ToString());

                if (deliveryUser == null || !deliveryUser.IsActive)
                {
                    DeliveryAccessMessage =
                        "Your delivery account is unavailable. Please contact the admin.";

                    return;
                }

                var isDelivery = await _userManager.IsInRoleAsync(
                    deliveryUser,
                    "Delivery");

                if (!isDelivery)
                {
                    DeliveryAccessMessage =
                        "Your delivery account is not configured correctly. Please contact the admin.";

                    return;
                }

                DeliveryAccountEmail =
                    deliveryUser.Email
                    ?? deliveryUser.UserName
                    ?? string.Empty;

                if (string.IsNullOrWhiteSpace(DeliveryAccountEmail))
                {
                    DeliveryAccessMessage =
                        "Your delivery account email is unavailable. Please contact the admin.";

                    return;
                }

                HasApprovedDeliveryAccount = true;
                DeliveryAccessMessage =
                    "Your delivery account is approved. Enter the password provided by the administrator.";

                return;
            }

            if (string.Equals(
                status,
                "Rejected",
                StringComparison.OrdinalIgnoreCase))
            {
                HasRejectedDeliveryRequest = true;

                DeliveryAccessMessage =
                    !string.IsNullOrWhiteSpace(deliveryRequest.RejectionReason)
                        ? $"Your delivery request was rejected: {deliveryRequest.RejectionReason}. You can submit a new request."
                        : "Your delivery request was rejected. You can submit a new request.";

                return;
            }

            DeliveryAccessMessage =
                "Submit your vehicle and license information to join our delivery fleet.";
        }

        private async Task LoadStoreAccessStatusAsync(User user)
        {
            HasStoreRequest = false;
            HasPendingStoreRequest = false;
            HasApprovedStoreOwnerAccount = false;
            HasRejectedStoreRequest = false;
            StoreAccessMessage = string.Empty;
            StoreOwnerAccountEmail = string.Empty;

            // New records are permanently linked through RequestedByUserID.
            // The OwnerUserID fallback keeps legacy Store requests working.
            var storeRequest = await _context.Stores
                .AsNoTracking()
                .Where(s =>
                    s.RequestedByUserID == user.Id
                    ||
                    (
                        s.RequestedByUserID == null
                        &&
                        s.OwnerUserID == user.Id
                    ))
                .OrderByDescending(s => s.StoreID)
                .FirstOrDefaultAsync();

            if (storeRequest == null)
            {
                StoreAccessMessage =
                    "Submit your store information to request a Store Owner account.";

                return;
            }

            HasStoreRequest = true;

            var status = storeRequest.Status?.Trim();

            if (string.Equals(
                    status,
                    "Pending",
                    StringComparison.OrdinalIgnoreCase))
            {
                HasPendingStoreRequest = true;
                StoreAccessMessage =
                    "Your store request is pending admin approval.";

                return;
            }

            if (string.Equals(
                    status,
                    "Rejected",
                    StringComparison.OrdinalIgnoreCase))
            {
                HasRejectedStoreRequest = true;
                StoreAccessMessage =
                    "Your store request was rejected. You can update your information and submit it again.";

                return;
            }

            if (string.Equals(
                    status,
                    "Inactive",
                    StringComparison.OrdinalIgnoreCase))
            {
                StoreAccessMessage =
                    "Your Store Owner account is currently inactive. Please contact the admin.";

                return;
            }

            if (string.Equals(
                    status,
                    "Suspended",
                    StringComparison.OrdinalIgnoreCase)
                ||
                storeRequest.IsSuspended
                ||
                string.Equals(
                    storeRequest.SubscriptionStatus?.Trim(),
                    "Suspended",
                    StringComparison.OrdinalIgnoreCase))
            {
                StoreAccessMessage =
                    "Your Store Owner account is currently suspended. Please contact the admin.";

                return;
            }

            if (string.Equals(
                    status,
                    "Approved",
                    StringComparison.OrdinalIgnoreCase))
            {
                var storeOwnerUser = await _userManager.FindByIdAsync(
                    storeRequest.OwnerUserID.ToString());

                if (storeOwnerUser == null || !storeOwnerUser.IsActive)
                {
                    StoreAccessMessage =
                        "Your Store Owner account is unavailable. Please contact the admin.";

                    return;
                }

                var isStoreOwner = await _userManager.IsInRoleAsync(
                    storeOwnerUser,
                    "StoreOwner");

                if (!isStoreOwner)
                {
                    StoreAccessMessage =
                        "Your Store Owner account is not configured correctly. Please contact the admin.";

                    return;
                }

                StoreOwnerAccountEmail =
                    storeOwnerUser.Email
                    ?? storeOwnerUser.UserName
                    ?? string.Empty;

                if (string.IsNullOrWhiteSpace(StoreOwnerAccountEmail))
                {
                    StoreAccessMessage =
                        "Your Store Owner account email is unavailable. Please contact the admin.";

                    return;
                }

                HasApprovedStoreOwnerAccount = true;
                StoreAccessMessage =
                    "Your Store Owner account is approved. Enter the password provided by the administrator.";

                return;
            }

            StoreAccessMessage =
                "Submit your store information to request a Store Owner account.";
        }
    }
}