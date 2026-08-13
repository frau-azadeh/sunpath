using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace sunpath.Hubs
{
    public class VehicleHub : Hub
    {
        public async Task NotifyConnected()
        {
            await Clients.Caller.SendAsync(
                "Welcome",
                "خوش آمدید، شما به سیستم مانیتورینگ SunPath وصل شدید."
            );
        }



        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
