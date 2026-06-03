using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class Customer1Model : PageModel
    {
        private readonly ApplicationDbContext _context;

        public Customer1Model(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Product> Products { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Load only active products with positive stock
            Products = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Store)
                .Where(p => p.IsActive && p.Quantity > 0)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
    }
}