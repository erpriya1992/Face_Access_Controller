using FaceAccessController.Api.Contracts;
using FaceAccessController.Api.Data;
using FaceAccessController.Api.Models;
using FaceAccessController.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FaceAccessController.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionsController(
    AppDbContext db,
    MiddlewareClient middlewareClient,
    IHubContext<LiveTransactionsHub> hubContext) : ControllerBase
{
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        List<MiddlewareRecordItem> records;
        try
        {
            records = await middlewareClient.FetchLatestRecordsAsync(ct);
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
            {
                return Problem(
                    detail: "Sync was cancelled (client closed the request or the host shut down).",
                    statusCode: StatusCodes.Status499ClientClosedRequest);
            }

            return Problem(
                detail:
                    "Timed out waiting for FaceReader middleware or the face device. Ensure middleware is running, FacereaderMiddleware:BaseUrl is correct, and the device is reachable. You can raise HttpClient timeout in Program.cs if the chain is legitimately slow.",
                statusCode: StatusCodes.Status504GatewayTimeout);
        }

        var inserted = new List<FaceTransaction>();
        var pendingKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in records)
        {
            if (!TryResolveTransactionTime(item.Time, out var txTime))
            {
                continue;
            }

            var personId = (item.PersonId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(personId))
            {
                continue;
            }

            var deviceSn = (item.DeviceSn ?? string.Empty).Trim();
            var dedupeKey = $"{personId}|{txTime:O}|{deviceSn}";
            if (!pendingKeys.Add(dedupeKey))
            {
                continue;
            }

            var exists = await db.Transactions.AnyAsync(x =>
                x.PersonId == personId &&
                x.TransactionTime == txTime &&
                (x.DeviceSn ?? string.Empty) == deviceSn, ct);

            if (exists)
            {
                continue;
            }

            var tx = new FaceTransaction
            {
                PersonId = personId,
                TransactionTime = txTime,
                DeviceSn = deviceSn,
                Model = item.Model
            };
            db.Transactions.Add(tx);
            inserted.Add(tx);
        }

        await db.SaveChangesAsync(ct);
        if (inserted.Count > 0)
        {
            await hubContext.Clients.All.SendAsync("transactions-updated", inserted, ct);
        }

        return Ok(new { synced = inserted.Count });
    }

    private static bool TryResolveTransactionTime(JsonElement timeElement, out DateTime txTime)
    {
        txTime = default;

        if (timeElement.ValueKind == JsonValueKind.Number && timeElement.TryGetInt64(out var unixMs))
        {
            txTime = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime;
            return true;
        }

        if (timeElement.ValueKind == JsonValueKind.String)
        {
            var raw = timeElement.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            if (long.TryParse(raw, out var unixFromString))
            {
                txTime = DateTimeOffset.FromUnixTimeMilliseconds(unixFromString).LocalDateTime;
                return true;
            }

            return DateTime.TryParse(raw, out txTime);
        }

        return false;
    }

    [HttpGet("live")]
    public async Task<IActionResult> Live([FromQuery] DateTime? startDateTime, [FromQuery] DateTime? endDateTime, CancellationToken ct)
    {
        var query = db.Transactions.AsQueryable();

        if (startDateTime.HasValue)
        {
            query = query.Where(x => x.TransactionTime >= startDateTime.Value);
        }

        if (endDateTime.HasValue)
        {
            query = query.Where(x => x.TransactionTime <= endDateTime.Value);
        }

        var data = await query
            .GroupJoin(
                db.Employees,
                t => t.PersonId,
                e => e.PersonId,
                (t, emp) => new { t, emp })
            .SelectMany(
                x => x.emp.DefaultIfEmpty(),
                (x, e) => new
                {
                    x.t.PersonId,
                    Name = e != null ? e.FullName : string.Empty,
                    Photo = e != null ? e.PhotoBase64 : null,
                    x.t.TransactionTime,
                    x.t.DeviceSn
                })
            .OrderByDescending(x => x.TransactionTime)
            .Take(100)
            .ToListAsync(ct);

        return Ok(data);
    }
}
