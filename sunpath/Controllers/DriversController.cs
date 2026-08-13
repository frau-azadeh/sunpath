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
    public class DriversController : ControllerBase
    {
        private readonly IDriverRepository _driverRepository;
        private readonly IHubContext<DriverHub> _hubContext;

        public DriversController(
            IDriverRepository driverRepository,
            IHubContext<DriverHub> hubContext)
        {
            _driverRepository = driverRepository;
            _hubContext = hubContext;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var drivers = _driverRepository.GetAll();
            return Ok(drivers);
        }

        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var driver = _driverRepository.GetById(id);

            if (driver == null)
            {
                return NotFound(new { message = "راننده پیدا نشد." });
            }

            return Ok(driver);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Driver driver)
        {
            if (driver == null)
            {
                return BadRequest(new { message = "اطلاعات راننده ارسال نشده است." });
            }

            if (string.IsNullOrWhiteSpace(driver.FirstName) ||
                string.IsNullOrWhiteSpace(driver.LastName) ||
                string.IsNullOrWhiteSpace(driver.NationalId) ||
                string.IsNullOrWhiteSpace(driver.Phone))
            {
                return BadRequest(new { message = "نام، نام خانوادگی، کد ملی و تلفن الزامی است." });
            }

            if (_driverRepository.ExistsByNationalId(driver.NationalId))
            {
                return BadRequest(new { message = "راننده‌ای با این کد ملی قبلاً ثبت شده است." });
            }

            var newId = _driverRepository.Create(driver);
            var createdDriver = _driverRepository.GetById(newId);

            await _hubContext.Clients.All.SendAsync("DriverCreated", createdDriver);

            return CreatedAtAction(nameof(GetById), new { id = newId }, createdDriver);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Driver driver)
        {
            if (driver == null)
            {
                return BadRequest(new { message = "اطلاعات راننده ارسال نشده است." });
            }

            var existingDriver = _driverRepository.GetById(id);
            if (existingDriver == null)
            {
                return NotFound(new { message = "راننده پیدا نشد." });
            }

            if (string.IsNullOrWhiteSpace(driver.FirstName) ||
                string.IsNullOrWhiteSpace(driver.LastName) ||
                string.IsNullOrWhiteSpace(driver.NationalId) ||
                string.IsNullOrWhiteSpace(driver.Phone))
            {
                return BadRequest(new { message = "نام، نام خانوادگی، کد ملی و تلفن الزامی است." });
            }

            if (_driverRepository.ExistsByNationalId(driver.NationalId, id))
            {
                return BadRequest(new { message = "کد ملی وارد شده برای راننده دیگری ثبت شده است." });
            }

            var updated = _driverRepository.Update(id, driver);
            if (!updated)
            {
                return StatusCode(500, new { message = "ویرایش راننده انجام نشد." });
            }

            var updatedDriver = _driverRepository.GetById(id);

            await _hubContext.Clients.All.SendAsync("DriverUpdated", updatedDriver);

            return Ok(updatedDriver);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existingDriver = _driverRepository.GetById(id);
            if (existingDriver == null)
            {
                return NotFound(new { message = "راننده پیدا نشد." });
            }

            var deleted = _driverRepository.Delete(id);
            if (!deleted)
            {
                return StatusCode(500, new { message = "حذف راننده انجام نشد." });
            }

            await _hubContext.Clients.All.SendAsync("DriverDeleted", id);

            return Ok(new { message = "راننده با موفقیت حذف شد.", id = id });
        }
    }
}