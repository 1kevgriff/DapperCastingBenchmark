using Microsoft.Data.SqlClient;

namespace DapperBenchmarkCasting.Helpers;

public static class SqlStatisticsCollector
{
    public static void RunDiagnostics(string connectionString)
    {
        Console.WriteLine("=== Dapper nvarchar Implicit Conversion Diagnostics ===");
        Console.WriteLine();

        var targetProduct = "ART-ELEC-0500000-A";
        var targetOrder = "ORD-2020-0250000";
        const string productSql = "SELECT TOP 1 ProductId, ProductCode, ProductName, Category, Price, IsActive FROM dbo.Products WHERE ProductCode = @ProductCode";
        const string orderSql = "SELECT TOP 1 OrderId, OrderNumber, CustomerEmail, OrderDate, TotalAmount, OrderStatus FROM dbo.Orders WHERE OrderNumber = @OrderNumber";

        Console.WriteLine($"Target ProductCode: {targetProduct}");
        Console.WriteLine($"Target OrderNumber: {targetOrder}");
        Console.WriteLine();

        // Product lookups
        Console.WriteLine("--- Products Table: ProductCode Lookup ---");
        Console.WriteLine();

        RunWithStats(connectionString, "1. Default Dapper (nvarchar(4000))", productSql, cmd =>
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@ProductCode";
            p.Value = targetProduct;
            p.DbType = System.Data.DbType.String; // nvarchar - what Dapper does by default
            p.Size = 4000;
            cmd.Parameters.Add(p);
        });

