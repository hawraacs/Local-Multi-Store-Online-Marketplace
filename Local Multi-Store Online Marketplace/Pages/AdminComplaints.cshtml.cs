using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;
using sun.java2d;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminComplaintsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ComplaintManager _complaintManager;
        private readonly PaymentManager _paymentManager;
        private readonly DeliveryManager _deliveryManager;
        private readonly ILogger<AdminComplaintsModel> _logger;

        public AdminComplaintsModel(
            ApplicationDbContext context,
            ComplaintManager complaintManager,
            PaymentManager paymentManager,
            DeliveryManager deliveryManager,
            ILogger<AdminComplaintsModel> logger)
        {
            _context = context;
            _complaintManager = complaintManager;
            _paymentManager = paymentManager;
            _deliveryManager = deliveryManager;
            _logger = logger;
        }

        public List<AdminComplaintViewModel> Complaints { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadComplaintsAsync();
        }

        // =====================================================
        // RESOLVE COMPLAINT (no money movement)
        // Now also notifies the customer, since previously they had
        // no visibility into their complaint being closed at all.
        // =====================================================
        public async Task<IActionResult> OnPostResolveAsync(int complaintId)
        {
            if (complaintId <= 0)
            {
                TempData["Error"] = "Invalid complaint ID.";
                return RedirectToPage();
            }

            try
            {
                var complaint = await _context.Complaints
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ComplaintID == complaintId);

                if (complaint == null)
                {
                    TempData["Error"] = "Complaint not found.";
                    return RedirectToPage();
                }

                if (string.Equals(complaint.Status, "Resolved", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Info"] = $"Complaint #{complaintId} is already resolved.";
                    return RedirectToPage();
                }

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var userAgent = Request.Headers.UserAgent.ToString();
                if (string.IsNullOrWhiteSpace(userAgent)) userAgent = "Unknown";

                await _complaintManager.ResolveComplaintAsync(
                    complaintId,
                    "Complaint reviewed and resolved by the administrator.",
                    "Resolved from the Admin Complaints page.",
                    ipAddress,
                    userAgent);

                await NotifyCustomerComplaintResolvedAsync(
                    complaint.CustomerID,
                    complaintId,
                    "Your complaint has been reviewed and resolved by our team.");

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Complaint #{complaintId} was resolved successfully.";
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error resolving complaint {ComplaintId}.", complaintId);
                TempData["Error"] = "The complaint could not be resolved. Please try again.";
            }

            return RedirectToPage();
        }

        // =====================================================
        // REFUND COMPLAINT
        //
        // - Supports a partial amount (defaults to full payment if
        //   the admin doesn't specify one).
        // - Routes the actual refund through PaymentManager's
        //   RefundRequest workflow so admin refunds share the same
        //   audit trail as customer-initiated ones.
        // - Assigns financial liability based on complaint type:
        //     "Store service" / "Order problem" -> StorePayment chargeback
        //     "Delivery issue"                   -> DeliveryPaymentCollection
        //                                            adjustment (if a COD
        //                                            collection row exists)
        //                                            plus a notification
        //     anything else                       -> platform absorbs it
        // - Notifies the customer that their complaint led to a refund.
        // =====================================================
        public async Task<IActionResult> OnPostRefundAsync(
            int complaintId,
            decimal? refundAmount)
        {
            if (complaintId <= 0)
            {
                TempData["Error"] = "Invalid complaint ID.";
                return RedirectToPage();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var complaint = await _context.Complaints
                    .Include(c => c.Order)
                    .FirstOrDefaultAsync(c => c.ComplaintID == complaintId);

                if (complaint == null)
                {
                    TempData["Error"] = "Complaint not found.";
                    await transaction.RollbackAsync();
                    return RedirectToPage();
                }

                if (!complaint.OrderID.HasValue)
                {
                    TempData["Error"] = "This complaint is not connected to an order, so a refund cannot be issued.";
                    await transaction.RollbackAsync();
                    return RedirectToPage();
                }

                var payment = await _context.Payments
                    .Where(p => p.OrderID == complaint.OrderID.Value)
                    .OrderByDescending(p => p.PaymentID)
                    .FirstOrDefaultAsync();

                if (payment == null)
                {
                    TempData["Error"] = "No payment record was found for this complaint's order.";
                    await transaction.RollbackAsync();
                    return RedirectToPage();
                }

                // Payment.RefundAmount accumulates prior refunds (full or
                // partial), so the true refundable balance is what's left
                // after those — not the original Amount. Status=="Refunded"
                // is kept as a belt-and-suspenders check in case a row is
                // ever marked Refunded without RefundAmount being fully
                // populated.
                var alreadyRefunded = payment.RefundAmount ?? 0m;
                var remainingRefundable = payment.Amount - alreadyRefunded;

                if (string.Equals(payment.Status, "Refunded", StringComparison.OrdinalIgnoreCase)
                    || remainingRefundable <= 0)
                {
                    TempData["Info"] = $"The payment for complaint #{complaintId} has already been fully refunded.";
                    await transaction.RollbackAsync();
                    return RedirectToPage();
                }

                if (payment.Amount <= 0)
                {
                    TempData["Error"] = "The payment amount is invalid and cannot be refunded.";
                    await transaction.RollbackAsync();
                    return RedirectToPage();
                }

                // Default to a full (remaining) refund unless the admin entered a lesser amount.
                var amountToRefund = (refundAmount.HasValue && refundAmount.Value > 0)
                    ? refundAmount.Value
                    : remainingRefundable;

                if (amountToRefund > remainingRefundable)
                {
                    TempData["Error"] = $"The refund amount cannot exceed the remaining refundable balance of ${remainingRefundable:N2}.";
                    await transaction.RollbackAsync();
                    return RedirectToPage();
                }

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var userAgent = Request.Headers.UserAgent.ToString();
                if (string.IsNullOrWhiteSpace(userAgent)) userAgent = "Unknown";

                // Create + immediately approve the refund request through the
                // shared PaymentManager, so this goes through one canonical
                // refund path instead of duplicating the logic here.
                var refundRequestId = await _paymentManager.RequestRefundAsync(
                    orderId: complaint.OrderID.Value,
                    customerId: complaint.CustomerID,
                    reason: $"Admin refund — complaint #{complaintId} ({complaint.ComplaintType})",
                    amount: amountToRefund,
                    description: $"Issued by an administrator while resolving complaint #{complaintId}.");

                await _paymentManager.ApproveRefundAsync(
                    refundRequestId,
                    amountToRefund,
                    $"Approved automatically as part of resolving complaint #{complaintId} from the Admin Complaints page.");

                // ------------------------------------------------------------
                // Liability routing.
                //
                // Complaint category picks the liable party for the three
                // explicit cases below. But for orders paid COD / at
                // pickup, the customer's money went straight to the store
                // (or was collected by the courier) — it never passed
                // through the platform's payment gateway. So if none of
                // the explicit categories apply, the fallback can't be
                // "Platform": the platform has no funds from this order to
                // absorb a refund from. It falls back to "Store" instead
                // whenever the payment was collected directly rather than
                // paid online, since the store is the only party that
                // actually holds that cash.
                // ------------------------------------------------------------
                var wasPaidOnCollection = IsPayOnCollectionMethod(payment.PaymentMethod);

                var faultParty = complaint.ComplaintType?.Trim() switch
                {
                    "Store service" => "Store",
                    "Order problem" => "Store",
                    "Delivery issue" => "Delivery",
                    _ => wasPaidOnCollection ? "Store" : "Platform"
                };

                if (faultParty == "Store" && complaint.StoreID.HasValue)
                {
                    var storeEntity = await _context.Stores
                        .FirstOrDefaultAsync(s => s.StoreID == complaint.StoreID.Value);

                    if (storeEntity != null)
                    {
                        // StorePayment in this app is a bill the STORE owes
                        // the PLATFORM (subscription fees, boost fees) —
                        // the store pays it off themselves via
                        // StoreOwnerPayment.cshtml, and everything in that
                        // flow (CheckStripeBalance, the "insufficient
                        // funds" check, etc.) assumes Amount is positive.
                        // A negative Amount here would be meaningless to
                        // that flow, so this must add to what's owed, not
                        // subtract from it.
                        _context.StorePayments.Add(new StorePayment
                        {
                            StoreId = storeEntity.StoreID,
                            Amount = amountToRefund,
                            Description = $"Refund chargeback for complaint #{complaintId} (Order #{complaint.OrderID}).",
                            DueDate = DateTime.UtcNow,
                            Status = "Pending",
                            CreatedAt = DateTime.UtcNow
                        });

                        // Dashboard.cshtml.cs reads Store.OutstandingBalance
                        // directly (Stats.OutstandingBalance = store.OutstandingBalance),
                        // not a sum of StorePayments — so without this line,
                        // the number the store actually sees on their own
                        // dashboard would never move, even though a
                        // StorePayment row was created behind the scenes.
                        storeEntity.OutstandingBalance += amountToRefund;

                        _context.Notifications.Add(new Notification
                        {
                            UserID = storeEntity.OwnerUserID,
                            Title = "Refund added to your outstanding balance",
                            Message = $"${amountToRefund:N2} has been added to your outstanding balance and refunded to the customer for complaint #{complaintId} (Order #{complaint.OrderID}).",
                            Type = "PaymentUpdate",
                            ReferenceID = complaintId,
                            IsRead = false,
                            SentAt = DateTime.UtcNow,
                            SentVia = "System"
                        });
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Complaint {ComplaintId}: StoreID {StoreId} on the complaint did not resolve to a Store row — chargeback skipped.",
                            complaintId, complaint.StoreID.Value);
                    }
                }
                else if (faultParty == "Store" && !complaint.StoreID.HasValue)
                {
                    // The order spanned multiple stores (or the store could
                    // never be resolved), so there's no single StoreID to
                    // charge back. The customer refund still goes through —
                    // this just falls through instead of silently doing
                    // nothing. Surface it for manual review, same as the
                    // Delivery branch does for its own gap.
                    _logger.LogWarning(
                        "Complaint {ComplaintId}: refund issued but no StoreID was resolvable — chargeback skipped.",
                        complaintId);

                    TempData["Warning"] = wasPaidOnCollection
                        // Paid on collection: no store to bill AND no
                        // platform funds to absorb it from either — this
                        // needs a human to figure out who actually holds
                        // the ${amountToRefund:N2} and recover it.
                        ? $"Refund issued, but this order was paid on collection and no single store could be identified — the ${amountToRefund:N2} is NOT yet recovered from anyone and needs manual follow-up."
                        : $"Refund issued, but no single store could be charged back (order spanned multiple stores) — the platform absorbed the ${amountToRefund:N2}.";
                }
                else if (faultParty == "Delivery")
                {
                    try
                    {
                        var (deliveryPerson, amountAdjusted) =
                            await _deliveryManager.PenalizeDeliveryPersonForOrderAsync(
                                complaint.OrderID.Value,
                                amountToRefund);

                        _context.Notifications.Add(new Notification
                        {
                            UserID = deliveryPerson.UserID,
                            Title = "Refund chargeback applied to your delivery",
                            Message = amountAdjusted
                                ? $"A ${amountToRefund:N2} refund was issued on complaint #{complaintId}. This amount has been deducted from your collected payment for Order #{complaint.OrderID}."
                                : $"A ${amountToRefund:N2} refund was issued on complaint #{complaintId} for Order #{complaint.OrderID}. No cash-collection record existed to adjust automatically — this is recorded for review.",
                            Type = "PaymentUpdate",
                            ReferenceID = complaintId,
                            IsRead = false,
                            SentAt = DateTime.UtcNow,
                            SentVia = "System"
                        });
                    }
                    catch (InvalidOperationException ex)
                    {
                        // The customer refund still goes through — a missing
                        // assignment shouldn't block making the customer
                        // whole. Surface it for manual admin follow-up.
                        _logger.LogWarning(
                            "Complaint {ComplaintId}: refund issued but delivery chargeback failed — {Reason}",
                            complaintId, ex.Message);

                        TempData["Warning"] =
                            $"Refund issued, but the delivery chargeback could not be applied: {ex.Message}";
                    }
                }

                await _complaintManager.ResolveComplaintAsync(
                    complaintId,
                    $"Refund of ${amountToRefund:N2} issued. Liable party: {faultParty}.",
                    "Complaint resolved through a refund issued by an administrator.",
                    ipAddress,
                    userAgent);

                await NotifyCustomerComplaintResolvedAsync(
                    complaint.CustomerID,
                    complaintId,
                    $"Your complaint has been resolved with a refund of ${amountToRefund:N2}.");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = $"A refund of ${amountToRefund:N2} was issued successfully for complaint #{complaintId}.";
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync();
                _logger.LogError(exception, "Error refunding complaint {ComplaintId}.", complaintId);
                TempData["Error"] = "The refund could not be completed. No changes were saved.";
            }

            return RedirectToPage();
        }

        // =====================================================
        // PAY-ON-COLLECTION DETECTION
        //
        // Orders paid this way never route money through the platform's
        // payment gateway — the customer hands cash to the store (pickup)
        // or the courier (COD delivery) directly. That matters for refund
        // liability: if a refund's fault party would otherwise default to
        // "Platform", the platform actually has nothing to absorb it
        // from here, so liability falls back to the store instead.
        //
        // NOTE: matched values are a best guess based on common naming
        // ("COD", "Cash on Delivery", "Cash on Pickup", "Pay on Delivery",
        // "Pay at Pickup", "Cash"). If PaymentMethod uses different
        // literal strings in this app, update the list below to match.
        // =====================================================
        private static readonly string[] PayOnCollectionMethods =
        {
            "COD",
            "Cash on Delivery",
            "Cash on Pickup",
            "Pay on Delivery",
            "Pay at Pickup",
            "Cash"
        };

        private static bool IsPayOnCollectionMethod(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
                return false;

            return PayOnCollectionMethods.Any(m =>
                string.Equals(m, paymentMethod.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // customer's Report Updates page (Type = "ComplaintUpdate",
        // now included in ReportUpdatesModel.VisibleNotificationTypes).
        // Looks up the customer's UserID via Customer.CustomerID, since
        // Complaint only stores CustomerID, not UserID directly.
        // =====================================================
        private async Task NotifyCustomerComplaintResolvedAsync(
            int customerId,
            int complaintId,
            string message)
        {
            var customerUserId = await _context.Customers
                .Where(cu => cu.CustomerID == customerId)
                .Select(cu => (int?)cu.UserID)
                .FirstOrDefaultAsync();

            if (customerUserId == null)
            {
                _logger.LogWarning(
                    "Complaint {ComplaintId}: could not resolve a UserID for CustomerID {CustomerId}; customer notification skipped.",
                    complaintId, customerId);
                return;
            }

            _context.Notifications.Add(new Notification
            {
                UserID = customerUserId.Value,
                Title = "Your complaint has been resolved",
                Message = message,
                Type = "ComplaintUpdate",
                ReferenceID = complaintId,
                IsRead = false,
                SentAt = DateTime.UtcNow,
                SentVia = "System"
            });
        }

        // =====================================================
        // LOAD COMPLAINTS FROM DATABASE
        //
        // Resolves the real customer name (Complaint.CustomerID ->
        // Customer.UserID -> User.FullName) via a correlated subquery
        // instead of the previous "Customer #{id}" placeholder.
        // Also pulls the linked payment amount, so the refund modal
        // can default to (and cap at) the correct figure.
        // =====================================================
        private async Task LoadComplaintsAsync()
        {
            var payments = await _context.Payments
                .Select(p => new { p.PaymentID, p.OrderID, p.Amount, p.Status, p.RefundAmount })
                .ToListAsync();

            var paymentsByOrder = payments
                .GroupBy(p => p.OrderID)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PaymentID).First());

            Complaints = await _context.Complaints
                .AsNoTracking()
                .Include(c => c.Store)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new AdminComplaintViewModel
                {
                    ComplaintId = c.ComplaintID,
                    CustomerName = _context.Customers
                        .Where(cu => cu.CustomerID == c.CustomerID)
                        .Join(_context.Users,
                            cu => cu.UserID,
                            u => u.Id,
                            (cu, u) => u.FullName)
                        .FirstOrDefault() ?? ("Customer #" + c.CustomerID),
                    StoreName = c.Store != null
                        ? c.Store.StoreName
                        : c.StoreID.HasValue
                            ? "Store #" + c.StoreID.Value
                            : "General Complaint",
                    Type = c.ComplaintType,
                    Description = c.Description,
                    Status = string.IsNullOrWhiteSpace(c.Status) ? "Pending" : c.Status,
                    CreatedAt = c.CreatedAt,
                    OrderId = c.OrderID,
                    Resolution = c.Resolution,
                    ResolvedAt = c.ResolvedAt
                })
                .ToListAsync();

            foreach (var complaint in Complaints)
            {
                if (complaint.OrderId.HasValue &&
                    paymentsByOrder.TryGetValue(complaint.OrderId.Value, out var payment))
                {
                    complaint.PaymentAmount = payment.Amount;
                    complaint.PaymentStatus = payment.Status;
                    complaint.PaymentRemainingRefundable = payment.Amount - (payment.RefundAmount ?? 0m);
                }
            }
        }
    }

    public class AdminComplaintViewModel
    {
        public int ComplaintId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int? OrderId { get; set; }
        public string? Resolution { get; set; }
        public DateTime? ResolvedAt { get; set; }

        // Populated from the linked Payment, when one exists.
        public decimal? PaymentAmount { get; set; }
        public string? PaymentStatus { get; set; }

        // Amount - any prior RefundAmount. What a new refund can actually
        // still draw against; null if there's no payment at all.
        public decimal? PaymentRemainingRefundable { get; set; }
    }
}