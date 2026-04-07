using System.Data;
using BenchmarkDotNet.Attributes;
using Dapper;
using DapperBenchmarkCasting.Config;
using DapperBenchmarkCasting.Models;
using Microsoft.Data.SqlClient;

namespace DapperBenchmarkCasting.Benchmarks;

[Config(typeof(BenchmarkConfig))]
[BenchmarkCategory("SingleRow", "ProductCode")]
public class ProductCodeLookupBenchmark
{
    private const string ConnectionString = "Server=.;Database=DapperBenchmarkCasting;Trusted_Connection=true;TrustServerCertificate=true";
    private const string Sql = "SELECT TOP 1 ProductId, ProductCode, ProductName, Category, Price, IsActive FROM dbo.Products WHERE ProductCode = @ProductCode";

    private string _targetProductCode = null!;

    [GlobalSetup]
    public void Setup()
    {
        _targetProductCode = "ART-ELEC-0500000-A";

        // Verify the target row exists
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM dbo.Products WHERE ProductCode = @ProductCode",
            new { ProductCode = _targetProductCode });

        if (count == 0)
            throw new InvalidOperationException(
                $"Target product '{_targetProductCode}' not found. Run with --setup first.");
    }

    [Benchmark(Description = "Dapper: Anonymous Object (nvarchar)")]
    public Product? DefaultDapper_AnonymousObject()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        return conn.QueryFirstOrDefault<Product>(Sql, new { ProductCode = _targetProductCode });
    }

    [Benchmark(Description = "Dapper: DbType.AnsiString (varchar)", Baseline = true)]
    public Product? DynamicParams_AnsiString()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        var parameters = new DynamicParameters();
        parameters.Add("ProductCode", _targetProductCode, DbType.AnsiString, size: 100);
        return conn.QueryFirstOrDefault<Product>(Sql, parameters);
    }

    [Benchmark(Description = "Dapper: DbType.AnsiStringFixedLength (char)")]
    public Product? DynamicParams_AnsiStringFixedLength()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        var parameters = new DynamicParameters();
        parameters.Add("ProductCode", _targetProductCode, DbType.AnsiStringFixedLength, size: 100);
        return conn.QueryFirstOrDefault<Product>(Sql, parameters);
    }

    [Benchmark(Description = "Dapper: DbType.String (explicit nvarchar)")]
    public Product? DynamicParams_ExplicitNvarchar()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        var parameters = new DynamicParameters();
        parameters.Add("ProductCode", _targetProductCode, DbType.String, size: 4000);
        return conn.QueryFirstOrDefault<Product>(Sql, parameters);
    }

    [Benchmark(Description = "ADO.NET: SqlDbType.VarChar (baseline)")]
    public Product? RawAdoNet_VarcharParameter()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(Sql, conn);
        cmd.Parameters.Add("@ProductCode", SqlDbType.VarChar, 100).Value = _targetProductCode;

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new Product(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetDecimal(4),
            reader.GetBoolean(5));
    }
}
