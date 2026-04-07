using System.Data;
using BenchmarkDotNet.Attributes;
using Dapper;
using DapperBenchmarkCasting.Config;
using DapperBenchmarkCasting.Models;
using Microsoft.Data.SqlClient;

namespace DapperBenchmarkCasting.Benchmarks;

[Config(typeof(BenchmarkConfig))]
[BenchmarkCategory("SingleRow", "OrderNumber")]
public class OrderNumberLookupBenchmark
{
    private const string ConnectionString = "Server=.;Database=DapperBenchmarkCasting;Trusted_Connection=true;TrustServerCertificate=true";
    private const string Sql = "SELECT TOP 1 OrderId, OrderNumber, CustomerEmail, OrderDate, TotalAmount, OrderStatus FROM dbo.Orders WHERE OrderNumber = @OrderNumber";

    private string _targetOrderNumber = null!;

    [GlobalSetup]
    public void Setup()
    {
        _targetOrderNumber = "ORD-2020-0250000";

        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM dbo.Orders WHERE OrderNumber = @OrderNumber",
            new { OrderNumber = _targetOrderNumber });

        if (count == 0)
            throw new InvalidOperationException(
                $"Target order '{_targetOrderNumber}' not found. Run with --setup first.");
    }

    [Benchmark(Description = "Dapper: Anonymous Object (nvarchar)")]
    public Order? DefaultDapper_AnonymousObject()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        return conn.QueryFirstOrDefault<Order>(Sql, new { OrderNumber = _targetOrderNumber });
    }

    [Benchmark(Description = "Dapper: DbType.AnsiString (varchar)", Baseline = true)]
    public Order? DynamicParams_AnsiString()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        var parameters = new DynamicParameters();
        parameters.Add("OrderNumber", _targetOrderNumber, DbType.AnsiString, size: 50);
        return conn.QueryFirstOrDefault<Order>(Sql, parameters);
    }

    [Benchmark(Description = "Dapper: DbType.AnsiStringFixedLength (char)")]
    public Order? DynamicParams_AnsiStringFixedLength()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        var parameters = new DynamicParameters();
        parameters.Add("OrderNumber", _targetOrderNumber, DbType.AnsiStringFixedLength, size: 50);
        return conn.QueryFirstOrDefault<Order>(Sql, parameters);
    }

    [Benchmark(Description = "Dapper: DbType.String (explicit nvarchar)")]
    public Order? DynamicParams_ExplicitNvarchar()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        var parameters = new DynamicParameters();
        parameters.Add("OrderNumber", _targetOrderNumber, DbType.String, size: 4000);
        return conn.QueryFirstOrDefault<Order>(Sql, parameters);
    }

    [Benchmark(Description = "ADO.NET: SqlDbType.VarChar (baseline)")]
    public Order? RawAdoNet_VarcharParameter()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(Sql, conn);
        cmd.Parameters.Add("@OrderNumber", SqlDbType.VarChar, 50).Value = _targetOrderNumber;

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new Order(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDateTime(3),
            reader.GetDecimal(4),
            reader.GetString(5));
    }
}
