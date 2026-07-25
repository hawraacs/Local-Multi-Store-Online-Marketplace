using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
using Microsoft.AspNetCore.Authorization;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages.Deliverypages
{
    [Authorize(Roles = "Customer")]
    public class DeliveryRequestModel : PageModel
    {
        private readonly DeliveryManager _deliveryManager;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<DeliveryRequestModel> _logger;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly NotificationManager _notifications;

        public DeliveryRequestModel(
            DeliveryManager deliveryManager,
            UserManager<User> userManager,
            ILogger<DeliveryRequestModel> logger,
            IAuditLogRepository auditLogRepository,
            NotificationManager notifications)
        {
            _deliveryManager = deliveryManager;
            _userManager = userManager;
            _logger = logger;
            _auditLogRepository = auditLogRepository;
            _notifications = notifications;
        }

        [BindProperty]
        public DeliveryPersonDTO Delivery { get; set; } = new();

        [BindProperty]
        public IFormFile? IDProofFile { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("Delivery request POST started.");

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                _logger.LogWarning("User not logged in.");
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (IDProofFile == null || IDProofFile.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "ID proof is required.");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid.");
                return Page();
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var extension = Path.GetExtension(IDProofFile!.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(string.Empty, "Only JPG, PNG, and PDF files are allowed.");
                return Page();
            }

            var maxFileSize = 5 * 1024 * 1024;

            if (IDProofFile.Length > maxFileSize)
            {
                ModelState.AddModelError(string.Empty, "ID proof file size must be less than 5 MB.");
                return Page();
            }

            var uploadsFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads",
                "delivery-id-proofs");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await IDProofFile.CopyToAsync(stream);
            }

            Delivery.IDProofURL = $"/uploads/delivery-id-proofs/{fileName}";

            // At request time, UserID is temporarily the customer account.
            // RequestedByUserID permanently keeps the original customer account.
            Delivery.UserID = user.Id;
            Delivery.RequestedByUserID = user.Id;

            try
            {
                var id = await _deliveryManager.RegisterDeliveryPersonAsync(Delivery);

                _logger.LogInformation("Delivery created with ID: {Id}", id);

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var userAgent = Request.Headers.UserAgent.ToString();
                if (string.IsNullOrWhiteSpace(userAgent))
                {
                    userAgent = "Unknown";
                }

                await _auditLogRepository.AddAsync(new AuditLog
                {
                    UserID = user.Id,
                    Action = "CreateDeliveryPersonRequest",
                    EntityName = "DeliveryPersonRequest",
                    EntityID = id.ToString(),
                    OldValue = null,
                    NewValue = $"Delivery partner application created: {Delivery.FullName}",
                    IPAddress = ipAddress,
                    UserAgent = userAgent,
                    ActionDate = DateTime.UtcNow
                });

                await _notifications.SendToAllAdminsAsync(
                    title: "New delivery partner application",
                    message: $"{Delivery.FullName} has applied to become a delivery partner and is awaiting approval.",
                    type: "DeliveryRequest",
                    referenceId: id);

                TempData["Success"] = "Delivery staff request submitted successfully. Waiting for admin approval.";

                return RedirectToPage("/CustomerProfile");
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while submitting delivery request.");
                ModelState.AddModelError(string.Empty, "An error occurred while submitting your request.");
                return Page();
            }
        }
    }
}
