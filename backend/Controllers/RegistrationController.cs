using FaceAccessController.Api.Contracts;
using FaceAccessController.Api.Data;
using FaceAccessController.Api.Models;
using FaceAccessController.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceAccessController.Api.Controllers;

[ApiController]
[Route("api/registration")]
[Authorize]
public class RegistrationController(
    AppDbContext db,
    MiddlewareClient middlewareClient,
    ILogger<RegistrationController> logger,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("face")]
    public async Task<IActionResult> RegisterFace([FromBody] RegisterFaceRequest request, CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PersonId) || string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            return BadRequest("PersonId, FullName and ImageBase64 are required.");
        }

        try
        {
            if (request.ImageBase64.Contains(','))
            {
                request.ImageBase64 = request.ImageBase64.Split(',').Last();
            }

            var existsInDb = await db.Employees.AnyAsync(x => x.PersonId == request.PersonId, ct);
            if (existsInDb)
            {
                return Conflict(new
                {
                    code = "duplicate_in_database",
                    message = "This Person ID is already registered in the database."
                });
            }

            var normalizedCard = request.IdCardNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedCard))
            {
                var cardExistsInDb = await db.Employees.AnyAsync(
                    x => x.IdCardNumber != null && x.IdCardNumber.Trim() == normalizedCard,
                    ct);
                if (cardExistsInDb)
                {
                    return Conflict(new
                    {
                        code = "duplicate_card_in_database",
                        message = "This card number is already assigned to another employee in the database."
                    });
                }
            }

            var targets = await ResolveEnrollmentTargetsAsync(request, ct);
            if (targets.Count == 0)
            {
                if (await db.FaceDevices.AnyAsync(x => x.IsActive, ct) &&
                    request.FaceDeviceId is null &&
                    (request.FaceDeviceAccess is null || request.FaceDeviceAccess.All(x => !x.AccessAllowed)))
                {
                    return BadRequest(new { message = "Select at least one active face reader for this employee." });
                }

                return await RegisterOnSingleTerminalAsync(request, request.FaceDeviceId, null, ct);
            }

            var enrolledIps = new List<string>();
            var junctionRows = new List<EmployeeFaceDevice>();

            try
            {
                foreach (var target in targets)
                {
                    var existsOnDevice = await middlewareClient.PersonExistsOnDeviceAsync(request.PersonId, ct, target.DeviceIp);
                    if (existsOnDevice)
                    {
                        return Conflict(new
                        {
                            code = "duplicate_on_device",
                            message = $"This Person ID is already enrolled on face reader \"{target.Device.Name}\" ({target.Device.DeviceIp})."
                        });
                    }

                    await middlewareClient.CreatePersonAsync(request, ct, target.DeviceIp);
                    await middlewareClient.CreatePhotoAsync(request, ct, target.DeviceIp);
                    enrolledIps.Add(target.DeviceIp);

                    var faceId = await middlewareClient.TryFindFaceIdAsync(request.PersonId, ct, target.DeviceIp);
                    junctionRows.Add(new EmployeeFaceDevice
                    {
                        FaceDeviceId = target.Device.Id,
                        AccessAllowed = true,
                        FaceId = faceId,
                        SyncedAtUtc = DateTime.UtcNow
                    });
                }
            }
            catch (Exception)
            {
                await RollbackTerminalEnrollmentsAsync(request.PersonId, enrolledIps, ct);
                throw;
            }

            var employee = new Employee
            {
                PersonId = request.PersonId,
                FullName = request.FullName,
                PhotoBase64 = request.ImageBase64,
                FaceId = junctionRows.FirstOrDefault()?.FaceId,
                IdCardNumber = request.IdCardNumber,
                Phone = request.Phone,
                Department = request.Department,
                FaceDeviceId = targets[0].Device.Id,
                FaceDeviceAccess = junctionRows
            };
            db.Employees.Add(employee);
            await db.SaveChangesAsync(ct);

            return await BuildRegistrationSuccessAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return await HandleMiddlewareErrorAsync(ex, request.PersonId);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Database update failed during face registration for PersonId {PersonId}", request.PersonId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Database update failed. Check SQL permissions and connection settings.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during face registration for PersonId {PersonId}", request.PersonId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Registration failed due to server configuration. Check server logs.");
        }
    }

    private async Task<IActionResult> RegisterOnSingleTerminalAsync(
        RegisterFaceRequest request,
        int? faceDeviceFk,
        string? terminalIp,
        CancellationToken ct)
    {
        if (faceDeviceFk is int devId)
        {
            var chosen = await db.FaceDevices.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == devId && x.IsActive, ct);
            if (chosen is null)
            {
                return BadRequest(new { message = "Invalid or inactive face device." });
            }

            faceDeviceFk = chosen.Id;
            terminalIp = chosen.DeviceIp;
        }
        else
        {
            terminalIp = await FaceDeviceResolution.GetDeviceIpAsync(db, null, ct);
        }

        var existsOnDevice = await middlewareClient.PersonExistsOnDeviceAsync(request.PersonId, ct, terminalIp);
        if (existsOnDevice)
        {
            return Conflict(new
            {
                code = "duplicate_on_device",
                message = "This Person ID is already enrolled on the face terminal (photo may already be registered)."
            });
        }

        await middlewareClient.CreatePersonAsync(request, ct, terminalIp);
        await middlewareClient.CreatePhotoAsync(request, ct, terminalIp);
        var resolvedFaceId = await middlewareClient.TryFindFaceIdAsync(request.PersonId, ct, terminalIp);

        var employee = new Employee
        {
            PersonId = request.PersonId,
            FullName = request.FullName,
            PhotoBase64 = request.ImageBase64,
            FaceId = resolvedFaceId,
            IdCardNumber = request.IdCardNumber,
            Phone = request.Phone,
            Department = request.Department,
            FaceDeviceId = faceDeviceFk
        };

        if (faceDeviceFk is int fk)
        {
            employee.FaceDeviceAccess.Add(new EmployeeFaceDevice
            {
                FaceDeviceId = fk,
                AccessAllowed = true,
                FaceId = resolvedFaceId,
                SyncedAtUtc = DateTime.UtcNow
            });
        }

        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);
        return await BuildRegistrationSuccessAsync(ct);
    }

    private async Task<List<EnrollmentTarget>> ResolveEnrollmentTargetsAsync(RegisterFaceRequest request, CancellationToken ct)
    {
        var selections = (request.FaceDeviceAccess ?? [])
            .Where(x => x.AccessAllowed)
            .ToList();

        if (selections.Count == 0 && request.FaceDeviceId is int legacyId)
        {
            selections.Add(new FaceDeviceAccessSelection { FaceDeviceId = legacyId, AccessAllowed = true });
        }

        if (selections.Count == 0)
        {
            return [];
        }

        var ids = selections.Select(x => x.FaceDeviceId).Distinct().ToList();
        var devices = await db.FaceDevices.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.IsActive)
            .ToListAsync(ct);

        if (devices.Count != ids.Count)
        {
            throw new InvalidOperationException("One or more selected face readers are invalid or inactive.");
        }

        return devices
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .Select(d => new EnrollmentTarget(d))
            .ToList();
    }

    private async Task RollbackTerminalEnrollmentsAsync(string personId, IReadOnlyList<string> enrolledIps, CancellationToken ct)
    {
        foreach (var ip in enrolledIps)
        {
            try
            {
                await middlewareClient.DeletePersonOnDeviceAsync(personId, ct, ip);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Rollback delete failed for {PersonId} on {DeviceIp}", personId, ip);
            }
        }
    }

    private async Task<IActionResult> BuildRegistrationSuccessAsync(CancellationToken ct)
    {
        bool? faceDeviceUiApplied = null;
        if (configuration.GetValue("FaceDeviceUi:ApplyAfterRegistration", false))
        {
            faceDeviceUiApplied = await middlewareClient.TryApplyDeviceUiConfigAsync(ct);
        }

        var webMessage = configuration["FaceDeviceUi:WebSuccessMessage"]
                         ?? "Face registration completed and synced to device.";
        return Ok(new { message = webMessage, faceDeviceUiApplied });
    }

    private Task<IActionResult> HandleMiddlewareErrorAsync(HttpRequestException ex, string personId)
    {
        logger.LogError(ex, "Middleware connectivity failed during face registration for PersonId {PersonId}", personId);
        if (IsLikelyDuplicateCardError(ex.Message))
        {
            return Task.FromResult<IActionResult>(Conflict(new
            {
                code = "duplicate_card_on_device",
                message = "This card number is already registered on one of the selected controllers."
            }));
        }

        return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status502BadGateway, new
        {
            message = "Face device middleware is unreachable or returned an error.",
            detail = ex.Message
        }));
    }

    [HttpPost("face/replace")]
    public async Task<IActionResult> ReplaceFace([FromBody] ReplaceFaceRequest request, CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (!request.ConfirmReplace)
        {
            return BadRequest(new
            {
                message = "Set confirmReplace to true to acknowledge replacing the existing face photo and profile data for this Person ID."
            });
        }

        if (string.IsNullOrWhiteSpace(request.PersonId) || string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            return BadRequest("PersonId, FullName and ImageBase64 are required.");
        }

        try
        {
            if (request.ImageBase64.Contains(','))
            {
                request.ImageBase64 = request.ImageBase64.Split(',').Last();
            }

            var employee = await db.Employees
                .Include(e => e.FaceDeviceAccess)
                .FirstOrDefaultAsync(x => x.PersonId == request.PersonId, ct);
            if (employee is null)
            {
                return NotFound(new
                {
                    code = "employee_not_found",
                    message = "This Person ID is not in the database. Use normal registration to enroll a new employee."
                });
            }

            var deviceLinks = employee.FaceDeviceAccess.Where(x => x.AccessAllowed).ToList();
            if (deviceLinks.Count == 0 && employee.FaceDeviceId is int fk)
            {
                deviceLinks =
                [
                    new EmployeeFaceDevice { FaceDeviceId = fk, AccessAllowed = true }
                ];
            }

            if (deviceLinks.Count == 0)
            {
                var terminalIp = await FaceDeviceResolution.GetDeviceIpAsync(db, employee.FaceDeviceId, ct);
                return await ReplaceOnTerminalAsync(request, employee, terminalIp, ct);
            }

            var deviceIds = deviceLinks.Select(x => x.FaceDeviceId).ToList();
            var devices = await db.FaceDevices.AsNoTracking()
                .Where(x => deviceIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

            foreach (var link in deviceLinks)
            {
                if (!devices.TryGetValue(link.FaceDeviceId, out var device))
                {
                    continue;
                }

                var existsOnDevice = await middlewareClient.PersonExistsOnDeviceAsync(request.PersonId, ct, device.DeviceIp);
                if (!existsOnDevice)
                {
                    continue;
                }

                var faceId = link.FaceId ?? await middlewareClient.TryFindFaceIdAsync(request.PersonId, ct, device.DeviceIp);
                if (!string.IsNullOrWhiteSpace(faceId))
                {
                    await middlewareClient.UpdateDevicePhotoBase64Async(request.PersonId, faceId, request.ImageBase64, ct, device.DeviceIp);
                    link.FaceId = faceId;
                }
                else
                {
                    await middlewareClient.DeletePersonOnDeviceAsync(request.PersonId, ct, device.DeviceIp);
                    await middlewareClient.CreatePersonAsync(request, ct, device.DeviceIp);
                    await middlewareClient.CreatePhotoAsync(request, ct, device.DeviceIp);
                    link.FaceId = await middlewareClient.TryFindFaceIdAsync(request.PersonId, ct, device.DeviceIp);
                }

                link.SyncedAtUtc = DateTime.UtcNow;
            }

            employee.FullName = request.FullName;
            employee.PhotoBase64 = request.ImageBase64;
            employee.IdCardNumber = request.IdCardNumber;
            employee.Phone = request.Phone;
            employee.Department = request.Department;
            employee.FaceId = deviceLinks.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.FaceId))?.FaceId ?? employee.FaceId;

            await db.SaveChangesAsync(ct);

            bool? faceDeviceUiApplied = null;
            if (configuration.GetValue("FaceDeviceUi:ApplyAfterRegistration", false))
            {
                faceDeviceUiApplied = await middlewareClient.TryApplyDeviceUiConfigAsync(ct);
            }

            var webMessage = configuration["FaceDeviceUi:WebReplaceMessage"]
                             ?? configuration["FaceDeviceUi:WebSuccessMessage"]
                             ?? "Face photo updated and synced to the device.";
            return Ok(new { message = webMessage, faceDeviceUiApplied });
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Middleware failed during face replace for PersonId {PersonId}", request.PersonId);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Face device middleware is unreachable or returned an error during photo replace.",
                detail = ex.Message
            });
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Database update failed during face replace for PersonId {PersonId}", request.PersonId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Database update failed. Check SQL permissions and connection settings.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during face replace for PersonId {PersonId}", request.PersonId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Photo replace failed due to server configuration. Check server logs.");
        }
    }

    private async Task<IActionResult> ReplaceOnTerminalAsync(
        ReplaceFaceRequest request,
        Employee employee,
        string? terminalIp,
        CancellationToken ct)
    {
        var existsOnDevice = await middlewareClient.PersonExistsOnDeviceAsync(request.PersonId, ct, terminalIp);
        if (!existsOnDevice)
        {
            return Conflict(new
            {
                code = "not_on_device",
                message =
                    "This Person ID is not found on the face terminal. Run a full registration or restore the person on the device before replacing the photo."
            });
        }

        var faceId = await middlewareClient.TryFindFaceIdAsync(request.PersonId, ct, terminalIp);
        if (!string.IsNullOrWhiteSpace(faceId))
        {
            await middlewareClient.UpdateDevicePhotoBase64Async(request.PersonId, faceId, request.ImageBase64, ct, terminalIp);
        }
        else
        {
            await middlewareClient.DeletePersonOnDeviceAsync(request.PersonId, ct, terminalIp);
            await middlewareClient.CreatePersonAsync(request, ct, terminalIp);
            await middlewareClient.CreatePhotoAsync(request, ct, terminalIp);
        }

        employee.FullName = request.FullName;
        employee.PhotoBase64 = request.ImageBase64;
        employee.IdCardNumber = request.IdCardNumber;
        employee.Phone = request.Phone;
        employee.Department = request.Department;
        employee.FaceId = await middlewareClient.TryFindFaceIdAsync(request.PersonId, ct, terminalIp) ?? employee.FaceId;
        await db.SaveChangesAsync(ct);

        return Ok(new { message = "Face photo updated and synced to the device." });
    }

    private static bool IsLikelyDuplicateCardError(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return false;
        }

        var text = detail.ToLowerInvariant();
        var mentionsCard = text.Contains("card") || text.Contains("idcard") || text.Contains("id card");
        var mentionsDuplicate = text.Contains("already") || text.Contains("exist") || text.Contains("duplicate");
        return mentionsCard && mentionsDuplicate;
    }

    private sealed record EnrollmentTarget(FaceDevice Device)
    {
        public string DeviceIp => Device.DeviceIp;
    }
}
