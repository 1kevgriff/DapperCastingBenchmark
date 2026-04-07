namespace DapperBenchmarkCasting.Models;

public record Product(
    int ProductId,
    string ProductCode,
    string ProductName,
    string Category,
    decimal Price,
    bool IsActive);
