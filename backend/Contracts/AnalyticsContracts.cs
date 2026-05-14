namespace FaceAccessController.Api.Contracts;

public record HourlyAccessItem(int Hour, int Count);

public record DailyVolumeItem(DateOnly Date, int TotalScans, int UniquePersons);

public record TopScannerItem(string PersonId, string? FullName, int ScanCount);

public record DepartmentAccessItem(string Department, int TotalScans, int UniqueEmployees);
