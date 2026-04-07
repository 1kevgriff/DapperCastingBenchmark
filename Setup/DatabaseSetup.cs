using Microsoft.Data.SqlClient;

namespace DapperBenchmarkCasting.Setup;

public static class DatabaseSetup
{
    private const string MasterConnectionString = "Server=.;Database=master;Trusted_Connection=true;TrustServerCertificate=true";

    public static void EnsureDatabaseSeeded(string connectionString)
    {
        Console.WriteLine("Setting up database...");

        CreateDatabase();
        CreateTables(connectionString);
        SeedData(connectionString);
        CreateHelperProcs(connectionString);
        UpdateStatistics(connectionString);

        Console.WriteLine("Database setup complete.");
    }

    private static void CreateDatabase()
    {
        Console.WriteLine("  Creating database if not exists...");
        ExecuteNonQuery(MasterConnectionString, @"
            IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'DapperBenchmarkCasting')
                CREATE DATABASE DapperBenchmarkCasting;");

        ExecuteNonQuery(MasterConnectionString, @"
            ALTER DATABASE DapperBenchmarkCasting SET RECOVERY SIMPLE;");
    }

    private static void CreateTables(string connectionString)
    {
        Console.WriteLine("  Creating tables...");

        ExecuteNonQuery(connectionString, @"
            IF OBJECT_ID('dbo.OrderItems', 'U') IS NOT NULL DROP TABLE dbo.OrderItems;
            IF OBJECT_ID('dbo.Orders', 'U') IS NOT NULL DROP TABLE dbo.Orders;
            IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL DROP TABLE dbo.Products;
            IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;");

        ExecuteNonQuery(connectionString, @"
            CREATE TABLE dbo.Categories (
                CategoryId INT IDENTITY(1,1) NOT NULL,
                CategoryCode VARCHAR(20) NOT NULL,
                CategoryName NVARCHAR(100) NOT NULL,
                CONSTRAINT PK_Categories PRIMARY KEY CLUSTERED (CategoryId)
            );

            CREATE UNIQUE NONCLUSTERED INDEX IX_Categories_CategoryCode
                ON dbo.Categories (CategoryCode);

            INSERT INTO dbo.Categories (CategoryCode, CategoryName) VALUES
                ('ELEC','Electronics'), ('CLTH','Clothing'), ('HOME','Home & Garden'),
                ('FOOD','Food & Beverage'), ('SPRT','Sports'), ('BOOK','Books'),
                ('TOYS','Toys & Games'), ('AUTO','Automotive'), ('HLTH','Health'),
                ('BEAU','Beauty'), ('OFFI','Office'), ('PETS','Pet Supplies'),
                ('TOOL','Tools'), ('MUSIC','Music'), ('JEWL','Jewelry');");

        ExecuteNonQuery(connectionString, @"
            CREATE TABLE dbo.Products (
                ProductId INT IDENTITY(1,1) NOT NULL,
                ProductCode VARCHAR(100) NOT NULL,
                ProductName NVARCHAR(200) NOT NULL,
                Description NVARCHAR(1000) NULL,
                CategoryId INT NOT NULL,
                Category VARCHAR(50) NOT NULL,
                Price DECIMAL(18,2) NOT NULL,
                CostPrice DECIMAL(18,2) NOT NULL,
                StockQuantity INT NOT NULL DEFAULT 0,
                SKU VARCHAR(50) NOT NULL,
                BarCode VARCHAR(50) NULL,
                Weight DECIMAL(10,3) NULL,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                ModifiedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (ProductId),
                CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId)
            );

            CREATE UNIQUE NONCLUSTERED INDEX IX_Products_ProductCode
                ON dbo.Products (ProductCode)
                INCLUDE (ProductName, Price, IsActive);

            CREATE NONCLUSTERED INDEX IX_Products_SKU
                ON dbo.Products (SKU);

            CREATE NONCLUSTERED INDEX IX_Products_Category
                ON dbo.Products (Category)
                INCLUDE (ProductCode, ProductName, Price);");

        ExecuteNonQuery(connectionString, @"
            CREATE TABLE dbo.Orders (
                OrderId INT IDENTITY(1,1) NOT NULL,
                OrderNumber VARCHAR(50) NOT NULL,
                CustomerEmail VARCHAR(200) NOT NULL,
                CustomerId INT NOT NULL,
                OrderDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                TotalAmount DECIMAL(18,2) NOT NULL,
                OrderStatus VARCHAR(20) NOT NULL DEFAULT 'Pending',
                ShippingAddress NVARCHAR(500) NULL,
                CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (OrderId)
            );

            CREATE UNIQUE NONCLUSTERED INDEX IX_Orders_OrderNumber
                ON dbo.Orders (OrderNumber)
                INCLUDE (CustomerEmail, TotalAmount, OrderStatus);

            CREATE NONCLUSTERED INDEX IX_Orders_CustomerEmail
                ON dbo.Orders (CustomerEmail)
                INCLUDE (OrderNumber, TotalAmount);

            CREATE NONCLUSTERED INDEX IX_Orders_OrderStatus
                ON dbo.Orders (OrderStatus)
                INCLUDE (OrderNumber, TotalAmount);");
    }

    private static void SeedData(string connectionString)
    {
        SeedCategories(connectionString);
        SeedProducts(connectionString);
        SeedOrders(connectionString);
    }

    private static void SeedCategories(string connectionString)
    {
        // Already seeded in CreateTables
    }

    private static void SeedProducts(string connectionString)
    {
        var count = ExecuteScalar<int>(connectionString, "SELECT COUNT(*) FROM dbo.Products");
        if (count >= 1_000_000)
        {
            Console.WriteLine($"  Products already has {count:N0} rows, skipping seed.");
            return;
        }

        Console.WriteLine("  Seeding 1,000,000 products (this may take a minute)...");

        // Truncate if partially seeded
        if (count > 0)
            ExecuteNonQuery(connectionString, "TRUNCATE TABLE dbo.Products;");

        ExecuteNonQuery(connectionString, @"
            ;WITH
            Categories AS (
                SELECT CategoryId, CategoryCode
                FROM dbo.Categories
            ),
            Prefixes AS (
                SELECT v.Prefix
                FROM (VALUES ('PRD'),('ITEM'),('SKU'),('PROD'),('ART'),('GDS'),('MRC'),('STK')) v(Prefix)
            ),
            Suffixes AS (
                SELECT v.Suffix
                FROM (VALUES ('A'),('B'),('C'),('X'),('Z')) v(Suffix)
            ),
            Numbers AS (
                SELECT TOP 1000000
                    ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Seq
                FROM sys.all_objects a
                CROSS JOIN sys.all_objects b
            )
            INSERT INTO dbo.Products (ProductCode, ProductName, Description, CategoryId, Category, Price, CostPrice, StockQuantity, SKU, BarCode, Weight, IsActive)
            SELECT
                p.Prefix + '-' + c.CategoryCode + '-' + RIGHT('0000000' + CAST(n.Seq AS VARCHAR), 7) + '-' + s.Suffix,
                'Product ' + CAST(n.Seq AS VARCHAR) + ' ' + c.CategoryCode,
                'Description for product ' + CAST(n.Seq AS VARCHAR),
                c.CategoryId,
                c.CategoryCode,
                CAST(10.00 + (n.Seq % 99000) / 100.0 AS DECIMAL(18,2)),
                CAST(5.00 + (n.Seq % 49500) / 100.0 AS DECIMAL(18,2)),
                n.Seq % 1000,
                'SKU-' + RIGHT('0000000' + CAST(n.Seq AS VARCHAR), 7),
                '890' + RIGHT('0000000000' + CAST(n.Seq AS VARCHAR), 10),
                CAST((n.Seq % 5000) / 100.0 AS DECIMAL(10,3)),
                CASE WHEN n.Seq % 20 = 0 THEN 0 ELSE 1 END
            FROM Numbers n
            CROSS JOIN (SELECT TOP 1 * FROM Prefixes ORDER BY Prefix) p
            CROSS JOIN (SELECT TOP 1 * FROM Categories ORDER BY CategoryId) c
            CROSS JOIN (SELECT TOP 1 * FROM Suffixes ORDER BY Suffix) s
            WHERE n.Seq <= 1000000;", timeout: 300);

        count = ExecuteScalar<int>(connectionString, "SELECT COUNT(*) FROM dbo.Products");
        Console.WriteLine($"  Products seeded: {count:N0} rows.");
    }

    private static void SeedOrders(string connectionString)
    {
        var count = ExecuteScalar<int>(connectionString, "SELECT COUNT(*) FROM dbo.Orders");
        if (count >= 500_000)
        {
            Console.WriteLine($"  Orders already has {count:N0} rows, skipping seed.");
            return;
        }

        Console.WriteLine("  Seeding 500,000 orders...");

        if (count > 0)
            ExecuteNonQuery(connectionString, "TRUNCATE TABLE dbo.Orders;");

        ExecuteNonQuery(connectionString, @"
            ;WITH Numbers AS (
                SELECT TOP 500000
                    ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Seq
                FROM sys.all_objects a
                CROSS JOIN sys.all_objects b
            )
            INSERT INTO dbo.Orders (OrderNumber, CustomerEmail, CustomerId, OrderDate, TotalAmount, OrderStatus, ShippingAddress)
            SELECT
                'ORD-' + CAST(2020 + (n.Seq % 5) AS VARCHAR) + '-' + RIGHT('0000000' + CAST(n.Seq AS VARCHAR), 7),
                'customer' + CAST(n.Seq % 50000 AS VARCHAR) + '@example.com',
                n.Seq % 50000 + 1,
                DATEADD(DAY, -(n.Seq % 1825), SYSUTCDATETIME()),
                CAST(25.00 + (n.Seq % 97500) / 100.0 AS DECIMAL(18,2)),
                CASE n.Seq % 5
                    WHEN 0 THEN 'Pending'
                    WHEN 1 THEN 'Processing'
                    WHEN 2 THEN 'Shipped'
                    WHEN 3 THEN 'Delivered'
                    ELSE 'Cancelled'
                END,
                CAST(n.Seq AS VARCHAR) + ' Main Street, City ' + CAST(n.Seq % 500 AS VARCHAR)
            FROM Numbers n;", timeout: 300);

        count = ExecuteScalar<int>(connectionString, "SELECT COUNT(*) FROM dbo.Orders");
        Console.WriteLine($"  Orders seeded: {count:N0} rows.");
    }

    private static void UpdateStatistics(string connectionString)
    {
        Console.WriteLine("  Updating statistics...");
        ExecuteNonQuery(connectionString, "UPDATE STATISTICS dbo.Products WITH FULLSCAN;", timeout: 120);
        ExecuteNonQuery(connectionString, "UPDATE STATISTICS dbo.Orders WITH FULLSCAN;", timeout: 120);
        Console.WriteLine("  Rebuilding indexes...");
        ExecuteNonQuery(connectionString, "ALTER INDEX ALL ON dbo.Products REBUILD;", timeout: 120);
        ExecuteNonQuery(connectionString, "ALTER INDEX ALL ON dbo.Orders REBUILD;", timeout: 120);
    }

    private static void CreateHelperProcs(string connectionString)
    {
        Console.WriteLine("  Creating helper procedures...");

        ExecuteNonQuery(connectionString, @"
            IF OBJECT_ID('dbo.ClearAllCaches', 'P') IS NOT NULL DROP PROCEDURE dbo.ClearAllCaches;");

        ExecuteNonQuery(connectionString, @"
            CREATE PROCEDURE dbo.ClearAllCaches
            AS
            BEGIN
                DBCC DROPCLEANBUFFERS WITH NO_INFOMSGS;
                DBCC FREEPROCCACHE WITH NO_INFOMSGS;
                DBCC FREESYSTEMCACHE('ALL') WITH NO_INFOMSGS;
            END;");
    }

    private static void ExecuteNonQuery(string connectionString, string sql, int timeout = 60)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeout };
        cmd.ExecuteNonQuery();
    }

    private static T ExecuteScalar<T>(string connectionString, string sql)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn);
        return (T)cmd.ExecuteScalar()!;
    }
}
