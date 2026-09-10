using Microsoft.AspNetCore.Mvc;
using sunpath.Models.Dto;
using sunpath.Services.Interface;
using System;
using System.Threading.Tasks;

namespace sunpath.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DispatchesController : ControllerBase
    {
        private readonly IDispatchService _service;

        public DispatchesController(IDispatchService service)
        {
            _service = service;
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

