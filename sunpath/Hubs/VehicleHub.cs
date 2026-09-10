using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace sunpath.Hubs
{
    public class VehicleHub : Hub
    {
        public Task SubscribeVehicle(int vehicleId) =>
            Groups.AddToGroupAsync(Context.ConnectionId, "vehicle-" + vehicleId);

        public Task UnsubscribeVehicle(int vehicleId) =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, "vehicle-" + vehicleId);

        public Task SubscribeLiveMap() =>
            Groups.AddToGroupAsync(Context.ConnectionId, "live-map");

        public Task UnsubscribeLiveMap() =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, "live-map");

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "live-map");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception) =>
            await base.OnDisconnectedAsync(exception);
    }
}