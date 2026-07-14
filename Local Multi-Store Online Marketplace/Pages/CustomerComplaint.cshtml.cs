using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CustomerComplaintModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CustomerComplaintModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // Dropdown source for the form
        public List<SelectOption> RecentOrders { get; set; } = new();

        public async Task OnGetAsync()
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null) return;

            // Orders don't carry a single StoreID directly (a cart can span multiple
            // stores), so store names are derived from OrderItems -> Product -> Store.
            var orders = await _context.Orders
                .Where(o => o.CustomerID == customerId)
                .OrderByDescending(o => o.OrderDate)
                .Take(20)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p!.Store)
                .ToListAsync();

            RecentOrders = orders.Select(o =>
            {
                var storeNames = o.OrderItems
                    .Select(oi => oi.Product?.Store?.StoreName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .ToList();

                var storeLabel = storeNames.Count switch
                {
                    0 => "Unknown store",
                    1 => storeNames[0],
                    _ => $"{storeNames.Count} stores"
                };

                return new SelectOption
                {
                    Id = o.OrderID,
                    Label = $"Order #{o.OrderNumber} — {storeLabel} ({o.OrderDate:dd MMM yyyy})"
                };
            }).ToList()!;
        }

        public async Task<IActionResult> OnPostAsync(
            string category,
            string description,
            int? orderId)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
            {
                TempData["Error"] = "You must be signed in as a customer to file a complaint.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(description))
            {
                TempData["Error"] = "Please choose a category and describe the issue.";
                return RedirectToPage();
            }

            int? resolvedStoreId = null;

            if (orderId.HasValue)
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.OrderID == orderId.Value && o.CustomerID == customerId);

                if (order == null)
                {
                    TempData["Error"] = "That order could not be found on your account.";
                    return RedirectToPage();
                }

                // If every item in the order came from the same store, attach that store.
                // If the order spanned multiple stores, leave StoreID null — admins can
                // still see the affected order via OrderID and sort it out from there.
                var storeIds = order.OrderItems
                    .Select(oi => oi.Product?.StoreID)
                    .Where(id => id.HasValue)
                    .Distinct()
                    .ToList();

                if (storeIds.Count == 1)
                {
                    resolvedStoreId = storeIds[0];
                }
            }

            _context.Complaints.Add(new Complaint
            {
                CustomerID = (int)customerId,
                StoreID = resolvedStoreId,
                OrderID = orderId,
                ComplaintType = category,
                Description = description.Trim(),
                Status = "Pending Review",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Your complaint has been submitted. Our team will review it shortly.";
            return RedirectToPage();
        }

        private async Task<int?> GetCurrentCustomerIdAsync()
        {
            // Adjust this to however the rest of the app resolves the
            // logged-in user's CustomerID (e.g. via User.Identity + a lookup).
            var userIdString = User.FindFirst("UserID")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdString, out var userId))
                return null;

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == userId);

            return customer?.CustomerID;
        }
    }

    public class SelectOption
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
