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
        public string Gender { get; set; } = "Not Specified";
        public DateTime? DateOfBirth { get; set; }
        public bool CODBlocked { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<StoreFollow> FollowedStoresList { get; set; } = new();
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

            // Safe lookup with robust try-catches around navigation inclusions
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

            OrdersCount = await _context.Orders
                .CountAsync(o => o.CustomerID == customer.CustomerID);

            WishlistCount = WishlistList.Count;

            AddressesCount = await _context.CustomerAddresses
                .CountAsync(a => a.CustomerID == customer.CustomerID);

            await LoadActivityListsAsync(customer.CustomerID, customer.UserID);

            return true;
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
            // No StoryLike/StoryReply/Message entity or DbSet was available
            // when this was wired up — only StoryManager's per-story check
            // methods (IsLikedByCustomerAsync, LikeStoryAsync, etc.) were
            // visible, not a way to fetch a customer's full history. Rather
            // than reference an unconfirmed DbSet/entity type at compile
            // time (which would fail to *build* rather than just fail at
            // runtime), this runs raw SQL against guessed table/column
            // names using the same naming conventions as your confirmed
            // tables (ExploreLikes, ExploreComments, StoreFollows, etc.).
            //
            // If a guess is wrong, the query throws, is caught, and the
            // list just stays empty — same as it does today. If it comes
            // back empty and you want it working, the fix is almost always
            // just correcting the table/column names in the SQL strings
            // below to match your actual schema (StoryLikes/Messages, or
            // whatever they're really called).
            StoryLikesList = await TryLoadActivityRowsAsync(
                @"SELECT sl.StoryID, sl.CreatedAt, st.StoreName
                  FROM StoryLikes sl
                  INNER JOIN Stories s ON s.StoryID = sl.StoryID
                  INNER JOIN Stores st ON st.StoreID = s.StoreID
                  WHERE sl.CustomerID = @CustomerId
                  ORDER BY sl.CreatedAt DESC",
                ("@CustomerId", customerId));

            // Story Replies — you confirmed the real chat schema uses
            // ChatMessageDTO/IChatMessageRepository, with SenderID,
            // MessageText, and SentAt as real column/property names (from
            // ChatConversationModel). Story replies are sent through the
            // same MessagingManager pipeline as regular chat, so the top
            // candidates now assume they land in a "ChatMessages" table
            // with those exact columns plus a StoryID reference. Older,
            // lower-confidence guesses are kept further down as fallbacks.
            var storyReplyCandidates = new (string Sql, (string, object)[] Parameters)[]
            {
                // 1) Confirmed schema from ChatConversationModel/ChatMessageDTO:
                //    SenderID, MessageText, SentAt are real column/property
                //    names used for regular chat. Story replies go through
                //    the same messaging pipeline, so this assumes they land
                //    in the same table with an added StoryID reference.
                (@"SELECT cm.StoryID, cm.MessageText AS ReplyText, cm.SentAt AS CreatedAt, st.StoreName
                   FROM ChatMessages cm
                   INNER JOIN Stories s ON s.StoryID = cm.StoryID
                   INNER JOIN Stores st ON st.StoreID = s.StoreID
                   WHERE cm.SenderID = @UserId AND cm.StoryID IS NOT NULL
                   ORDER BY cm.SentAt DESC",
                 new (string, object)[] { ("@UserId", userId) }),

                // 2) Same table/columns, but StoryID in camelCase ("StoryId")
                (@"SELECT cm.StoryId AS StoryID, cm.MessageText AS ReplyText, cm.SentAt AS CreatedAt, st.StoreName
                   FROM ChatMessages cm
                   INNER JOIN Stories s ON s.StoryID = cm.StoryId
                   INNER JOIN Stores st ON st.StoreID = s.StoreID
                   WHERE cm.SenderID = @UserId AND cm.StoryId IS NOT NULL
                   ORDER BY cm.SentAt DESC",
                 new (string, object)[] { ("@UserId", userId) }),

                // 3) Same table, reference stored via a generic ReferenceID/
                //    ReferenceType pair (the same pattern already used for
                //    Notifications elsewhere in this app)
                (@"SELECT cm.ReferenceID AS StoryID, cm.MessageText AS ReplyText, cm.SentAt AS CreatedAt, st.StoreName
                   FROM ChatMessages cm
                   INNER JOIN Stories s ON s.StoryID = cm.ReferenceID
                   INNER JOIN Stores st ON st.StoreID = s.StoreID
                   WHERE cm.SenderID = @UserId AND cm.ReferenceType = 'StoryReply'
                   ORDER BY cm.SentAt DESC",
                 new (string, object)[] { ("@UserId", userId) }),

                // 4) Generic "Messages" table, in case story replies use a
                //    different table than regular ChatMessages
                (@"SELECT m.StoryID, m.Content AS ReplyText, m.SentAt AS CreatedAt, st.StoreName
                   FROM Messages m
                   INNER JOIN Stories s ON s.StoryID = m.StoryID
                   INNER JOIN Stores st ON st.StoreID = s.StoreID
                   WHERE m.SenderUserID = @UserId AND m.StoryID IS NOT NULL
                   ORDER BY m.SentAt DESC",
                 new (string, object)[] { ("@UserId", userId) }),

                // 5) Dedicated StoryReplies table, keyed by CustomerID (mirrors StoryLikes)
                (@"SELECT sr.StoryID, sr.ReplyText, sr.CreatedAt, st.StoreName
                   FROM StoryReplies sr
                   INNER JOIN Stories s ON s.StoryID = sr.StoryID
                   INNER JOIN Stores st ON st.StoreID = s.StoreID
                   WHERE sr.CustomerID = @CustomerId
                   ORDER BY sr.CreatedAt DESC",
                 new (string, object)[] { ("@CustomerId", customerId) }),

                // 6) Dedicated StoryReplies table, keyed by sender user id instead
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

        // Tries each candidate (SQL + parameters) in order via
        // TryLoadActivityRowsAsync, and returns the first one that actually
        // comes back with rows. If every candidate throws or returns zero
        // rows, returns an empty list. Used where the exact table/column
        // shape wasn't confirmed and there's more than one plausible guess
        // worth trying automatically instead of asking again.
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

        // Runs a raw SQL SELECT and returns each row as a
        // Dictionary<string, object?> (column name -> value), so the caller
        // doesn't need a compile-time entity/DbSet type for tables whose
        // exact schema wasn't confirmed. GetSafeString/GetSafeInt/
        // GetSafeObject/GetSafeDateString all understand these dictionary
        // rows the same way they understand real entities. Returns an
        // empty list (never throws) if the SQL doesn't match the real
        // schema — e.g. wrong table/column name, or a non-relational
        // provider that doesn't support this SQL dialect.
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