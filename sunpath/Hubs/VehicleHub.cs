using Microsoft.AspNetCore.SignalR;
using sunpath.Services.Implementation;
using System;
using System.Threading.Tasks;

namespace sunpath.Hubs
{
    public class VehicleHub : Hub
    {
        private readonly DriverSessionService _sessions;
        public VehicleHub(DriverSessionService sessions) { _sessions = sessions; }
        public Task SubscribeNotifications(string audience, string token)
        {
            if (audience == "admin") return Groups.AddToGroupAsync(Context.ConnectionId, MissionNotificationService.Group("admin", null));
            var driverId = _sessions.Validate(token);
            if (audience != "driver" || !driverId.HasValue) throw new HubException("برای دریافت اعلان‌ها دوباره وارد حساب راننده شوید.");
            return Groups.AddToGroupAsync(Context.ConnectionId, MissionNotificationService.Group("driver", driverId));
        }

        public Task SubscribeVehicle(int vehicleId)
        {
            return Groups.AddToGroupAsync(
                Context.ConnectionId,
                "vehicle-" + vehicleId
            );
        }

        public Task UnsubscribeVehicle(int vehicleId)
        {
            return Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                "vehicle-" + vehicleId
            );
        }

        public Task SubscribeLiveMap()
        {
            return Groups.AddToGroupAsync(
                Context.ConnectionId,
                "live-map"
            );
        }

        public Task UnsubscribeLiveMap()
        {
            return Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                "live-map"
            );
        }

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                "live-map"
            );

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(
            Exception exception
        )
        {
            await base.OnDisconnectedAsync(
                exception
            );
        }
    }
}