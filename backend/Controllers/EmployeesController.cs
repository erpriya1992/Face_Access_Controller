using FaceAccessController.Api.Contracts;
using FaceAccessController.Api.Data;
using FaceAccessController.Api.Models;
using FaceAccessController.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceAccessController.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController(
    AppDbContext db,
    IServiceScopeFactory scopeFactory,
    MiddlewareClient middlewareClient,
    ILogger<EmployeesController> logger) : ControllerBase
{
    /// <summary>All enrolled users for Settings — access control and status.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await db.Employees
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .ThenBy(e => e.PersonId)
            .Select(e => new EmployeeSettingsRow(
                e.Id,
                e.PersonId,
                e.FullName,
                e.Department,
                e.Phone,
                e.IdCardNumber,
                e.DoorAccessAllowed,
                e.CreatedAt,
                e.FaceDeviceId,
                e.FaceDevice != null ? e.FaceDevice.Name : null))
            .ToListAsync(ct);

        return Ok(rows);
    }

    /// <summary>Allow or block door access (updates DB and attempts the same on the face terminal).</summary>
    [HttpPatch("{personId}/door-access")]
    public async Task<IActionResult> UpdateDoorAccess(
        [FromRoute] string personId,
        [FromBody] UpdateDoorAccessRequest? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            return BadRequest(new { message = "Person ID is required." });
        }

        if (body is null)
        {
            return BadRequest(new { message = "Request body with doorAccessAllowed is required." });
        }

        var trimmed = personId.Trim();
        var employee = await db.Employees.FirstOrDefaultAsync(x => x.PersonId == trimmed, ct);
        if (employee is null)
        {
            return NotFound(new { message = "No registered employee found for this Person ID." });
        }

        employee.DoorAccessAllowed = body.DoorAccessAllowed;
        await db.SaveChangesAsync(ct);

        var syncedToDevice = false;
        string? warning = null;

        var links = await db.EmployeeFaceDevices.AsNoTracking()
            .Where(x => x.EmployeeId == employee.Id && x.AccessAllowed)
            .ToListAsync(ct);

        if (links.Count == 0 && employee.FaceDeviceId is int fk)
        {
            links = [new EmployeeFaceDevice { FaceDeviceId = fk, AccessAllowed = true }];
        }

        var deviceMap = links.Count == 0
            ? new Dictionary<int, FaceDevice>()
            : await db.FaceDevices.AsNoTracking()
                .Where(d => links.Select(l => l.FaceDeviceId).Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, ct);

        var syncCount = 0;
        foreach (var link in links)
        {
            if (!deviceMap.TryGetValue(link.FaceDeviceId, out var device))
            {
                continue;
            }

            var onDevice = await middlewareClient.PersonExistsOnDeviceAsync(employee.PersonId, ct, device.DeviceIp);
            if (!onDevice)
            {
                continue;
            }

            try
            {
                await middlewareClient.UpdatePersonDoorAccessOnDeviceAsync(employee, body.DoorAccessAllowed, ct, device.DeviceIp);
                syncCount++;
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Door access sync failed for {PersonId} on {DeviceIp}", employee.PersonId, device.DeviceIp);
                warning ??=
                    "Saved in the database, but one or more face terminals did not accept the update.";
            }
        }

        syncedToDevice = syncCount > 0;
        if (syncCount == 0 && links.Count > 0)
        {
            warning ??=
                "This person was not found on the selected face terminal(s). The database was updated; enroll them on the device before door access rules apply there.";
        }

        return Ok(new
        {
            personId = employee.PersonId,
            doorAccessAllowed = employee.DoorAccessAllowed,
            syncedToDevice,
            warning
        });
    }

    /// <summary>
    /// Removes the employee from the database immediately, then deletes the person on the terminal in the background
    /// so the HTTP response is not blocked by device/middleware latency.
    /// </summary>
    [HttpDelete("{personId}")]
    public async Task<IActionResult> DeleteEnrollment([FromRoute] string personId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            return BadRequest(new { message = "Person ID is required." });
        }

        var trimmed = personId.Trim();
        var employee = await db.Employees.AsNoTracking()
            .Include(e => e.FaceDeviceAccess)
            .FirstOrDefaultAsync(x => x.PersonId == trimmed, ct);
        if (employee is null)
        {
            return NotFound(new { message = "No registered employee found for this Person ID." });
        }

        var terminalIps = new List<string?>();
        foreach (var link in employee.FaceDeviceAccess.Where(x => x.AccessAllowed))
        {
            var ip = await FaceDeviceResolution.GetDeviceIpAsync(db, link.FaceDeviceId, ct);
            if (!string.IsNullOrWhiteSpace(ip))
            {
                terminalIps.Add(ip);
            }
        }

        if (terminalIps.Count == 0)
        {
            terminalIps.Add(await FaceDeviceResolution.GetDeviceIpAsync(db, employee.FaceDeviceId, ct));
        }

        var deleted = await db.Employees.Where(x => x.PersonId == trimmed).ExecuteDeleteAsync(ct);
        if (deleted == 0)
        {
            return NotFound(new { message = "No registered employee found for this Person ID." });
        }

        foreach (var ip in terminalIps.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _ = RemoveFromTerminalInBackgroundAsync(trimmed, ip);
        }

        return Ok(new
        {
            message =
                "Enrollment removed from the database. The access terminal is updating in the background (usually a few seconds)."
        });
    }

    private async Task RemoveFromTerminalInBackgroundAsync(string personId, string? deviceIpOverride)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var middleware = scope.ServiceProvider.GetRequiredService<MiddlewareClient>();
            await middleware.DeletePersonOnDeviceAsync(personId, CancellationToken.None, deviceIpOverride);
            logger.LogInformation("Removed person {PersonId} from access terminal.", personId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Background delete on access terminal failed for {PersonId}. The person was removed from the database; remove them on the device manually if needed.",
                personId);
        }
    }
}
