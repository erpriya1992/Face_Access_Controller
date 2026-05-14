namespace FaceAccessController.Api.Contracts;

/// <summary>Paginated API envelope for report endpoints.</summary>
public record PagedReportResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

/// <summary>Request body for POST reports/daily/photo-batch (thumbnails after fast daily load).</summary>
public sealed class DailyPhotoBatchRequest
{
    public List<string> PersonIds { get; set; } = new();
}

public record DailyPhotoBatchItem(string PersonId, string? PhotoBase64);

public record DailyAttendanceItem(
    string PersonId,
    string? FullName,
    string? PhotoBase64,
    string? FaceId,
    string? Department,
    string? Phone,
    string? IdCardNumber,
    DateOnly Date,
    DateTime? FirstIn,
    DateTime? LastOut,
    string Status,
    string TotalHoursFormatted);

public record MonthlyAttendanceItem(string PersonId, int Year, int Month, int PresentDays, int AbsentDays);

/// <summary>Per scan line for a day (IN/OUT activity view).</summary>
public record InOutScanItem(string PersonId, string? FullName, DateTime TransactionTime, string DeviceSn, string EventLabel);

/// <summary>Aggregated hours for a person in a month.</summary>
public record HoursTotalItem(string PersonId, int Year, int Month, int DaysPresent, string TotalHoursFormatted);

/// <summary>One gate read in an employee's daily timeline (for HR/manager review).</summary>
public record DailyAccessEventDetail(
    int Sequence,
    int TotalForPerson,
    DateTime Time,
    string DeviceSn,
    string AccessRoleLabel,
    int? MinutesSincePrevious);

/// <summary>Per-employee daily summary + full access sequence (multiple scans / re-entry).</summary>
public record DailyEmployeeActivityItem(
    string PersonId,
    string? FullName,
    DateOnly Date,
    int AccessCount,
    DateTime FirstAccessTime,
    DateTime LastAccessTime,
    string TimeOnPremisesFormatted,
    bool RequiresHrReview,
    string MonitoringNote,
    IReadOnlyList<DailyAccessEventDetail> Events);
