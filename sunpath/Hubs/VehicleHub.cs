using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace sunpath.Hubs
{
    public class VehicleHub : Hub
    {
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