using FaceAccessController.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FaceAccessController.Api.Services;

public static class FaceDeviceResolution
{
    /// <summary>LAN address for middleware when the employee is bound to an active face device row.</summary>
    public static async Task<string?> GetDeviceIpAsync(AppDbContext db, int? faceDeviceId, CancellationToken ct)
    {
        if (faceDeviceId is not int id)
        {
            return null;
        }

        var dev = await db.FaceDevices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        return string.IsNullOrWhiteSpace(dev?.DeviceIp) ? null : dev.DeviceIp.Trim();
    }
}
