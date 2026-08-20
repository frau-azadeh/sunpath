using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using sunpath.Models.Dto;
using sunpath.Services.Interface;

namespace sunpath.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DispatchesController : ControllerBase
    {
        private readonly IDispatchService _dispatchService;

        public DispatchesController(IDispatchService dispatchService)
        {
            _dispatchService = dispatchService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _dispatchService.GetAllAsync();

            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateDispatchRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var dispatchId = await _dispatchService.CreateAsync(request);

            return CreatedAtAction(
                nameof(GetById),
                new { id = dispatchId },
                new { id = dispatchId });
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(
            int id,
            [FromBody] UpdateDispatchStatusRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var isUpdated = await _dispatchService.UpdateStatusAsync(
                id,
                request);

            if (!isUpdated)
            {
                return NotFound(new
                {
                    message = "مأموریت موردنظر پیدا نشد."
                });
            }

            return NoContent();
        }

        [HttpPost("location")]
        public async Task<IActionResult> UpdateLocation(
            [FromBody] UpdateVehicleLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var isUpdated =
                await _dispatchService.UpdateVehicleLocationAsync(request);

            if (!isUpdated)
            {
                return NotFound(new
                {
                    message = "خودروی موردنظر پیدا نشد."
                });
            }

            return Ok(new
            {
                message = "موقعیت با موفقیت ثبت و ارسال شد."
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var isDeleted = await _dispatchService.DeleteAsync(id);

            if (!isDeleted)
            {
                return NotFound(new
                {
                    message = "مأموریت موردنظر پیدا نشد."
                });
            }

            return NoContent();
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _dispatchService.GetByIdAsync(id);

            if (data == null)
            {
                return NotFound(new
                {
                    message = "مأموریت موردنظر پیدا نشد."
                });
            }

            return Ok(data);
        }
    }
}