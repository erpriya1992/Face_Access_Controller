using FaceAccessController.Api.Contracts;
using FaceAccessController.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaceAccessController.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController(AttendanceService attendanceService) : ControllerBase
{
    [HttpGet("daily")]
    public async Task<IActionResult> Daily(
        [FromQuery] DateOnly? date,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AttendanceService.DefaultPageSize,
        [FromQuery] bool all = false,
        [FromQuery] bool includePhotos = false,
        [FromQuery] bool enrichMissingPhotosFromDevice = false,
        CancellationToken ct = default)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        if (!all && pageSize > AttendanceService.MaxPageSize)
        {
            return BadRequest($"pageSize cannot exceed {AttendanceService.MaxPageSize}. Use all=true to export the full report.");
        }

        var result = await attendanceService.GetDailyPagedAsync(
            targetDate,
            page,
            pageSize,
            all,
            includePhotos,
            enrichMissingPhotosFromDevice,
            ct);
        return Ok(result);
    }

    /// <summary>Returns enrollment photos for the given person IDs (for Daily tab after fast load).</summary>
    [HttpPost("daily/photo-batch")]
    public async Task<IActionResult> DailyPhotoBatch([FromBody] DailyPhotoBatchRequest? body, CancellationToken ct)
    {
        var ids = body?.PersonIds;
        if (ids == null || ids.Count == 0)
        {
            return Ok(new { items = Array.Empty<DailyPhotoBatchItem>() });
        }

        var items = await attendanceService.GetDailyEmployeePhotosBatchAsync(ids, ct);
        return Ok(new { items });
    }

    [HttpGet("monthly")]
    public async Task<IActionResult> Monthly(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AttendanceService.DefaultPageSize,
        [FromQuery] bool all = false,
        CancellationToken ct = default)
    {
        if (year <= 0 || month is < 1 or > 12)
        {
            return BadRequest("Provide valid year and month.");
        }

        if (!all && pageSize > AttendanceService.MaxPageSize)
        {
            return BadRequest($"pageSize cannot exceed {AttendanceService.MaxPageSize}. Use all=true to export the full report.");
        }

        var result = await attendanceService.GetMonthlyPagedAsync(year, month, page, pageSize, all, ct);
        return Ok(result);
    }

    [HttpGet("inout")]
    public async Task<IActionResult> InOut(
        [FromQuery] DateOnly? date,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AttendanceService.DefaultPageSize,
        [FromQuery] bool all = false,
        CancellationToken ct = default)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        if (!all && pageSize > AttendanceService.MaxPageSize)
        {
            return BadRequest($"pageSize cannot exceed {AttendanceService.MaxPageSize}. Use all=true to export the full report.");
        }

        var result = await attendanceService.GetInOutScansPagedAsync(targetDate, page, pageSize, all, ct);
        return Ok(result);
    }

    [HttpGet("hours-total")]
    public async Task<IActionResult> HoursTotal(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AttendanceService.DefaultPageSize,
        [FromQuery] bool all = false,
        CancellationToken ct = default)
    {
        if (year <= 0 || month is < 1 or > 12)
        {
            return BadRequest("Provide valid year and month.");
        }

        if (!all && pageSize > AttendanceService.MaxPageSize)
        {
            return BadRequest($"pageSize cannot exceed {AttendanceService.MaxPageSize}. Use all=true to export the full report.");
        }

        var result = await attendanceService.GetHoursTotalPagedAsync(year, month, page, pageSize, all, ct);
        return Ok(result);
    }

    /// <summary>HR/manager daily activity: per-employee access count and full timeline (multiple scans).</summary>
    [HttpGet("daily-activity")]
    public async Task<IActionResult> DailyActivity(
        [FromQuery] DateOnly? date,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AttendanceService.DefaultPageSize,
        [FromQuery] bool all = false,
        CancellationToken ct = default)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        if (!all && pageSize > AttendanceService.MaxPageSize)
        {
            return BadRequest($"pageSize cannot exceed {AttendanceService.MaxPageSize}. Use all=true to export the full report.");
        }

        var result = await attendanceService.GetDailyEmployeeActivityPagedAsync(targetDate, page, pageSize, all, ct);
        return Ok(result);
    }
}
