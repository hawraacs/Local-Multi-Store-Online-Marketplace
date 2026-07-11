using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsApiController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly NotificationManager _notificationManager;

        public NotificationsApiController(
            UserManager<User> userManager,
            NotificationManager notificationManager)
        {
            _userManager = userManager;
            _notificationManager = notificationManager;
        }

        [HttpPost("mark-messages-read")]
        public async Task<IActionResult> MarkMessagesRead()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var unread = await _notificationManager.GetUnreadAsync(user.Id);

            foreach (var n in unread.Where(x => x.Type == "Message"))
            {
                await _notificationManager.MarkAsReadAsync(n.NotificationID);
            }

            return Ok(new { success = true });
        }
    }
}