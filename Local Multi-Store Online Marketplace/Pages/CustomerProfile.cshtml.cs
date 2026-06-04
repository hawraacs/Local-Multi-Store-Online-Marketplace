using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CustomerProfileModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public CustomerProfileModel(
            UserManager<User> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // =========================
        // CUSTOMER PROFILE
        // =========================
        [BindProperty]
        public string CustomerFullName { get; set; } = string.Empty;

        [BindProperty]
        public string CustomerEmail { get; set; } = string.Empty;

        [BindProperty]
        public string CustomerPhone { get; set; } = string.Empty;

        public int OrdersCount { get; set; }

        public int WishlistCount { get; set; }

        public int AddressesCount { get; set; }

        // =========================
        // STORE REQUEST
        // =========================
        [BindProperty]
        public StoreDTO Store { get; set; } = new StoreDTO();

        // =========================
        // DELIVERY REQUEST
        // =========================
        [BindProperty]
        public DeliveryPersonDTO Delivery { get; set; } = new DeliveryPersonDTO();

        // =========================
        // GET
        // =========================
        public async Task<IActionResult> OnGetAsync()
        {
            var loaded = await LoadCustomerProfileAsync();

            if (!loaded)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            return Page();
        }

        // =========================
        // UPDATE PROFILE
        // =========================
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            user.FullName = CustomerFullName;
            user.PhoneNumber = CustomerPhone;

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

        // =========================
        // STORE REQUEST
        // =========================
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

        // =========================
        // DELIVERY REQUEST
        // =========================
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

        // =========================
        // LOAD CURRENT CUSTOMER DATA
        // =========================
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
            CustomerPhone = user.PhoneNumber ?? "No phone number";

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
            {
                OrdersCount = 0;
                WishlistCount = 0;
                AddressesCount = 0;
                return true;
            }

            OrdersCount = await _context.Orders
                .CountAsync(o => o.CustomerID == customer.CustomerID);

            WishlistCount = await _context.Wishlists
                .CountAsync(w => w.CustomerID == customer.CustomerID);

            AddressesCount = await _context.CustomerAddresses
                .CountAsync(a => a.CustomerID == customer.CustomerID);

            return true;
        }
    }
}