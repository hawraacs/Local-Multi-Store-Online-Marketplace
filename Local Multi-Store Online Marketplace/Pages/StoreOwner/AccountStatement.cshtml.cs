using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using Multi_Store.Infrastructure.Data;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Alias to avoid conflict with your SubscriptionService
using MySubscriptionService = Multi_Store.Services.SubscriptionService;
using StripeCustomer = Stripe.Customer;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner
{
    [Authorize(Roles = "StoreOwner")]
    public class AccountStatementModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly UserManager<User> _userManager;
        private readonly MySubscriptionService _subscriptionService;  // alias used
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountStatementModel> _logger;   // ✅ added

        public AccountStatementModel(
            ApplicationDbContext context,
            ICurrentStoreService currentStoreService,
            UserManager<User> userManager,
            MySubscriptionService subscriptionService,
            IConfiguration configuration,
            ILogger<AccountStatementModel> logger)   // ✅ injected
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _userManager = userManager;
            _subscriptionService = subscriptionService;
            _configuration = configuration;
            _logger = logger;   // ✅ assigned
        }

        public Store Store { get; set; } = null!;
        public StatementSummary Summary { get; set; } = new();
        public List<StatementLine> Lines { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var store = await GetStoreAsync();
            if (store == null)
                return RedirectToPage("/StoreOwner/Dashboard");

            Store = store;
            await BuildStatementAsync(store);
            return Page();
        }

        // =============================================================
        // Renew Subscription – try auto-charge with saved card, else manual
        // =============================================================
        public async Task<IActionResult> OnPostRenewAsync()
        {
            var store = await GetStoreAsync();
            if (store == null)
                return RedirectToPage("/StoreOwner/Dashboard");

            // If we have saved card, attempt auto-charge
            if (!string.IsNullOrEmpty(store.StripeCustomerId) && !string.IsNullOrEmpty(store.StripePaymentMethodId))
            {
                var success = await ChargeSavedCardAsync(store, 20.00m);
                if (success)
                {
                    TempData["Success"] = "Subscription renewed automatically using your saved card.";
                    return RedirectToPage();
                }
                // fall through to manual payment
            }

            // Manual: create pending payment and redirect to payment page
            var pendingPayment = await GetOrCreatePendingSubscriptionPaymentAsync(store.StoreID);
            if (pendingPayment == null)
            {
                TempData["ErrorMessage"] = "Unable to create payment request. Please try again.";
                return RedirectToPage();
            }

            return RedirectToPage("/StoreOwner/StoreOwnerPayment", new
            {
                paymentId = pendingPayment.StorePaymentId,
                returnUrl = Url.Page("/StoreOwner/AccountStatement")
            });
        }

        // =============================================================
        // Helpers
        // =============================================================

        private async Task<bool> ChargeSavedCardAsync(Store store, decimal amount)
        {
            try
            {
                StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(amount * 100),
                    Currency = "usd",
                    Customer = store.StripeCustomerId,
                    PaymentMethod = store.StripePaymentMethodId,
                    OffSession = true,
                    Confirm = true,
                };
                var service = new PaymentIntentService();
                var intent = await service.CreateAsync(options);

                if (intent.Status == "succeeded")
                {
                    // Record payment and extend subscription
                    await using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var transferId = intent.Id;

                        DateTime newExpiry = DateTime.UtcNow.AddMonths(1);
                        if (store.SubscriptionExpiryDate.HasValue && store.SubscriptionExpiryDate.Value > DateTime.UtcNow)
                            newExpiry = store.SubscriptionExpiryDate.Value.AddMonths(1);

                        store.SubscriptionStatus = "Active";
                        store.SubscriptionExpiryDate = newExpiry;
                        store.LastPaymentDate = DateTime.UtcNow;
                        store.LastPaymentAmount = amount;

                        var subscriptionPayment = new SubscriptionPayment
                        {
                            StoreId = store.StoreID,
                            Amount = amount,
                            PaymentDate = DateTime.UtcNow,
                            Reference = $"Auto-renewal - {transferId}"
                        };
                        _context.SubscriptionPayments.Add(subscriptionPayment);

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        return true;
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
                return false;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Auto-renewal failed for store {StoreId}.", store.StoreID);
                return false;
            }
        }

        private async Task<Store?> GetStoreAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            var store = await _currentStoreService.GetCurrentStoreAsync();
            if (store == null)
            {
                store = await _context.Stores
                    .FirstOrDefaultAsync(s => s.OwnerUserID == user.Id && s.Status == "Approved");
            }
            return store;
        }

        private async Task BuildStatementAsync(Store store)
        {
            var deliveredOrderItems = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.StoreID == store.StoreID && oi.Order.Status == "Delivered")
                .OrderBy(oi => oi.Order.OrderDate)
                .ToListAsync();

            var orderGroups = deliveredOrderItems
                .GroupBy(oi => oi.OrderID)
                .Select(g => new
                {
                    OrderId = g.Key,
                    OrderNumber = g.First().Order.OrderNumber,
                    OrderDate = g.First().Order.OrderDate,
                    GrossAmount = g.Sum(oi => oi.TotalPrice),
                    Commission = g.Sum(oi => oi.TotalPrice * 0.05m)
                })
                .ToList();

            var subscriptionPayments = await _context.SubscriptionPayments
                .Where(sp => sp.StoreId == store.StoreID)
                .OrderBy(sp => sp.PaymentDate)
                .ToListAsync();

            var lines = new List<StatementLine>();

            foreach (var o in orderGroups)
            {
                lines.Add(new StatementLine
                {
                    Date = o.OrderDate,
                    Description = $"Order #{o.OrderNumber}",
                    GrossRevenue = o.GrossAmount,
                    Commission = o.Commission,
                    SubscriptionFee = 0,
                    Type = LineType.Order
                });
            }

            foreach (var p in subscriptionPayments)
            {
                lines.Add(new StatementLine
                {
                    Date = p.PaymentDate,
                    Description = p.Reference ?? "Subscription payment",
                    GrossRevenue = 0,
                    Commission = 0,
                    SubscriptionFee = p.Amount,
                    Type = LineType.Subscription
                });
            }

            Lines = lines.OrderByDescending(l => l.Date).ToList();

            Summary.TotalGrossRevenue = orderGroups.Sum(o => o.GrossAmount);
            Summary.TotalCommission = orderGroups.Sum(o => o.Commission);
            Summary.TotalSubscriptionFees = subscriptionPayments.Sum(p => p.Amount);
            Summary.NetRevenue = Summary.TotalGrossRevenue - Summary.TotalCommission - Summary.TotalSubscriptionFees;
            Summary.OutstandingBalance = store.OutstandingBalance;
        }

        private async Task<StorePayment?> GetOrCreatePendingSubscriptionPaymentAsync(int storeId)
        {
            decimal monthlyFee = _configuration.GetValue<decimal>("StoreSettings:MonthlySubscriptionFee", 20.00m);
            const string description = "Monthly Subscription Fee";

            var existing = await _context.StorePayments
                .FirstOrDefaultAsync(sp => sp.StoreId == storeId
                                           && sp.Description == description
                                           && sp.Status == "Pending");
            if (existing != null)
                return existing;

            var payment = new StorePayment
            {
                StoreId = storeId,
                Amount = monthlyFee,
                Description = description,
                DueDate = DateTime.UtcNow.AddDays(7),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.StorePayments.Add(payment);
            await _context.SaveChangesAsync();

            return payment;
        }
    }

    public class StatementSummary
    {
        public decimal TotalGrossRevenue { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal TotalSubscriptionFees { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal OutstandingBalance { get; set; }
    }

    public class StatementLine
    {
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal GrossRevenue { get; set; }
        public decimal Commission { get; set; }
        public decimal SubscriptionFee { get; set; }
        public LineType Type { get; set; }
        public decimal NetEffect => GrossRevenue - Commission - SubscriptionFee;
    }

    public enum LineType
    {
        Order,
        Subscription
    }
}