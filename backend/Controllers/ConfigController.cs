using FaceAccessController.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace FaceAccessController.Api.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController(
    IConfiguration configuration,
    IHostEnvironment environment,
    MiddlewareClient middlewareClient) : ControllerBase
{
    [HttpGet("admin")]
    [AllowAnonymous]
    public ActionResult<object> GetAdminConfig()
    {
        var username = configuration["Admin:Username"] ?? "admin";
        var password = configuration["Admin:Password"] ?? "Admin@123";

        // For safety, only return password in Development.
        if (!environment.IsDevelopment())
        {
            return Ok(new { username });
        }

        return Ok(new { username, password });
    }

    /// <summary>Middleware host reachability and Face Reader device path (via recognition record probe).</summary>
    [HttpGet("device-status")]
    [Authorize]
    public async Task<IActionResult> GetDeviceStatus(CancellationToken ct)
    {
        var status = await middlewareClient.GetConnectivityStatusAsync(ct);
        return Ok(status);
    }
}
