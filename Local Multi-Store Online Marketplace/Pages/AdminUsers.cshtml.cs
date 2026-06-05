using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]

    public class AdminUsersModel : PageModel
    {
        private readonly UserManager<User> _userManager;

        public AdminUsersModel(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        public List<AdminUserViewModel> Users { get; set; } = new();

        public async Task OnGetAsync()
        {
            var users = await _userManager.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            Users = new List<AdminUserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                Users.Add(new AdminUserViewModel
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email ?? "",
                    PhoneNumber = user.PhoneNumber ?? "",
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    Roles = string.Join(", ", roles)
                });
            }
        }

        public async Task<IActionResult> OnPostApproveAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToPage();
            }

            user.IsActive = true;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                TempData["Error"] = "Could not approve user.";
                return RedirectToPage();
            }

            TempData["Success"] = "User approved successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeactivateAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToPage();
            }

            user.IsActive = false;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                TempData["Error"] = "Could not deactivate user.";
                return RedirectToPage();
            }

            TempData["Success"] = "User deactivated successfully.";

            return RedirectToPage();
        }
    }

    public class AdminUserViewModel
    {
        public int UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public string Roles { get; set; } = string.Empty;
    }
}