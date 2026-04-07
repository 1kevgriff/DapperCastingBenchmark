using System.Data;
using System.Diagnostics;
using Dapper;
using DapperBenchmarkCasting.Models;
using Microsoft.Data.SqlClient;

namespace DapperBenchmarkCasting.Helpers;

public static class WallTimeComparison
{
    private const int Iterations = 1000;

    public static void Run(string connectionString)
    {
        Console.WriteLine("=== Wall-Time Comparison ===");
        Console.WriteLine($"Each test runs {Iterations:N0} single-row lookups against the database.");
        Console.WriteLine();

        // Warm up the connection pool
        using (var conn = new SqlConnection(connectionString))
        {
            conn.Open();
            conn.QueryFirstOrDefault<Product>(
                "SELECT TOP 1 ProductId, ProductCode, ProductName, Category, Price, IsActive FROM dbo.Products WHERE ProductId = 1");
        }

        RunProductCodeComparison(connectionString);
        Console.WriteLine();
        RunOrderNumberComparison(connectionString);
    }

    private static void RunProductCodeComparison(string connectionString)
    {
        const string sql = "SELECT TOP 1 ProductId, ProductCode, ProductName, Category, Price, IsActive FROM dbo.Products WHERE ProductCode = @ProductCode";
        var target = "ART-ELEC-0500000-A";

        Console.WriteLine($"--- {Iterations:N0} ProductCode lookups (1M row table) ---");
        Console.WriteLine();

        // nvarchar (Dapper default)
        var nvarcharTime = TimeIt(() =>
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            for (int i = 0; i < Iterations; i++)
                conn.QueryFirstOrDefault<Product>(sql, new { ProductCode = target });
        });

        // varchar (correct)
        var varcharTime = TimeIt(() =>
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            for (int i = 0; i < Iterations; i++)
            {
                var p = new DynamicParameters();
                p.Add("ProductCode", target, DbType.AnsiString, size: 100);
                conn.QueryFirstOrDefault<Product>(sql, p);
            }
        });

        PrintResults("Anonymous Object (nvarchar)", nvarcharTime, "DbType.AnsiString (varchar)", varcharTime);
    }

    private static void RunOrderNumberComparison(string connectionString)
    {
        const string sql = "SELECT TOP 1 OrderId, OrderNumber, CustomerEmail, OrderDate, TotalAmount, OrderStatus FROM dbo.Orders WHERE OrderNumber = @OrderNumber";
        var target = "ORD-2020-0250000";

        Console.WriteLine($"--- {Iterations:N0} OrderNumber lookups (500K row table) ---");
        Console.WriteLine();

        var nvarcharTime = TimeIt(() =>
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            for (int i = 0; i < Iterations; i++)
                conn.QueryFirstOrDefault<Order>(sql, new { OrderNumber = target });
        });

        var varcharTime = TimeIt(() =>
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            for (int i = 0; i < Iterations; i++)
            {
                var p = new DynamicParameters();
                p.Add("OrderNumber", target, DbType.AnsiString, size: 50);
                conn.QueryFirstOrDefault<Order>(sql, p);
            }
        });

        PrintResults("Anonymous Object (nvarchar)", nvarcharTime, "DbType.AnsiString (varchar)", varcharTime);
    }

    private static TimeSpan TimeIt(Action action)
    {
        // Warm-up run
        action();

        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        return sw.Elapsed;
    }

    private static void PrintResults(string slowLabel, TimeSpan slowTime, string fastLabel, TimeSpan fastTime)
    {
        var ratio = slowTime.TotalMilliseconds / fastTime.TotalMilliseconds;

        Console.WriteLine($"  {slowLabel,-40} {FormatTime(slowTime),12}");
        Console.WriteLine($"  {fastLabel,-40} {FormatTime(fastTime),12}");
        Console.WriteLine();
        Console.WriteLine($"  Difference: {FormatTime(slowTime - fastTime)} slower ({ratio:F0}x)");
        Console.WriteLine($"  Per query:  {FormatTime(slowTime / Iterations)} vs {FormatTime(fastTime / Iterations)}");
    }

    private static string FormatTime(TimeSpan ts)
    {
        if (ts.TotalSeconds >= 1)
            return $"{ts.TotalSeconds:F2} sec";
        return $"{ts.TotalMilliseconds:F1} ms";
    }
}
