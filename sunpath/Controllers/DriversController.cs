using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using sunpath.Hubs;
using sunpath.Models;
using sunpath.Models.Dto;
using sunpath.Services.Implementation;
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
        private readonly IDriverRepository _repository;
        private readonly IHubContext<DriverHub> _hub;

        public DriversController(
            IDriverRepository repository,
            IHubContext<DriverHub> hub)
        {
            _repository = repository;
            _hub = hub;
        }

        // دریافت تمام راننده‌ها
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var drivers = await _repository.GetAllAsync();

            return Ok(drivers);
        }

        // دریافت راننده بر اساس شناسه
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var driver = await _repository.GetByIdAsync(id);

            if (driver == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "راننده پیدا نشد."
                });
            }

            return Ok(driver);
        }

        // ایجاد راننده
        [HttpPost]
        public async Task<IActionResult> Create(CreateDriverDto dto)
        {
            if (dto == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "اطلاعات راننده ارسال نشده است."
                });
            }

            var error = Validate(
                dto.FirstName,
                dto.LastName,
                dto.NationalId,
                dto.Phone,
                dto.LicenseType,
                dto.Username,
                dto.Password,
                true);

            if (error != null)
            {
                return Ok(new
                {
                    success = false,
                    message = error
                });
            }

            if (await _repository.ExistsByNationalIdAsync(dto.NationalId.Trim()))
            {
                return Ok(new
                {
                    success = false,
                    message = "این کد ملی قبلاً ثبت شده است."
                });
            }

            if (await _repository.ExistsByUsernameAsync(dto.Username.Trim()))
            {
                return Ok(new
                {
                    success = false,
                    message = "این نام کاربری قبلاً استفاده شده است."
                });
            }

            var driver = BuildDriver(
                dto.FirstName,
                dto.LastName,
                dto.NationalId,
                dto.Phone,
                dto.LicenseType,
                dto.Username);

            driver.PasswordHash =
                PasswordHasher.Hash(dto.Password.Trim());

            try
            {
                var id = await _repository.CreateAsync(driver);

                var created = await _repository.GetByIdAsync(id);

                await _hub.Clients.All.SendAsync(
                    "DriverCreated",
                    created);

                return Ok(new
                {
                    success = true,
                    id = id,
                    data = created,
                    message = "راننده با موفقیت ثبت شد."
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "خطا در ثبت راننده.",
                    detail = ex.Message
                });
            }
        }

        // ویرایش راننده
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
            int id,
            UpdateDriverDto dto)
        {
            if (dto == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "اطلاعات راننده ارسال نشده است."
                });
            }

            var existing = await _repository.GetByIdAsync(id);

            if (existing == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "راننده پیدا نشد."
                });
            }

            var error = Validate(
                dto.FirstName,
                dto.LastName,
                dto.NationalId,
                dto.Phone,
                dto.LicenseType,
                dto.Username,
                dto.Password,
                false);

            if (error != null)
            {
                return Ok(new
                {
                    success = false,
                    message = error
                });
            }

            if (await _repository.ExistsByNationalIdAsync(
                dto.NationalId.Trim(),
                id))
            {
                return Ok(new
                {
                    success = false,
                    message = "این کد ملی برای راننده دیگری ثبت شده است."
                });
            }

            if (await _repository.ExistsByUsernameAsync(
                dto.Username.Trim(),
                id))
            {
                return Ok(new
                {
                    success = false,
                    message = "این نام کاربری برای راننده دیگری استفاده شده است."
                });
            }

            var driver = BuildDriver(
                dto.FirstName,
                dto.LastName,
                dto.NationalId,
                dto.Phone,
                dto.LicenseType,
                dto.Username);

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                driver.PasswordHash =
                    PasswordHasher.Hash(dto.Password.Trim());
            }
            else
            {
                driver.PasswordHash = null;
            }

            try
            {
                var updated =
                    await _repository.UpdateAsync(id, driver);

                if (!updated)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "ویرایش راننده انجام نشد."
                    });
                }

                var updatedDriver =
                    await _repository.GetByIdAsync(id);

                await _hub.Clients.All.SendAsync(
                    "DriverUpdated",
                    updatedDriver);

                return Ok(new
                {
                    success = true,
                    data = updatedDriver,
                    message = "راننده با موفقیت ویرایش شد."
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "خطا در ویرایش راننده.",
                    detail = ex.Message
                });
            }
        }

        // حذف راننده
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing =
                await _repository.GetByIdAsync(id);

            if (existing == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "راننده پیدا نشد."
                });
            }

            try
            {
                var deleted =
                    await _repository.DeleteAsync(id);

                if (!deleted)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "حذف راننده انجام نشد."
                    });
                }

                await _hub.Clients.All.SendAsync(
                    "DriverDeleted",
                    id);

                return Ok(new
                {
                    success = true,
                    id = id,
                    message = "راننده با موفقیت حذف شد."
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "امکان حذف راننده وجود ندارد. ابتدا تخصیص‌های وابسته را بررسی کنید.",
                    detail = ex.Message
                });
            }
        }

        // ساخت مدل راننده
        private static Driver BuildDriver(
            string first,
            string last,
            string national,
            string phone,
            int license,
            string username)
        {
            return new Driver
            {
                FirstName = first.Trim(),
                LastName = last.Trim(),
                NationalId = national.Trim(),
                Phone = phone.Trim(),
                LicenseType = license,
                Username = username.Trim()
            };
        }

        // اعتبارسنجی اطلاعات راننده
        private static string Validate(
            string first,
            string last,
            string national,
            string phone,
            int license,
            string username,
            string password,
            bool passwordRequired)
        {
            if (string.IsNullOrWhiteSpace(first))
                return "نام الزامی است.";

            if (string.IsNullOrWhiteSpace(last))
                return "نام خانوادگی الزامی است.";

            if (string.IsNullOrWhiteSpace(national) ||
                national.Trim().Length != 10 ||
                !national.Trim().All(char.IsDigit))
                return "کد ملی باید ۱۰ رقم باشد.";

            if (string.IsNullOrWhiteSpace(phone) ||
                phone.Trim().Length < 10 ||
                phone.Trim().Length > 20)
                return "شماره تماس نامعتبر است.";

            if (license < 1 || license > 3)
                return "نوع گواهینامه نامعتبر است.";

            if (string.IsNullOrWhiteSpace(username) ||
                username.Trim().Length < 3)
                return "نام کاربری الزامی است.";

            if (passwordRequired &&
                string.IsNullOrWhiteSpace(password))
                return "رمز عبور الزامی است.";

            if (!string.IsNullOrWhiteSpace(password) &&
                password.Trim().Length < 6)
                return "رمز عبور باید حداقل ۶ کاراکتر باشد.";

            return null;
        }
    }
}

