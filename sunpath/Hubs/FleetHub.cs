using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using sunpath.Models.Dto;

namespace SunPath.Hubs
{
    public class FleetHub : Hub
    {
        // راننده لوکیشن زنده را می‌فرستد
        public async Task SendLocationUpdate(LiveTelemetryDto telemetry)
        {
            // ارسال به پنل ادمین و کلاینت‌ها
            await Clients.Others.SendAsync("ReceiveLocationUpdate", telemetry);
        }

        // ادمین مسیر و ماموریت را برای راننده پوش (Push) می‌کند
        public async Task AssignMissionToDriver(int driverId, object missionDetails)
        {
            await Clients.All.SendAsync($"MissionAssigned_{driverId}", missionDetails);
        }
    }
}
