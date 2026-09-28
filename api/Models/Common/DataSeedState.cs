namespace api.Models.Common;

public class DataSeedState
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTime CreatedAt { get; set; }
}