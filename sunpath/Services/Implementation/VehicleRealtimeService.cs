using Microsoft.AspNetCore.SignalR;
using sunpath.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Services.Implementation
{
    public class VehicleRealtimeService
    {

        private readonly IHubContext<VehicleHub> _hubContext;

        public VehicleRealtimeService(IHubContext<VehicleHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task BroadcastVehiclePositionAsync(
            int vehicleId,
            double latitude,
            double longitude,
            double speed,
            double heading)
        {
            var payload = new
            {
                id = vehicleId,
                latitude = latitude,
                longitude = longitude,
                speed = speed,
                heading = heading,
                timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("VehiclePositionChanged", payload);
        }
    }
}