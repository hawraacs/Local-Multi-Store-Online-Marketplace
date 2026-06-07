using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminAnalyticsModel : PageModel
    {
        public List<TopStoreDto> TopStores { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Replace with real data from analytics service
            TopStores = new List<TopStoreDto>
            {
                new() { Name = "Fresh Mart", Revenue = 45230, OrderCount = 345, Rating = 4.8 },
                new() { Name = "Tech Zone", Revenue = 32100, OrderCount = 234, Rating = 4.5 },
                new() { Name = "Fashion Hub", Revenue = 28450, OrderCount = 198, Rating = 4.2 }
            };
        }
    }

    public class TopStoreDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public double Rating { get; set; }
    }
}