        RunWithStats(connectionString, "2. AnsiString (varchar)", productSql, cmd =>
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@ProductCode";
            p.Value = targetProduct;
            p.DbType = System.Data.DbType.AnsiString;
            p.Size = 100;
            cmd.Parameters.Add(p);
        });

        Console.WriteLine();
        Console.WriteLine("--- Orders Table: OrderNumber Lookup ---");
        Console.WriteLine();

        RunWithStats(connectionString, "1. Default Dapper (nvarchar(4000))", orderSql, cmd =>
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@OrderNumber";
            p.Value = targetOrder;
            p.DbType = System.Data.DbType.String;
            p.Size = 4000;
            cmd.Parameters.Add(p);
        });

        RunWithStats(connectionString, "2. AnsiString (varchar)", orderSql, cmd =>
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@OrderNumber";
            p.Value = targetOrder;
            p.DbType = System.Data.DbType.AnsiString;
            p.Size = 50;
            cmd.Parameters.Add(p);
        });

        Console.WriteLine();
        Console.WriteLine("=== Execution Plan Analysis ===");
        Console.WriteLine();
        ShowExecutionPlans(connectionString, productSql, targetProduct, "ProductCode", "Products");
        ShowExecutionPlans(connectionString, orderSql, targetOrder, "OrderNumber", "Orders");
    }

    private static void RunWithStats(string connectionString, string label, string sql, Action<SqlCommand> addParams)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        conn.StatisticsEnabled = true;

        using (var cmd = new SqlCommand("DBCC DROPCLEANBUFFERS WITH NO_INFOMSGS", conn))
            cmd.ExecuteNonQuery();
        using (var cmd = new SqlCommand("DBCC FREEPROCCACHE WITH NO_INFOMSGS", conn))
            cmd.ExecuteNonQuery();

        conn.ResetStatistics();

        // Enable STATISTICS IO via InfoMessage
        var ioMessages = new List<string>();
        conn.InfoMessage += (_, e) => ioMessages.Add(e.Message);

        using (var cmd = new SqlCommand("SET STATISTICS IO ON", conn))
            cmd.ExecuteNonQuery();

        using var query = new SqlCommand(sql, conn);
        addParams(query);

        using var reader = query.ExecuteReader();
        var hasRows = reader.Read();
        reader.Close();

        var stats = conn.RetrieveStatistics();
        var executionTime = Convert.ToInt64(stats["ExecutionTime"]);

        Console.WriteLine($"  {label}:");
        Console.WriteLine($"    Found row: {hasRows}");
        Console.WriteLine($"    Execution time: {executionTime}ms");

        foreach (var msg in ioMessages)
        {
            if (msg.Contains("logical reads"))
                Console.WriteLine($"    IO Stats: {msg.Trim()}");
        }

        Console.WriteLine();
    }

    private static void ShowExecutionPlans(string connectionString, string sql, string paramValue, string paramName, string tableName)
    {
        Console.WriteLine($"--- {tableName} Table: Execution Plans ---");
        Console.WriteLine();

        using var conn = new SqlConnection(connectionString);
        conn.Open();

        // Clear plan cache so we get fresh plans
        using (var cmd = new SqlCommand("DBCC FREEPROCCACHE WITH NO_INFOMSGS", conn))
            cmd.ExecuteNonQuery();

        // Execute with nvarchar parameter to generate a cached plan
        Console.WriteLine($"  nvarchar(4000) parameter (Dapper default):");
        using (var cmd = new SqlCommand(sql, conn))
        {
            var p = cmd.CreateParameter();
            p.ParameterName = $"@{paramName}";
            p.Value = paramValue;
            p.DbType = System.Data.DbType.String;
            p.Size = 4000;
            cmd.Parameters.Add(p);
            using var r = cmd.ExecuteReader();
            r.Read();
        }

        // Grab the actual execution plan from cache
        var nvarcharPlan = GetCachedPlan(conn, paramName);
        if (nvarcharPlan != null) AnalyzePlan(nvarcharPlan);
        else Console.WriteLine("    (plan not found in cache)");

        Console.WriteLine();

        // Clear again, execute with varchar
        using (var cmd = new SqlCommand("DBCC FREEPROCCACHE WITH NO_INFOMSGS", conn))
            cmd.ExecuteNonQuery();

        Console.WriteLine($"  varchar parameter (correct):");
        using (var cmd = new SqlCommand(sql, conn))
        {
            var p = cmd.CreateParameter();
            p.ParameterName = $"@{paramName}";
            p.Value = paramValue;
            p.DbType = System.Data.DbType.AnsiString;
            p.Size = 100;
            cmd.Parameters.Add(p);
            using var r = cmd.ExecuteReader();
            r.Read();
        }

        var varcharPlan = GetCachedPlan(conn, paramName);
        if (varcharPlan != null) AnalyzePlan(varcharPlan);
        else Console.WriteLine("    (plan not found in cache)");

        Console.WriteLine();
    }

    private static string? GetCachedPlan(SqlConnection conn, string paramName)
    {
        const string planQuery = @"
            SELECT TOP 1 CAST(qp.query_plan AS NVARCHAR(MAX))
            FROM sys.dm_exec_query_stats qs
            CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
            CROSS APPLY sys.dm_exec_query_plan(qs.plan_handle) qp
            WHERE st.text LIKE @filter
              AND st.text NOT LIKE '%dm_exec%'
            ORDER BY qs.last_execution_time DESC";

        using var cmd = new SqlCommand(planQuery, conn);
        cmd.Parameters.AddWithValue("@filter", $"%{paramName}%");
        return cmd.ExecuteScalar() as string;
    }

    private static void AnalyzePlan(string planXml)
    {
        var hasConvertImplicit = planXml.Contains("CONVERT_IMPLICIT");

        // Extract PhysicalOp from the plan XML
        var physOpMatch = System.Text.RegularExpressions.Regex.Match(planXml, @"PhysicalOp=""(Index Seek|Index Scan|Clustered Index Seek|Clustered Index Scan)""");
        var scanType = physOpMatch.Success ? physOpMatch.Groups[1].Value : "Unknown";

        Console.WriteLine($"    Plan operator: {scanType}");
        Console.WriteLine($"    CONVERT_IMPLICIT present: {(hasConvertImplicit ? "YES - implicit conversion detected!" : "No")}");

        // Try to extract estimated rows and I/O cost
        ExtractPlanDetails(planXml);
    }

    private static void ExtractPlanDetails(string planXml)
    {
        // Extract EstimatedTotalSubtreeCost
        var costMatch = System.Text.RegularExpressions.Regex.Match(planXml, @"EstimatedTotalSubtreeCost=""([^""]+)""");
        if (costMatch.Success)
            Console.WriteLine($"    Estimated subtree cost: {costMatch.Groups[1].Value}");

        // Extract EstimateRows from the scan/seek operator
        var rowsMatch = System.Text.RegularExpressions.Regex.Match(planXml, @"EstimateRows=""([^""]+)""");
        if (rowsMatch.Success)
            Console.WriteLine($"    Estimated rows: {rowsMatch.Groups[1].Value}");

        // Extract EstimateIO
        var ioMatch = System.Text.RegularExpressions.Regex.Match(planXml, @"EstimateIO=""([^""]+)""");
        if (ioMatch.Success)
            Console.WriteLine($"    Estimated I/O cost: {ioMatch.Groups[1].Value}");

        // Show if CONVERT_IMPLICIT is in the ScalarOperator
        var convertMatch = System.Text.RegularExpressions.Regex.Match(planXml, @"CONVERT_IMPLICIT\([^)]+\)");
        if (convertMatch.Success)
            Console.WriteLine($"    Conversion: {convertMatch.Value}");
    }
}
