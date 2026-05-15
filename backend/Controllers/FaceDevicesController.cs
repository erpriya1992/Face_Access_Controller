using FaceAccessController.Api.Contracts;
using FaceAccessController.Api.Data;
using FaceAccessController.Api.Models;
using FaceAccessController.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceAccessController.Api.Controllers;

[ApiController]
[Route("api/face-devices")]
[Authorize]
public class FaceDevicesController(AppDbContext db, MiddlewareClient middleware, DeviceConfigPushQueue pushQueue) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await db.FaceDevices
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

        return Ok(rows.Select(ToListDto));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var entity = await db.FaceDevices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? NotFound() : Ok(FaceDeviceConfigMapper.ToDetailDto(entity));
    }

    [HttpPost("probe")]
    public async Task<IActionResult> Probe([FromBody] ProbeFaceDeviceRequest? body, CancellationToken ct)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.DeviceIp))
        {
            return BadRequest(new { message = "deviceIp is required." });
        }

        var result = await middleware.ProbeDeviceAsync(body.DeviceIp, ct);
        return Ok(result);
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var rows = await pushQueue.BuildHealthAsync(ct);
        return Ok(rows);
    }

    [HttpPost("{id:int}/retry-config")]
    public async Task<IActionResult> RetryConfig([FromRoute] int id, CancellationToken ct)
    {
        var ok = await pushQueue.EnqueueFromDeviceIdAsync(id, ct);
        if (!ok)
        {
            return BadRequest(new { message = "Device not found or password missing." });
        }

        return Ok(new { message = "Retry queued." });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveFaceDeviceRequest? body, CancellationToken ct)
    {
        var validation = ValidateSaveRequest(body);
        if (validation is not null)
        {
            return validation;
        }

        var ip = body!.DeviceIp.Trim();
        if (await db.FaceDevices.AnyAsync(x => x.DeviceIp == ip, ct))
        {
            return Conflict(new { message = "A device with this IP is already registered." });
        }

        var entity = new FaceDevice();
        FaceDeviceConfigMapper.ApplySaveRequest(entity, body);
        db.FaceDevices.Add(entity);
        await db.SaveChangesAsync(ct);

        return Ok(await BuildSaveResultAsync(entity, body.Settings, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] SaveFaceDeviceRequest? body, CancellationToken ct)
    {
        var validation = ValidateSaveRequest(body);
        if (validation is not null)
        {
            return validation;
        }

        var entity = await db.FaceDevices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        var ip = body!.DeviceIp.Trim();
        if (await db.FaceDevices.AnyAsync(x => x.DeviceIp == ip && x.Id != id, ct))
        {
            return Conflict(new { message = "Another device already uses this IP." });
        }

        FaceDeviceConfigMapper.ApplySaveRequest(entity, body);
        await db.SaveChangesAsync(ct);

        return Ok(await BuildSaveResultAsync(entity, body.Settings, ct));
    }

    private static IActionResult? ValidateSaveRequest(SaveFaceDeviceRequest? body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Name) || string.IsNullOrWhiteSpace(body.DeviceIp))
        {
            return new BadRequestObjectResult(new { message = "Name and deviceIp are required." });
        }

        if (body.Name.Trim().Length > 30)
        {
            return new BadRequestObjectResult(new { message = "Device name must be at most 30 characters." });
        }

        body.Settings ??= new FaceDeviceSettings();
        return null;
    }

    private async Task<FaceDeviceSaveResultDto> BuildSaveResultAsync(
        FaceDevice entity,
        FaceDeviceSettings settings,
        CancellationToken ct)
    {
        var detail = FaceDeviceConfigMapper.ToDetailDto(entity);
        if (!settings.PushConfigToDevice)
        {
            return new FaceDeviceSaveResultDto(detail, false, null);
        }

        var password = entity.DevicePassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            return new FaceDeviceSaveResultDto(
                detail,
                false,
                "Device saved locally. Enter a device password to push controller settings to the terminal.");
        }

        var push = await middleware.TryPushFaceDeviceConfigAsync(
            entity.DeviceIp,
            password,
            FaceDeviceConfigMapper.ToDeviceUiConfig(entity, settings),
            ct);

        if (!push.Success)
        {
            await pushQueue.EnqueueAsync(
                entity.Id,
                entity.DeviceIp,
                password,
                FaceDeviceConfigMapper.ToDeviceUiConfig(entity, settings),
                push.Warning,
                ct);
        }

        return new FaceDeviceSaveResultDto(detail, push.Success, push.Warning);
    }

    private static FaceDeviceListDto ToListDto(FaceDevice entity)
    {
        var settings = FaceDeviceConfigMapper.ParseSettings(entity.SettingsJson);
        return new FaceDeviceListDto(
            entity.Id,
            entity.Name,
            settings.SiteControl ?? entity.DepartmentLabel,
            entity.DeviceIp,
            settings.Direction,
            entity.IsActive,
            entity.SortOrder);
    }
}
