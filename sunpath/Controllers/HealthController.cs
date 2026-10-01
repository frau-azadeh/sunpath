using System;
using Microsoft.AspNetCore.Mvc;
namespace sunpath.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        // Connectivity check only; no database credentials or personal data are exposed.
        [HttpGet]
        public IActionResult Get() => Ok(new { status = "ok", serverTimeUtc = DateTime.UtcNow });
    }
}
