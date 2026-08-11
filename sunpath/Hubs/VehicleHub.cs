using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Hubs
{
    public class VehicleHub: Hub
    {
        public async Task NotifyConnected()
        {
            await Clients.Caller.SendAsync("Welcome", "خوش آمدید، شما به سیستم مانیتورینگ SunPath وصل شدید.");
        }
    }
}
