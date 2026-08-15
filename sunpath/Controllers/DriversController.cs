using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using sunpath.Hubs;
using sunpath.Models;
using sunpath.Models.Dto;
using sunpath.Services.Interface;
using System;
using System.Linq;
using System.Threading.Tasks;

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
        public async Task<IActionResult> GetAll()
        {
            var drivers = await _driverRepository.GetAllAsync();
            return Ok(drivers);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var driver = await _driverRepository.GetByIdAsync(id);

            if (driver == null)
            {
                return NotFound(new { message = "راننده پیدا نشد." });
            }

            return Ok(driver);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDriverDto dto)
        {
            var validationError = ValidateDriver(dto.FirstName, dto.LastName, dto.NationalId, dto.Phone, dto.LicenseType);
            if (validationError != null)
            {
                return BadRequest(new { message = validationError });
            }

            var nationalId = dto.NationalId.Trim();
            var phone = dto.Phone.Trim();

            if (await _driverRepository.ExistsByNationalIdAsync(nationalId))
            {
                return BadRequest(new { message = "راننده‌ای با این کد ملی قبلاً ثبت شده است." });
            }

            var driver = new Driver
            {
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                NationalId = nationalId,
                Phone = phone,
                LicenseType = dto.LicenseType
            };

            try
            {
                var newId = await _driverRepository.CreateAsync(driver);
                var createdDriver = await _driverRepository.GetByIdAsync(newId);

                await _hubContext.Clients.All.SendAsync("DriverCreated", createdDriver);

                return CreatedAtAction(nameof(GetById), new { id = newId }, createdDriver);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "خطا در ثبت راننده." });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDriverDto dto)
        {
            var existingDriver = await _driverRepository.GetByIdAsync(id);
            if (existingDriver == null)
            {
                return NotFound(new { message = "راننده پیدا نشد." });
            }

            var validationError = ValidateDriver(dto.FirstName, dto.LastName, dto.NationalId, dto.Phone, dto.LicenseType);
            if (validationError != null)
            {
                return BadRequest(new { message = validationError });
            }

            var nationalId = dto.NationalId.Trim();
            var phone = dto.Phone.Trim();

            if (await _driverRepository.ExistsByNationalIdAsync(nationalId, id))
            {
                return BadRequest(new { message = "کد ملی وارد شده برای راننده دیگری ثبت شده است." });
            }

            var driver = new Driver
            {
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                NationalId = nationalId,
                Phone = phone,
                LicenseType = dto.LicenseType
            };

            try
            {
                var updated = await _driverRepository.UpdateAsync(id, driver);
                if (!updated)
                {
                    return StatusCode(500, new { message = "ویرایش راننده انجام نشد." });
                }

                var updatedDriver = await _driverRepository.GetByIdAsync(id);

                await _hubContext.Clients.All.SendAsync("DriverUpdated", updatedDriver);

                return Ok(updatedDriver);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "خطا در ویرایش راننده." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existingDriver = await _driverRepository.GetByIdAsync(id);
            if (existingDriver == null)
            {
                return NotFound(new { message = "راننده پیدا نشد." });
            }

            try
            {
                var deleted = await _driverRepository.DeleteAsync(id);
                if (!deleted)
                {
                    return StatusCode(500, new { message = "حذف راننده انجام نشد." });
                }

                await _hubContext.Clients.All.SendAsync("DriverDeleted", id);

                return Ok(new { message = "راننده با موفقیت حذف شد.", id });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "خطا در حذف راننده." });
            }
        }

        private string ValidateDriver(
            string firstName,
            string lastName,
            string nationalId,
            string phone,
            int licenseType)
        {
            if (string.IsNullOrWhiteSpace(firstName))
                return "نام الزامی است.";

            if (string.IsNullOrWhiteSpace(lastName))
                return "نام خانوادگی الزامی است.";

            if (string.IsNullOrWhiteSpace(nationalId))
                return "کد ملی الزامی است.";

            if (string.IsNullOrWhiteSpace(phone))
                return "شماره تلفن الزامی است.";

            if (firstName.Trim().Length > 100)
                return "نام نمی‌تواند بیشتر از 100 کاراکتر باشد.";

            if (lastName.Trim().Length > 100)
                return "نام خانوادگی نمی‌تواند بیشتر از 100 کاراکتر باشد.";

            if (nationalId.Trim().Length < 10 || nationalId.Trim().Length > 20)
                return "کد ملی نامعتبر است.";

            if (!nationalId.Trim().All(char.IsDigit))
                return "کد ملی باید فقط شامل عدد باشد.";

            if (phone.Trim().Length < 10 || phone.Trim().Length > 20)
                return "شماره تلفن نامعتبر است.";

            if (licenseType < 1 || licenseType > 3)
                return "نوع گواهینامه نامعتبر است.";

            return null;
        }
    }
}
