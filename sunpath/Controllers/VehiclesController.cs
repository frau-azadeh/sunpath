using System.Threading.Tasks;
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

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _service.GetByIdAsync(id);
            if (data == null)
                return NotFound(new { message = "وسیله نقلیه مورد نظر پیدا نشد." });

            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Vehicle vehicle)
        {
            if (vehicle == null)
                return BadRequest(new { message = "داده‌های ورودی نامعتبر است." });

            if (string.IsNullOrWhiteSpace(vehicle.PlateNumber))
                return BadRequest(new { message = "شماره پلاک الزامی است." });

            var exists = await _service.ExistsByPlateNumberAsync(vehicle.PlateNumber);
            if (exists)
                return BadRequest(new { message = "این شماره پلاک قبلاً ثبت شده است." });

            var newId = await _service.CreateAsync(vehicle);
            vehicle.Id = newId;

            await _hubContext.Clients.All.SendAsync("VehicleCreated", vehicle);

            return Ok(new
            {
                message = "وسیله نقلیه با موفقیت ثبت شد.",
                data = vehicle
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Vehicle vehicle)
        {
            if (vehicle == null)
                return BadRequest(new { message = "داده‌های ورودی نامعتبر است." });

            var current = await _service.GetByIdAsync(id);
            if (current == null)
                return NotFound(new { message = "وسیله نقلیه مورد نظر پیدا نشد." });

            if (!string.IsNullOrWhiteSpace(vehicle.PlateNumber))
            {
                var exists = await _service.ExistsByPlateNumberAsync(vehicle.PlateNumber, id);
                if (exists)
                    return BadRequest(new { message = "این شماره پلاک قبلاً برای وسیله دیگری ثبت شده است." });
            }

            var result = await _service.UpdateAsync(id, vehicle);
            if (!result)
                return StatusCode(500, new { message = "خطا در بروزرسانی اطلاعات." });

            vehicle.Id = id;

            await _hubContext.Clients.All.SendAsync("VehicleUpdated", vehicle);

            return Ok(new
            {
                message = "اطلاعات وسیله نقلیه با موفقیت ویرایش شد.",
                data = vehicle
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var current = await _service.GetByIdAsync(id);
            if (current == null)
                return NotFound(new { message = "وسیله نقلیه مورد نظر پیدا نشد." });

            var result = await _service.DeleteAsync(id);
            if (!result)
                return StatusCode(500, new { message = "خطا در حذف وسیله نقلیه." });

            await _hubContext.Clients.All.SendAsync("VehicleDeleted", id);

            return Ok(new
            {
                message = "وسیله نقلیه با موفقیت حذف شد."
            });
        }

        [HttpPost("update-location")]
        public async Task<IActionResult> UpdateLocation([FromBody] Vehicle updateInfo)
        {
            if (updateInfo == null)
                return BadRequest(new { message = "اطلاعات موقعیت نامعتبر است." });

            var success = await _service.UpdateVehicleStatusAsync(
                updateInfo.Id,
                updateInfo.Latitude,
                updateInfo.Longitude,
                updateInfo.Speed,
                updateInfo.Heading);

            if (!success)
                return NotFound(new { message = "وسیله نقلیه مورد نظر پیدا نشد." });

            await _hubContext.Clients.All.SendAsync("VehiclePositionChanged", updateInfo);

            return Ok(new { message = "موقعیت با موفقیت آپدیت شد." });
        }
    }
}
