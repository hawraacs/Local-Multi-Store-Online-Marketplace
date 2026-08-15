using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Local_Multi_Store_Online_Marketplace.Hubs
{
    [Authorize]
    public class AppHub : Hub
    {
    }
}
