using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.DemoApi.Models;

namespace Tika.BatchIngestor.DemoApi.Configuration;

/// <summary>
/// Row mapper for SensorReading entities.
/// </summary>
public class SensorReadingRowMapper : IRowMapper<SensorReading>
{
    private static readonly IReadOnlyList<string> Columns = new[]
    {
        "Id", "DeviceId", "SensorType", "Value", "Unit", "Timestamp", "Location"
    };

    public IReadOnlyDictionary<string, object?> Map(SensorReading item)
    {
        return new Dictionary<string, object?>
        {
            ["Id"] = item.Id,
            ["DeviceId"] = item.DeviceId,
            ["SensorType"] = item.SensorType,
            ["Value"] = item.Value,
            ["Unit"] = item.Unit,
            ["Timestamp"] = item.Timestamp,
            ["Location"] = item.Location
        };
    }

    public IReadOnlyList<string> GetColumns() => Columns;
}

/// <summary>
/// Row mapper for CustomerRecord entities.
/// </summary>
public class CustomerRecordRowMapper : IRowMapper<CustomerRecord>
{
    private static readonly IReadOnlyList<string> Columns = new[]
    {
        "Id", "Name", "Email", "City", "CreatedAt"
    };

    public IReadOnlyDictionary<string, object?> Map(CustomerRecord item)
    {
        return new Dictionary<string, object?>
        {
            ["Id"] = item.Id,
            ["Name"] = item.Name,
            ["Email"] = item.Email,
            ["City"] = item.City,
            ["CreatedAt"] = item.CreatedAt
        };
    }

    public IReadOnlyList<string> GetColumns() => Columns;
}

/// <summary>
/// Row mapper for OrderRecord entities.
/// </summary>
public class OrderRecordRowMapper : IRowMapper<OrderRecord>
{
    private static readonly IReadOnlyList<string> Columns = new[]
    {
        "OrderId", "CustomerId", "ProductCode", "Quantity", "UnitPrice",
        "TotalAmount", "Status", "OrderDate", "ShippedDate"
    };

    public IReadOnlyDictionary<string, object?> Map(OrderRecord item)
    {
        return new Dictionary<string, object?>
        {
            ["OrderId"] = item.OrderId,
            ["CustomerId"] = item.CustomerId,
            ["ProductCode"] = item.ProductCode,
            ["Quantity"] = item.Quantity,
            ["UnitPrice"] = item.UnitPrice,
            ["TotalAmount"] = item.TotalAmount,
            ["Status"] = item.Status,
            ["OrderDate"] = item.OrderDate,
            ["ShippedDate"] = item.ShippedDate
        };
    }

    public IReadOnlyList<string> GetColumns() => Columns;
}
