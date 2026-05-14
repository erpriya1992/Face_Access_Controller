using FaceAccessController.Api.Contracts;
using FaceAccessController.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FaceAccessController.Api.Services;

public class AnalyticsService(AppDbContext db)
{
    public async Task<List<HourlyAccessItem>> GetHourlyDistributionAsync(DateOnly day, CancellationToken ct)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        var end = day.ToDateTime(TimeOnly.MaxValue);

        var raw = await db.Transactions
            .Where(x => x.TransactionTime >= start && x.TransactionTime <= end)
            .GroupBy(x => x.TransactionTime.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var map = raw.ToDictionary(x => x.Hour, x => x.Count);
        var result = new List<HourlyAccessItem>();
        for (var h = 0; h < 24; h++)
        {
            result.Add(new HourlyAccessItem(h, map.GetValueOrDefault(h, 0)));
        }

        return result;
    }

    /// <summary>Last N calendar days ending at endDate (inclusive).</summary>
    public async Task<List<DailyVolumeItem>> GetDailyVolumeAsync(DateOnly endDate, int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 90);
        var startDate = endDate.AddDays(-(days - 1));
        var start = startDate.ToDateTime(TimeOnly.MinValue);
        var end = endDate.ToDateTime(TimeOnly.MaxValue);

        var txs = await db.Transactions
            .Where(x => x.TransactionTime >= start && x.TransactionTime <= end)
            .Select(x => new { x.PersonId, x.TransactionTime })
            .ToListAsync(ct);

        var byDay = txs
            .GroupBy(t => DateOnly.FromDateTime(t.TransactionTime.Date))
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<DailyVolumeItem>();
        for (var d = 0; d < days; d++)
        {
            var date = startDate.AddDays(d);
            var dayTx = byDay.GetValueOrDefault(date, []);
            var total = dayTx.Count;
            var unique = dayTx.Select(x => x.PersonId).Distinct().Count();
            result.Add(new DailyVolumeItem(date, total, unique));
        }

        return result;
    }

    public async Task<List<TopScannerItem>> GetTopScannersAsync(DateOnly start, DateOnly end, int take, bool excludeVisitors, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 50);
        var t0 = start.ToDateTime(TimeOnly.MinValue);
        var t1 = end.ToDateTime(TimeOnly.MaxValue);

        var query = db.Transactions.Where(x => x.TransactionTime >= t0 && x.TransactionTime <= t1);
        if (excludeVisitors)
        {
            // OrdinalIgnoreCase StartsWith is not translatable; ToLower() maps to SQL LOWER().
            query = query.Where(x =>
                !x.PersonId.ToLower().StartsWith("stranger") &&
                x.PersonId != "--");
        }

        var counts = await query
            .GroupBy(x => x.PersonId)
            .Select(g => new { PersonId = g.Key, Cnt = g.Count() })
            .OrderByDescending(x => x.Cnt)
            .Take(take)
            .ToListAsync(ct);

        var personIds = counts.Select(c => c.PersonId).ToList();
        var names = await db.Employees
            .Where(e => personIds.Contains(e.PersonId))
            .Select(e => new { e.PersonId, e.FullName })
            .ToDictionaryAsync(x => x.PersonId, x => x.FullName, ct);

        return counts
            .Select(x => new TopScannerItem(x.PersonId, names.GetValueOrDefault(x.PersonId), x.Cnt))
            .ToList();
    }

    public async Task<List<DepartmentAccessItem>> GetDepartmentAccessAsync(DateOnly day, CancellationToken ct)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        var end = day.ToDateTime(TimeOnly.MaxValue);

        var txs = await db.Transactions
            .Where(x =>
                x.TransactionTime >= start &&
                x.TransactionTime <= end &&
                !x.PersonId.ToLower().StartsWith("stranger"))
            .Select(x => x.PersonId)
            .ToListAsync(ct);

        var personIds = txs.Distinct().ToList();
        var deptByPerson = await db.Employees
            .Where(e => personIds.Contains(e.PersonId))
            .Select(e => new { e.PersonId, Dept = e.Department ?? "Unassigned" })
            .ToListAsync(ct);

        var deptMap = deptByPerson.ToDictionary(x => x.PersonId, x => x.Dept);

        var rollup = new Dictionary<string, (int Scans, HashSet<string> People)>();
        foreach (var pid in txs)
        {
            var dept = deptMap.GetValueOrDefault(pid) ?? "Unknown";
            if (!rollup.ContainsKey(dept))
            {
                rollup[dept] = (0, new HashSet<string>());
            }

            var x = rollup[dept];
            x.People.Add(pid);
            rollup[dept] = (x.Scans + 1, x.People);
        }

        return rollup
            .Select(kv => new DepartmentAccessItem(kv.Key, kv.Value.Scans, kv.Value.People.Count))
            .OrderByDescending(x => x.TotalScans)
            .ToList();
    }
}
