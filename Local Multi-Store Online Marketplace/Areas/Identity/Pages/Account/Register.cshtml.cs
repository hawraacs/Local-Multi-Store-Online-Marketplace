using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using System.ComponentModel.DataAnnotations;

namespace Local_Multi_Store_Online_Marketplace.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public RegisterModel(
            UserManager<User> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        [Required]
        public string FullName { get; set; }

        [BindProperty]
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [BindProperty]
        [Required]
        public string PhoneNumber { get; set; }

        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [BindProperty]
        [Required]
        [Compare("Password")]
        public string ConfirmPassword { get; set; }

        public void OnGet()
        {

        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = new User
            {
                UserName = Email,
                Email = Email,
                PhoneNumber = PhoneNumber,
                FullName = FullName,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return Page();
            }

            await _userManager.AddToRoleAsync(user, "Customer");

            var customer = new Customer
            {
                UserID = user.Id
            };

            _context.Customers.Add(customer);

            await _context.SaveChangesAsync();

            return RedirectToPage("./Login");
        }
    }
}