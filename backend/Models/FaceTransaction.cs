namespace FaceAccessController.Api.Models;

public class FaceTransaction
{
    public long Id { get; set; }
    public string PersonId { get; set; } = string.Empty;
    public DateTime TransactionTime { get; set; }
    public string DeviceSn { get; set; } = string.Empty;
    public int Model { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}
