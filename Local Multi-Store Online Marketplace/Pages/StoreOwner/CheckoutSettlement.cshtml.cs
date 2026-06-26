using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner
{
    public class CheckoutSettlementModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public decimal Amount { get; set; }

        public string Direction { get; set; }

        public void OnGet()
        {
            Direction = Amount >= 0
                ? "Platform owes you"
                : "You owe platform";

            Amount = Math.Abs(Amount);
        }

        public IActionResult OnPost()
        {
            // TODO:
            // 1. Process payment (Stripe / card / manual)
            // 2. Record settlement transaction
            // 3. Update balances

            return RedirectToPage("/StoreOwner/AccountStatement");
        }
    }
}