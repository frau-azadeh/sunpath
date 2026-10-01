using Microsoft.AspNetCore.Mvc;
using sunpath.Models.Dto;
using sunpath.Services;
using sunpath.Services.Implementation;
using sunpath.Services.Interface;
using System;
using System.Threading.Tasks;

namespace sunpath.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DispatchesController : ControllerBase
    {
        private readonly MissionWorkflowService _workflow;
        private readonly DriverSessionService _sessions;
        private readonly IDispatchService _service;
        private readonly VehicleSimulationService _simulation;

        public DispatchesController(IDispatchService service, VehicleSimulationService simulation, MissionWorkflowService workflow, DriverSessionService sessions)
        {
            _service = service;
            _workflow = workflow;
            _sessions = sessions;
            _simulation = simulation;
        }

        [HttpPost("{id}/driver-start")]
        public Task<IActionResult> DriverStart(int id) => RecordDriverStage(id, false);
        [HttpPost("{id}/driver-complete")]
        public Task<IActionResult> DriverComplete(int id) => RecordDriverStage(id, true);
        private async Task<IActionResult> RecordDriverStage(int id, bool complete)
        {
            var driverId = _sessions.FromAuthorization(Request.Headers["Authorization"].ToString());
            if (!driverId.HasValue) return Unauthorized();
            try { return Ok(await _workflow.StageAsync(id, driverId.Value, complete)); }
            catch (InvalidOperationException error) { return Conflict(new { message = error.Message }); }
        }

        [HttpPost("{id}/accept")]
        public Task<IActionResult> Accept(int id) => RecordDriverAction(id, false);
        [HttpPost("{id}/arrive")]
        public Task<IActionResult> Arrive(int id) => RecordDriverAction(id, true);
        private async Task<IActionResult> RecordDriverAction(int id, bool arrived)
        {
            var driverId = _sessions.FromAuthorization(Request.Headers["Authorization"].ToString());
            if (!driverId.HasValue) return Unauthorized();
            if (arrived) {
                var mission = await _service.GetByIdAsync(id);
                if (mission != null && await _simulation.IsRunningAsync(mission.VehicleId))
                    return Conflict(new { message = "ابتدا شبیه‌سازی را متوقف کنید؛ رسیدن واقعی جداگانه ثبت می‌شود." });
            }
            try { return Ok(await _workflow.RecordAsync(id, driverId.Value, arrived)); }
            catch (InvalidOperationException error) { return Conflict(new { message = error.Message }); }
        }

        // دریافت تمام مأموریت‌ها
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _service.GetAllAsync();

            return Ok(items);
        }

        // دریافت مأموریت بر اساس شناسه
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _service.GetByIdAsync(id);

            if (item == null)
            {
                return Ok(new
                {
                    message = "مأموریت موردنظر پیدا نشد.",
                    data = (object)null
                });
            }

            return Ok(item);
        }

        // دریافت مأموریت فعال راننده
        [HttpGet("driver/{driverId}/active")]
        public async Task<IActionResult> GetActiveForDriver(int driverId)
        {
            var item = await _service.GetActiveForDriverAsync(driverId);

            return Ok(item);
        }

        // ایجاد مأموریت
        [HttpPost]
        public async Task<IActionResult> Create(CreateDispatchRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var id = await _service.CreateAsync(request);

                return Ok(new
                {
                    id = id,
                    message = "مأموریت با موفقیت ایجاد شد."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        // ویرایش مأموریت
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
            int id,
            UpdateDispatchRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var updated = await _service.UpdateAsync(id, request);

                if (!updated)
                {
                    return Ok(new
                    {
                        message = "مأموریت پیدا نشد."
                    });
                }

                var item = await _service.GetByIdAsync(id);

                return Ok(item);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        // تغییر وضعیت مأموریت
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(
            int id,
            UpdateDispatchStatusRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var updated = await _service.UpdateStatusAsync(id, request);

                if (!updated)
                {
                    return Ok(new
                    {
                        message = "مأموریت پیدا نشد."
                    });
                }

                return Ok(new
                {
                    message = "وضعیت مأموریت با موفقیت تغییر کرد."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        // ثبت موقعیت خودرو
        [HttpPost("location")]
        public async Task<IActionResult> UpdateLocation(
            UpdateVehicleLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                if (await _simulation.IsRunningAsync(request.VehicleId))
                    return Conflict(new { message = "شبیه‌سازی این وسیله فعال است؛ ابتدا آن را متوقف کنید تا GPS واقعی ثبت شود." });
                if (request.MissionId.HasValue)
                {
                    var mission = await _service.GetByIdAsync(request.MissionId.Value);
                    if (mission == null || mission.VehicleId != request.VehicleId || mission.DriverId != request.DriverId || mission.ArrivedAtUtc.HasValue || (int)mission.Status != 2)
                        return Conflict(new { message = "مأموریت فعال نیست یا رسیدن به مقصد ثبت شده است." });
                }
                var updated =
                    await _service.UpdateVehicleLocationAsync(request);

                if (!updated)
                {
                    return Ok(new
                    {
                        message = "خودرو پیدا نشد."
                    });
                }

                return Ok(new
                {
                    message = "موقعیت با موفقیت ثبت شد."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }
        [HttpGet("driver/{driverId}/history")]
        public async Task<IActionResult> GetDriverHistory(
    int driverId
)
        {
            if (driverId <= 0)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "شناسه راننده نامعتبر است."
                    }
                );
            }

            var history =
                await _service
                    .GetDriverRouteHistoryAsync(
                        driverId
                    );

            return Ok(history);
        }
        // حذف مأموریت
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _service.DeleteAsync(id);

                if (!deleted)
                {
                    return Ok(new
                    {
                        message = "مأموریت پیدا نشد."
                    });
                }

                return Ok(new
                {
                    message = "مأموریت با موفقیت حذف شد."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }
    }
}

