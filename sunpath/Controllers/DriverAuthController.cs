
using Microsoft.AspNetCore.Mvc;
using sunpath.Models;
using sunpath.Services.Interface;
using System.Threading.Tasks;

namespace sunpath.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DriverAuthController : ControllerBase
    {
        private readonly IDriverRepository _repository;

        public DriverAuthController(IDriverRepository repository)
        {
            _repository = repository;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] DriverLoginRequest request)
        {
            // بررسی اطلاعات ورودی
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return Ok(new DriverLoginResponse
                {
                    Success = false,
                    Message = "لطفاً نام کاربری و رمز عبور را وارد کنید."
                });
            }

            // دریافت لیست راننده‌ها از Repository
            var drivers = await _repository.GetAllAsync();

            // پیدا کردن راننده بر اساس شماره موبایل
            var driver = drivers.Find(x =>
                x.Phone == request.Username.Trim());

            // اگر راننده پیدا نشد
            if (driver == null)
            {
                return Ok(new DriverLoginResponse
                {
                    Success = false,
                    Message = "راننده‌ای با این نام کاربری پیدا نشد."
                });
            }

            // ورود موفق
            return Ok(new DriverLoginResponse
            {
                Success = true,
                DriverId = driver.Id,
                FullName = (
                    (driver.FirstName ?? string.Empty) +
                    " " +
                    (driver.LastName ?? string.Empty)
                ).Trim(),
                Phone = driver.Phone,
                CurrentVehicleId = 0,
                Token = null,
                Message = "ورود با موفقیت انجام شد."
            });
        }
    }
}

