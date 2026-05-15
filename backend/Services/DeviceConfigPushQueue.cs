using System.Text.Json;
using FaceAccessController.Api.Contracts;
using FaceAccessController.Api.Data;
using FaceAccessController.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FaceAccessController.Api.Services;

public class DeviceConfigPushQueue
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeviceConfigPushQueue> _logger;

    public DeviceConfigPushQueue(IServiceScopeFactory scopeFactory, ILogger<DeviceConfigPushQueue> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task EnqueueAsync(int? faceDeviceId, string deviceIp, string devicePassword, DeviceUiConfig config, string? initialError, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceIp) || string.IsNullOrWhiteSpace(devicePassword))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        db.DeviceConfigPushJobs.Add(new DeviceConfigPushJob
        {
            FaceDeviceId = faceDeviceId,
            DeviceIp = deviceIp.Trim(),
            DevicePassword = devicePassword.Trim(),
            ConfigJson = JsonSerializer.Serialize(config),
            Status = "Pending",
            AttemptCount = 0,
            LastError = initialError,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            NextAttemptAtUtc = now
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<FaceDeviceHealthDto>> BuildHealthAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var middleware = scope.ServiceProvider.GetRequiredService<MiddlewareClient>();

        var devices = await db.FaceDevices.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

        var pendingByDevice = await db.DeviceConfigPushJobs.AsNoTracking()
            .Where(x => x.Status == "Pending" || x.Status == "Retrying")
            .GroupBy(x => x.FaceDeviceId)
            .Select(g => new { FaceDeviceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FaceDeviceId, x => x.Count, ct);

        var recentByDevice = await db.DeviceConfigPushJobs.AsNoTracking()
            .Where(x => x.FaceDeviceId != null)
            .GroupBy(x => x.FaceDeviceId)
            .Select(g => g.OrderByDescending(x => x.UpdatedAtUtc).First())
            .ToListAsync(ct);
        var recentMap = recentByDevice.ToDictionary(x => x.FaceDeviceId);

        var probes = await Task.WhenAll(devices.Select(async d =>
        {
            var probe = await middleware.ProbeDeviceAsync(d.DeviceIp, ct);
            return (d.Id, probe);
        }));
        var probeMap = probes.ToDictionary(x => x.Id, x => x.probe);

        return devices.Select(d =>
        {
            probeMap.TryGetValue(d.Id, out var probe);
            recentMap.TryGetValue(d.Id, out var recent);
            return new FaceDeviceHealthDto(
                d.Id,
                d.Name,
                d.DeviceIp,
                d.IsActive,
                probe?.Reachable ?? false,
                probe?.Detail,
                pendingByDevice.TryGetValue(d.Id, out var count) ? count : 0,
                recent?.LastSuccessAtUtc,
                recent?.LastError);
        }).ToList();
    }

    public async Task<bool> EnqueueFromDeviceIdAsync(int id, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await db.FaceDevices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (device is null || string.IsNullOrWhiteSpace(device.DevicePassword))
        {
            return false;
        }

        var settings = FaceDeviceConfigMapper.ParseSettings(device.SettingsJson);
        var cfg = FaceDeviceConfigMapper.ToDeviceUiConfig(device, settings);
        await EnqueueAsync(device.Id, device.DeviceIp, device.DevicePassword, cfg, "Manual retry requested.", ct);
        return true;
    }

    public async Task ProcessDueJobsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var middleware = scope.ServiceProvider.GetRequiredService<MiddlewareClient>();
        var now = DateTime.UtcNow;

        var due = await db.DeviceConfigPushJobs
            .Where(x => (x.Status == "Pending" || x.Status == "Retrying")
                        && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now))
            .OrderBy(x => x.NextAttemptAtUtc ?? x.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);

        foreach (var job in due)
        {
            job.AttemptCount++;
            job.LastAttemptAtUtc = now;
            job.UpdatedAtUtc = now;
            job.Status = "Retrying";

            DeviceUiConfig? cfg = null;
            try
            {
                cfg = JsonSerializer.Deserialize<DeviceUiConfig>(job.ConfigJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid config payload for job {JobId}", job.Id);
            }

            if (cfg is null)
            {
                job.Status = "Failed";
                job.LastError = "Invalid config payload.";
                continue;
            }

            var push = await middleware.TryPushFaceDeviceConfigAsync(job.DeviceIp, job.DevicePassword, cfg, ct);
            if (push.Success)
            {
                job.Status = "Completed";
                job.LastSuccessAtUtc = DateTime.UtcNow;
                job.LastError = null;
                job.NextAttemptAtUtc = null;
            }
            else
            {
                job.Status = job.AttemptCount >= 8 ? "Failed" : "Retrying";
                job.LastError = push.Warning ?? "Push failed.";
                job.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(Math.Min(300, 10 * job.AttemptCount));
            }
        }

        await db.SaveChangesAsync(ct);
    }
}

public class DeviceConfigPushRetryWorker : BackgroundService
{
    private readonly DeviceConfigPushQueue _queue;
    private readonly ILogger<DeviceConfigPushRetryWorker> _logger;

    public DeviceConfigPushRetryWorker(DeviceConfigPushQueue queue, ILogger<DeviceConfigPushRetryWorker> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _queue.ProcessDueJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Device config retry worker loop failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }
}
