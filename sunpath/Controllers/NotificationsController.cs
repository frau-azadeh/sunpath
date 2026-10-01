using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using sunpath.Services.Implementation;
namespace sunpath.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    public class NotificationsController : ControllerBase
    {
        private readonly MissionNotificationService _notifications;
        private readonly DriverSessionService _sessions;
        public NotificationsController(MissionNotificationService notifications, DriverSessionService sessions) { _notifications = notifications; _sessions = sessions; }
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string audience = "admin")
        {
            if (audience != "admin" && audience != "driver") return BadRequest();
            var driverId = audience == "driver" ? _sessions.FromAuthorization(Request.Headers["Authorization"].ToString()) : null;
            if (audience == "driver" && !driverId.HasValue) return Unauthorized();
            return Ok(await _notifications.ListAsync(audience, driverId));
        }
        [HttpPut("{id}/read")]
        public async Task<IActionResult> Read(int id, [FromQuery] string audience = "admin")
        {
            if (audience != "admin" && audience != "driver") return BadRequest();
            var driverId = audience == "driver" ? _sessions.FromAuthorization(Request.Headers["Authorization"].ToString()) : null;
            if (audience == "driver" && !driverId.HasValue) return Unauthorized();
            if (!await _notifications.MarkReadAsync(id, audience, driverId)) return NotFound();
            return Ok(new { success = true });
        }
    }
}
