using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using sunpath.Hubs;
using sunpath.Models;
using sunpath.Services.Interface;

namespace sunpath.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VehiclesController : ControllerBase
    {
        private readonly IVehicleService _service;
        private readonly IHubContext<VehicleHub> _hubContext;
        public VehiclesController(IVehicleService service, IHubContext<VehicleHub> hubContext)
        {
            _service = service;
            _hubContext = hubContext;
        }
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var data = await _service.GetAllVehiclesAsync();
            return Ok(data);
        }
        [HttpPost("update-location")]
        public async Task<IActionResult> UpdateLocation([FromBody] Vehicle updateInfo)
        {
            await _service.UpdateVehicleStatusAsync(
                updateInfo.Id,
                updateInfo.Latitude,
                updateInfo.Longitude,
                updateInfo.Speed,
                updateInfo.Heading);

            // ۲. ارسال دیتای جدید به تمام کلاینت‌های متصل (فرانت‌انند) بدون رفرش صفحه
            await _hubContext.Clients.All.SendAsync("VehiclePositionChanged", updateInfo);

            return Ok(new { message = "موقعیت با موفقیت آپدیت شد و به کلاینت‌ها ارسال گردید." });
        }
    }
}