using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using finalyearproject.Extensions;

namespace finalyearproject.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.GetUserId();
        
        if (userId != null && userId != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId.ToString());
            Console.WriteLine($"✅ User {userId} connected to SignalR");
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.GetUserId();
        
        if (userId != null && userId != Guid.Empty)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId.ToString());
            Console.WriteLine($"❌ User {userId} disconnected from SignalR");
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}