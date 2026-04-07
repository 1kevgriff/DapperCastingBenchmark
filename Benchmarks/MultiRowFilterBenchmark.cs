using System.Data;
using BenchmarkDotNet.Attributes;
using Dapper;
using DapperBenchmarkCasting.Config;
using DapperBenchmarkCasting.Models;
using Microsoft.Data.SqlClient;

namespace DapperBenchmarkCasting.Benchmarks;

[Config(typeof(BenchmarkConfig))]
[BenchmarkCategory("MultiRow", "Category")]
public class MultiRowFilterBenchmark
{
    private const string ConnectionString = "Server=.;Database=DapperBenchmarkCasting;Trusted_Connection=true;TrustServerCertificate=true";

    private const string CategorySql = "SELECT TOP 100 ProductId, ProductCode, ProductName, Category, Price, IsActive FROM dbo.Products WHERE Category = @Category";
    private const string LikeSql = "SELECT TOP 100 ProductId, ProductCode, ProductName, Category, Price, IsActive FROM dbo.Products WHERE ProductCode LIKE @Prefix";

    private string _targetCategory = null!;
    private string _likePrefix = null!;

    [GlobalSetup]
    public void Setup()
    {
        _targetCategory = "ELEC";
        _likePrefix = "ART-ELEC-025%";
    }

    [Benchmark(Description = "Category Filter: Anonymous Object (nvarchar)")]
    public List<Product> CategoryFilter_AnonymousObject()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        return conn.Query<Product>(CategorySql, new { Category = _targetCategory }).AsList();
    }

    [Benchmark(Description = "Category Filter: AnsiString (varchar)", Baseline = true)]
    public List<Product> CategoryFilter_AnsiString()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        var parameters = new DynamicParameters();
        parameters.Add("Category", _targetCategory, DbType.AnsiString, size: 50);
        return conn.Query<Product>(CategorySql, parameters).AsList();
    }

    [Benchmark(Description = "LIKE Prefix: Anonymous Object (nvarchar)")]
    public List<Product> LikePrefix_AnonymousObject()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        return conn.Query<Product>(LikeSql, new { Prefix = _likePrefix }).AsList();
    }

    [Benchmark(Description = "LIKE Prefix: AnsiString (varchar)")]
    public List<Product> LikePrefix_AnsiString()
    {
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        var parameters = new DynamicParameters();
        parameters.Add("Prefix", _likePrefix, DbType.AnsiString, size: 100);
        return conn.Query<Product>(LikeSql, parameters).AsList();
    }
}
