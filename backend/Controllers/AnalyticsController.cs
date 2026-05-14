using FaceAccessController.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaceAccessController.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController(AnalyticsService analyticsService) : ControllerBase
{
    [HttpGet("hourly")]
    public async Task<IActionResult> Hourly([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.Today);
        var result = await analyticsService.GetHourlyDistributionAsync(d, ct);
        return Ok(result);
    }

    [HttpGet("daily-volume")]
    public async Task<IActionResult> DailyVolume([FromQuery] DateOnly? endDate, [FromQuery] int days = 14, CancellationToken ct = default)
    {
        var end = endDate ?? DateOnly.FromDateTime(DateTime.Today);
        var result = await analyticsService.GetDailyVolumeAsync(end, days, ct);
        return Ok(result);
    }

    [HttpGet("top-scanners")]
    public async Task<IActionResult> TopScanners(
        [FromQuery] DateOnly? start,
        [FromQuery] DateOnly? end,
        [FromQuery] int take = 10,
        [FromQuery] bool excludeVisitors = true,
        CancellationToken ct = default)
    {
        var e = end ?? DateOnly.FromDateTime(DateTime.Today);
        var s = start ?? e.AddDays(-30);
        if (s > e)
        {
            return BadRequest("start must be on or before end.");
        }

        var result = await analyticsService.GetTopScannersAsync(s, e, take, excludeVisitors, ct);
        return Ok(result);
    }

    [HttpGet("by-department")]
    public async Task<IActionResult> ByDepartment([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.Today);
        var result = await analyticsService.GetDepartmentAccessAsync(d, ct);
        return Ok(result);
    }
}
