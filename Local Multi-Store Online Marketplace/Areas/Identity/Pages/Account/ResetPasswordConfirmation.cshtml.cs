#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Local_Multi_Store_Online_Marketplace.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResetPasswordConfirmationModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}