namespace DapperBenchmarkCasting.Models;

public record Order(
    int OrderId,
    string OrderNumber,
    string CustomerEmail,
    DateTime OrderDate,
    decimal TotalAmount,
    string OrderStatus);
