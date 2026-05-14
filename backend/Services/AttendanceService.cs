using FaceAccessController.Api.Contracts;
using FaceAccessController.Api.Data;
using FaceAccessController.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace FaceAccessController.Api.Services;

public class AttendanceService(AppDbContext db, MiddlewareClient middlewareClient)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    /// <param name="enrichMissingPhotosFromDevice">
    /// When true with <paramref name="includePhotos"/>, fetches missing faces from the access terminal (slow).
    /// Default false so the daily report stays fast.
    /// </param>
    public async Task<PagedReportResult<DailyAttendanceItem>> GetDailyPagedAsync(
        DateOnly day,
        int page,
        int pageSize,
        bool all,
        bool includePhotos,
        bool enrichMissingPhotosFromDevice,
        CancellationToken ct)
    {
        var (p, ps, skip, take) = NormalizePaging(page, pageSize, all);

        var start = day.ToDateTime(TimeOnly.MinValue);
        var end = day.ToDateTime(TimeOnly.MaxValue);

        var inDay = db.Transactions.Where(x => x.TransactionTime >= start && x.TransactionTime <= end);

        var total = await inDay.Select(x => x.PersonId).Distinct().CountAsync(ct);

        List<string> pagePersonIds;
        if (all)
        {
            pagePersonIds = await inDay.Select(x => x.PersonId).Distinct().OrderBy(x => x).ToListAsync(ct);
            p = 1;
            ps = pagePersonIds.Count;
        }
        else
        {
            pagePersonIds = await inDay
                .Select(x => x.PersonId)
                .Distinct()
                .OrderBy(x => x)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }

        if (pagePersonIds.Count == 0)
        {
            return new PagedReportResult<DailyAttendanceItem>(Array.Empty<DailyAttendanceItem>(), total, p, all ? 0 : ps);
        }

        var grouped = await db.Transactions
            .Where(x => x.TransactionTime >= start && x.TransactionTime <= end && pagePersonIds.Contains(x.PersonId))
            .GroupBy(x => x.PersonId)
            .Select(g => new
            {
                PersonId = g.Key,
                FirstIn = g.Min(x => x.TransactionTime),
                LastOut = g.Max(x => x.TransactionTime)
            })
            .ToListAsync(ct);

        var byPerson = grouped.ToDictionary(x => x.PersonId, StringComparer.OrdinalIgnoreCase);

        Dictionary<string, Employee> employeesForDevice = new(StringComparer.OrdinalIgnoreCase);
        var slice = new List<DailyAttendanceItem>(pagePersonIds.Count);

        if (includePhotos)
        {
            var empList = await db.Employees
                .AsNoTracking()
                .Where(e => pagePersonIds.Contains(e.PersonId))
                .ToListAsync(ct);
            employeesForDevice = empList.ToDictionary(e => e.PersonId, StringComparer.OrdinalIgnoreCase);

            foreach (var pid in pagePersonIds)
            {
                if (!byPerson.TryGetValue(pid, out var g))
                {
                    continue;
                }

                employeesForDevice.TryGetValue(pid, out var emp);
                slice.Add(
                    new DailyAttendanceItem(
                        pid,
                        emp?.FullName,
                        emp?.PhotoBase64,
                        emp?.FaceId,
                        emp?.Department,
                        emp?.Phone,
                        emp?.IdCardNumber,
                        day,
                        g.FirstIn,
                        g.LastOut,
                        "Present",
                        FormatDuration(g.FirstIn, g.LastOut)));
            }
        }
        else
        {
            var slim = await db.Employees
                .AsNoTracking()
                .Where(e => pagePersonIds.Contains(e.PersonId))
                .Select(e => new { e.PersonId, e.FullName, e.FaceId, e.Department, e.Phone, e.IdCardNumber })
                .ToListAsync(ct);
            var byPid = slim.ToDictionary(x => x.PersonId, StringComparer.OrdinalIgnoreCase);

            foreach (var pid in pagePersonIds)
            {
                if (!byPerson.TryGetValue(pid, out var g))
                {
                    continue;
                }

                byPid.TryGetValue(pid, out var emp);
                slice.Add(
                    new DailyAttendanceItem(
                        pid,
                        emp?.FullName,
                        null,
                        emp?.FaceId,
                        emp?.Department,
                        emp?.Phone,
                        emp?.IdCardNumber,
                        day,
                        g.FirstIn,
                        g.LastOut,
                        "Present",
                        FormatDuration(g.FirstIn, g.LastOut)));
            }
        }

        var enriched = includePhotos && enrichMissingPhotosFromDevice
            ? await EnrichDailyPhotosFromDeviceAsync(slice, employeesForDevice, ct).ConfigureAwait(false)
            : slice;
        return new PagedReportResult<DailyAttendanceItem>(enriched, total, p, all ? total : ps);
    }

    /// <summary>Loads enrollment photos only (one query). Used after a fast daily report load.</summary>
    public async Task<IReadOnlyList<DailyPhotoBatchItem>> GetDailyEmployeePhotosBatchAsync(
        IReadOnlyList<string> personIds,
        CancellationToken ct)
    {
        var ids = personIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxPageSize)
            .ToList();

        if (ids.Count == 0)
        {
            return Array.Empty<DailyPhotoBatchItem>();
        }

        return await db.Employees
            .AsNoTracking()
            .Where(e => ids.Contains(e.PersonId))
            .Select(e => new DailyPhotoBatchItem(e.PersonId, e.PhotoBase64))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Fills missing photos from the face terminal (photo-find with base64) when the DB has no enrollment image.
    /// Limited concurrency; capped row count to avoid overloading the device on large exports.
    /// </summary>
    private async Task<List<DailyAttendanceItem>> EnrichDailyPhotosFromDeviceAsync(
        List<DailyAttendanceItem> slice,
        Dictionary<string, Employee> employeesByPersonId,
        CancellationToken ct)
    {
        if (slice.Count == 0 || slice.Count > MaxPageSize)
        {
            return slice;
        }

        var needIds = slice
            .Where(x => string.IsNullOrWhiteSpace(x.PhotoBase64))
            .Select(x => x.PersonId)
            .Distinct()
            .ToList();

        if (needIds.Count == 0)
        {
            return slice;
        }

        var deviceIdSet = needIds
            .Where(employeesByPersonId.ContainsKey)
            .Select(pid => employeesByPersonId[pid].FaceDeviceId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var deviceIpByFaceDeviceId = deviceIdSet.Count == 0
            ? new Dictionary<int, string>()
            : await db.FaceDevices.AsNoTracking()
                .Where(d => deviceIdSet.Contains(d.Id) && d.IsActive)
                .ToDictionaryAsync(d => d.Id, d => d.DeviceIp.Trim(), ct)
                .ConfigureAwait(false);

        string? DeviceIpForPerson(string pid)
        {
            if (!employeesByPersonId.TryGetValue(pid, out var emp) || emp.FaceDeviceId is not int fid)
            {
                return null;
            }

            return deviceIpByFaceDeviceId.TryGetValue(fid, out var ip) ? ip : null;
        }

        var fetched = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var sem = new SemaphoreSlim(4, 4);
        var tasks = needIds.Select(async pid =>
        {
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var b64 = await middlewareClient
                    .TryGetPhotoBase64FromDeviceAsync(pid, ct, DeviceIpForPerson(pid))
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(b64))
                {
                    lock (fetched)
                    {
                        fetched[pid] = b64;
                    }
                }
            }
            finally
            {
                sem.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (fetched.Count > 0)
        {
            var savableIds = fetched.Keys.Where(employeesByPersonId.ContainsKey).ToList();
            if (savableIds.Count > 0)
            {
                var rows = await db.Employees.Where(e => savableIds.Contains(e.PersonId)).ToListAsync(ct).ConfigureAwait(false);
                foreach (var emp in rows)
                {
                    if (fetched.TryGetValue(emp.PersonId, out var ph) && string.IsNullOrWhiteSpace(emp.PhotoBase64))
                    {
                        emp.PhotoBase64 = ph;
                    }
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }

        if (fetched.Count == 0)
        {
            return slice;
        }

        return slice
            .Select(x =>
            {
                if (!string.IsNullOrWhiteSpace(x.PhotoBase64))
                {
                    return x;
                }

                return fetched.TryGetValue(x.PersonId, out var ph) ? x with { PhotoBase64 = ph } : x;
            })
            .ToList();
    }

    public async Task<PagedReportResult<MonthlyAttendanceItem>> GetMonthlyPagedAsync(
        int year,
        int month,
        int page,
        int pageSize,
        bool all,
        CancellationToken ct)
    {
        var (p, ps, skip, take) = NormalizePaging(page, pageSize, all);

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var first = new DateTime(year, month, 1);
        var last = new DateTime(year, month, daysInMonth, 23, 59, 59);

        var aggQuery = db.Transactions
            .Where(x => x.TransactionTime >= first && x.TransactionTime <= last)
            .GroupBy(x => x.PersonId)
            .Select(g => new
            {
                PersonId = g.Key,
                PresentDays = g.Select(x => x.TransactionTime.Date).Distinct().Count()
            })
            .OrderBy(x => x.PersonId);

        var total = await aggQuery.CountAsync(ct);
        List<MonthlyAttendanceItem> slice;
        if (all)
        {
            var rows = await aggQuery.ToListAsync(ct);
            slice = rows
                .Select(x => new MonthlyAttendanceItem(x.PersonId, year, month, x.PresentDays, Math.Max(0, daysInMonth - x.PresentDays)))
                .ToList();
            p = 1;
            ps = slice.Count;
        }
        else
        {
            var rows = await aggQuery.Skip(skip).Take(take).ToListAsync(ct);
            slice = rows
                .Select(x => new MonthlyAttendanceItem(x.PersonId, year, month, x.PresentDays, Math.Max(0, daysInMonth - x.PresentDays)))
                .ToList();
        }

        return new PagedReportResult<MonthlyAttendanceItem>(slice, total, p, all ? total : ps);
    }

    public async Task<PagedReportResult<InOutScanItem>> GetInOutScansPagedAsync(
        DateOnly day,
        int page,
        int pageSize,
        bool all,
        CancellationToken ct)
    {
        var (p, ps, skip, take) = NormalizePaging(page, pageSize, all);

        var start = day.ToDateTime(TimeOnly.MinValue);
        var end = day.ToDateTime(TimeOnly.MaxValue);
        var baseQuery = db.Transactions.Where(x => x.TransactionTime >= start && x.TransactionTime <= end);

        var totalCount = await baseQuery.CountAsync(ct);

        List<InOutTxRow> pageTxs;
        if (all)
        {
            pageTxs = await baseQuery
                .OrderBy(x => x.TransactionTime)
                .ThenBy(x => x.Id)
                .Select(x => new InOutTxRow(x.Id, x.PersonId, x.TransactionTime, x.DeviceSn ?? string.Empty))
                .ToListAsync(ct);
            p = 1;
            ps = pageTxs.Count;
        }
        else
        {
            pageTxs = await baseQuery
                .OrderBy(x => x.TransactionTime)
                .ThenBy(x => x.Id)
                .Skip(skip)
                .Take(take)
                .Select(x => new InOutTxRow(x.Id, x.PersonId, x.TransactionTime, x.DeviceSn ?? string.Empty))
                .ToListAsync(ct);
        }

        if (pageTxs.Count == 0)
        {
            return new PagedReportResult<InOutScanItem>(Array.Empty<InOutScanItem>(), totalCount, p, all ? 0 : ps);
        }

        var personIds = pageTxs.Select(x => x.PersonId).Distinct().ToList();
        var allForPersons = await db.Transactions
            .Where(x => x.TransactionTime >= start && x.TransactionTime <= end && personIds.Contains(x.PersonId))
            .OrderBy(x => x.TransactionTime)
            .ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.PersonId })
            .ToListAsync(ct);

        var idOrderByPerson = allForPersons
            .GroupBy(x => x.PersonId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var employees = await db.Employees
            .Where(e => personIds.Contains(e.PersonId))
            .Select(e => new { e.PersonId, e.FullName })
            .ToDictionaryAsync(x => x.PersonId, x => x.FullName, ct);

        var result = new List<InOutScanItem>(pageTxs.Count);
        foreach (var t in pageTxs)
        {
            if (!idOrderByPerson.TryGetValue(t.PersonId, out var ids))
            {
                ids = new List<long> { t.Id };
            }

            var idx = ids.IndexOf(t.Id) + 1;
            var personTotal = ids.Count;
            var label = personTotal switch
            {
                1 => "Single visit",
                _ when idx == 1 => "Entry",
                _ when idx == personTotal => "Exit",
                _ => "Re-access"
            };

            employees.TryGetValue(t.PersonId, out var name);
            result.Add(new InOutScanItem(t.PersonId, name, t.TransactionTime, t.DeviceSn, label));
        }

        return new PagedReportResult<InOutScanItem>(result, totalCount, p, all ? totalCount : ps);
    }

    public async Task<PagedReportResult<HoursTotalItem>> GetHoursTotalPagedAsync(
        int year,
        int month,
        int page,
        int pageSize,
        bool all,
        CancellationToken ct)
    {
        var (p, ps, skip, take) = NormalizePaging(page, pageSize, all);

        var full = await GetHoursTotalListAsync(year, month, ct);
        var total = full.Count;
        var slice = all ? full : full.Skip(skip).Take(take).ToList();
        return new PagedReportResult<HoursTotalItem>(slice, total, p, all ? total : ps);
    }

    public async Task<PagedReportResult<DailyEmployeeActivityItem>> GetDailyEmployeeActivityPagedAsync(
        DateOnly day,
        int page,
        int pageSize,
        bool all,
        CancellationToken ct)
    {
        var (p, ps, skip, take) = NormalizePaging(page, pageSize, all);

        var start = day.ToDateTime(TimeOnly.MinValue);
        var end = day.ToDateTime(TimeOnly.MaxValue);

        var distinctPersonsQuery = db.Transactions
            .Where(x => x.TransactionTime >= start && x.TransactionTime <= end)
            .Select(x => x.PersonId)
            .Distinct();

        var totalPersons = await distinctPersonsQuery.CountAsync(ct);

        List<string> pagedPersonIds;
        if (all)
        {
            pagedPersonIds = await distinctPersonsQuery.OrderBy(x => x).ToListAsync(ct);
            p = 1;
            ps = pagedPersonIds.Count;
        }
        else
        {
            pagedPersonIds = await distinctPersonsQuery
                .OrderBy(x => x)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }

        if (pagedPersonIds.Count == 0)
        {
            return new PagedReportResult<DailyEmployeeActivityItem>(
                Array.Empty<DailyEmployeeActivityItem>(),
                totalPersons,
                p,
                all ? 0 : ps);
        }

        var txs = await db.Transactions
            .Where(x => x.TransactionTime >= start && x.TransactionTime <= end && pagedPersonIds.Contains(x.PersonId))
            .OrderBy(x => x.PersonId)
            .ThenBy(x => x.TransactionTime)
            .ThenBy(x => x.Id)
            .Select(x => new { x.PersonId, x.TransactionTime, x.DeviceSn })
            .ToListAsync(ct);

        var employees = await db.Employees
            .Where(e => pagedPersonIds.Contains(e.PersonId))
            .Select(e => new { e.PersonId, e.FullName })
            .ToDictionaryAsync(x => x.PersonId, x => (string?)x.FullName, ct);

        var byPerson = txs.GroupBy(t => t.PersonId).OrderBy(g => g.Key).ToList();
        var result = new List<DailyEmployeeActivityItem>();
        foreach (var group in byPerson)
        {
            var ordered = group.Select(x => (x.TransactionTime, x.DeviceSn ?? string.Empty)).ToList();
            result.Add(BuildActivityItem(group.Key, ordered, day, employees));
        }

        return new PagedReportResult<DailyEmployeeActivityItem>(result, totalPersons, p, all ? totalPersons : ps);
    }

    private static DailyEmployeeActivityItem BuildActivityItem(
        string personId,
        IReadOnlyList<(DateTime Time, string DeviceSn)> ordered,
        DateOnly day,
        IReadOnlyDictionary<string, string?> employees)
    {
        var n = ordered.Count;
        var first = ordered[0].Time;
        var last = ordered[^1].Time;
        var span = last - first;

        employees.TryGetValue(personId, out var fullName);

        var events = new List<DailyAccessEventDetail>();
        DateTime? prev = null;
        for (var i = 0; i < n; i++)
        {
            var row = ordered[i];
            int? minutesGap = null;
            if (prev.HasValue)
            {
                minutesGap = (int)Math.Round((row.Time - prev.Value).TotalMinutes);
            }

            var seq = i + 1;
            string roleLabel;
            if (n == 1)
            {
                roleLabel = "Single gate read";
            }
            else if (seq == 1)
            {
                roleLabel = "Entry — 1st access";
            }
            else if (seq == n)
            {
                roleLabel = $"Exit — final access ({n} of {n})";
            }
            else
            {
                roleLabel = $"Re-access — returned to gate ({seq} of {n})";
            }

            events.Add(new DailyAccessEventDetail(
                seq,
                n,
                row.Time,
                row.DeviceSn,
                roleLabel,
                minutesGap));

            prev = row.Time;
        }

        var requiresReview = n >= 3;
        var note = n switch
        {
            1 => "One read only — no separate exit recorded.",
            2 => "Typical in / out pattern (two reads).",
            _ => $"{n} gate reads — employee left and re-entered or passed the reader multiple times. Suitable for HR review."
        };

        return new DailyEmployeeActivityItem(
            personId,
            fullName,
            day,
            n,
            first,
            last,
            span.TotalSeconds < 0 ? "—" : FormatDurationSpan(span),
            requiresReview,
            note,
            events);
    }

    private async Task<List<HoursTotalItem>> GetHoursTotalListAsync(int year, int month, CancellationToken ct)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var rangeStart = new DateTime(year, month, 1);
        var rangeEnd = new DateTime(year, month, daysInMonth, 23, 59, 59);

        // One query for the whole month (replaces one query per calendar day).
        var perDay = await db.Transactions
            .Where(x => x.TransactionTime >= rangeStart && x.TransactionTime <= rangeEnd)
            .GroupBy(x => new { x.PersonId, Day = x.TransactionTime.Date })
            .Select(g => new
            {
                g.Key.PersonId,
                FirstIn = g.Min(x => x.TransactionTime),
                LastOut = g.Max(x => x.TransactionTime)
            })
            .ToListAsync(ct);

        var totals = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        var presentDaysByPerson = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in perDay)
        {
            var span = row.LastOut - row.FirstIn;
            if (span.TotalSeconds < 0)
            {
                continue;
            }

            if (!totals.ContainsKey(row.PersonId))
            {
                totals[row.PersonId] = TimeSpan.Zero;
            }

            totals[row.PersonId] = totals[row.PersonId].Add(span);
            presentDaysByPerson[row.PersonId] = presentDaysByPerson.GetValueOrDefault(row.PersonId, 0) + 1;
        }

        return totals
            .OrderBy(x => x.Key)
            .Select(x => new HoursTotalItem(
                x.Key,
                year,
                month,
                presentDaysByPerson.GetValueOrDefault(x.Key, 0),
                FormatDurationSpan(x.Value)))
            .ToList();
    }

    private static (int page, int pageSize, int skip, int take) NormalizePaging(int page, int pageSize, bool all)
    {
        if (all)
        {
            return (1, 0, 0, int.MaxValue);
        }

        var p = page < 1 ? 1 : page;
        var ps = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        var skip = (p - 1) * ps;
        return (p, ps, skip, ps);
    }

    private static string FormatDuration(DateTime? first, DateTime? last)
    {
        if (first == null || last == null)
        {
            return "—";
        }

        var span = last.Value - first.Value;
        return span.TotalSeconds < 0 ? "—" : FormatDurationSpan(span);
    }

    private static string FormatDurationSpan(TimeSpan span)
    {
        if (span.TotalSeconds < 0)
        {
            return "—";
        }

        var h = (int)span.TotalHours;
        var m = span.Minutes;
        return $"{h}h {m}m";
    }

    private sealed record InOutTxRow(long Id, string PersonId, DateTime TransactionTime, string DeviceSn);
}
