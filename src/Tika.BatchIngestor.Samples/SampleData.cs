using Tika.BatchIngestor.Samples.Models;

namespace Tika.BatchIngestor.Samples;

public static class SampleData
{
    private static readonly string[] FirstNames = 
    {
        "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Henry",
        "Ivy", "Jack", "Kate", "Liam", "Mia", "Noah", "Olivia", "Paul"
    };

    private static readonly string[] LastNames = 
    {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller",
        "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez"
    };

    private static readonly string[] Cities = 
    {
        "New York", "Los Angeles", "Chicago", "Houston", "Phoenix", "Philadelphia",
        "San Antonio", "San Diego", "Dallas", "San Jose", "Austin", "Seattle"
    };

    public static async IAsyncEnumerable<CustomerRecord> GenerateCustomersAsync(int count)
    {
        var random = new Random(42);

        for (int i = 1; i <= count; i++)
        {
            var firstName = FirstNames[random.Next(FirstNames.Length)];
            var lastName = LastNames[random.Next(LastNames.Length)];
            var city = Cities[random.Next(Cities.Length)];

            yield return new CustomerRecord
            {
                Id = i,
                Name = $"{firstName} {lastName}",
                Email = $"{firstName.ToLower()}.{lastName.ToLower()}{i}@example.com",
                City = city,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(365))
            };

            if (i % 10000 == 0)
            {
                await Task.Yield();
            }
        }
    }
}
