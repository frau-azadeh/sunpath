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
        public Task SubscribeVehicle(int vehicleId)
        {
            return Groups.AddToGroupAsync(
                Context.ConnectionId,
                "vehicle-" + vehicleId);
        }

        public Task UnsubscribeVehicle(int vehicleId)
        {
            return Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                "vehicle-" + vehicleId);
        }

        /*
         * برای صفحه‌ی نقشه‌ی کلی؛
         * در حال حاضر فقط اتصال را نگه می‌دارد.
         */
        public Task SubscribeLiveMap()
        {
            return Groups.AddToGroupAsync(
                Context.ConnectionId,
                "live-map");
        }

        public Task UnsubscribeLiveMap()
        {
            return Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                "live-map");
        }
    }
}

