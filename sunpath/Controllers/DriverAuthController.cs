
using Microsoft.AspNetCore.Mvc;
using sunpath.Models;
using sunpath.Services.Implementation;
using sunpath.Services.Interface;
using System.Threading.Tasks;

namespace sunpath.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DriverAuthController : ControllerBase
    {
        private readonly IDriverRepository _repository;
        private readonly DriverSessionService _sessions;

        public DriverAuthController(IDriverRepository repository, DriverSessionService sessions)
        {
            _repository = repository;
            _sessions = sessions;
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

            var driver = await _repository.FindByCredentialsAsync(request.Username.Trim(), request.Password);

            // اگر راننده پیدا نشد
            if (driver == null)
            {
                return Ok(new DriverLoginResponse
                {
                    Success = false,
                    Message = "نام کاربری یا رمز عبور نامعتبر است."
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
                Token = _sessions.Issue(driver.Id),
                Message = "ورود با موفقیت انجام شد."
            });
        }
    }
}

