namespace Tika.BatchIngestor.DemoApi.Models;

/// <summary>
/// Represents an order record for batch ingestion.
/// </summary>
public class OrderRecord
{
    public long OrderId { get; set; }
    public int CustomerId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime OrderDate { get; set; }
    public DateTime? ShippedDate { get; set; }
}
