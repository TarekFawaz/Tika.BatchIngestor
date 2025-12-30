namespace Tika.BatchIngestor.DemoApi.Models;

/// <summary>
/// Represents a customer record for batch ingestion.
/// </summary>
public class CustomerRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? City { get; set; }
    public DateTime CreatedAt { get; set; }
}
