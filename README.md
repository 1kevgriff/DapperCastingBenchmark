# Dapper nvarchar Implicit Conversion Benchmark

Benchmarks proving the performance impact of Dapper's default behavior of sending C# `string` parameters as `nvarchar(4000)` when querying `varchar` columns in SQL Server.

**Read the full article:** [Dapper's nvarchar Implicit Conversion Performance Trap](https://consultwithgriff.com/dapper-nvarchar-implicit-conversion-performance-trap)

## The Problem

When you write this common Dapper pattern:

```csharp
connection.QueryFirstOrDefault<Product>(
    "SELECT * FROM Products WHERE ProductCode = @ProductCode",
    new { ProductCode = code });
```

Dapper sends `@ProductCode` as `nvarchar(4000)`. If `ProductCode` is a `varchar(100)` column, SQL Server must perform `CONVERT_IMPLICIT` on **every row** to compare types. This prevents index seeks and forces full index scans.

## The Fix

Use `DynamicParameters` with `DbType.AnsiString`:

```csharp
var parameters = new DynamicParameters();
parameters.Add("ProductCode", code, DbType.AnsiString, size: 100);
connection.QueryFirstOrDefault<Product>(sql, parameters);
```

## Results

### Wall-Time (1,000 queries each)

| Scenario | nvarchar (default) | varchar (fixed) | Difference |
|----------|-------------------|-----------------|------------|
| ProductCode (1M rows) | 23 sec | 0.1 sec | **176x slower** |
| OrderNumber (500K rows) | 35 sec | 0.1 sec | **268x slower** |

### BenchmarkDotNet (per query)

| Scenario | nvarchar (default) | varchar (fixed) | Ratio |
|----------|-------------------|-----------------|-------|
| ProductCode (1M rows) | 22,812 us | 158 us | **144x** |
| OrderNumber (500K rows) | 33,639 us | 152 us | **222x** |

### Execution Plans

| | nvarchar (Dapper default) | varchar (correct) |
|---|---|---|
| Plan Operator | Index Scan | Index Seek |
| CONVERT_IMPLICIT | Yes | No |
| Estimated Cost | 7.78 | 0.007 |

## Running the Benchmarks

### Prerequisites

- .NET 8 SDK
- SQL Server (local instance with trusted connection)

### Setup

```bash
# Create database and seed 1M products + 500K orders
dotnet run -c Release -- --setup
```

### Run

```bash
# Wall-time comparison (human-readable seconds)
dotnet run -c Release -- --walltime

# Execution plan + statistics diagnostics
dotnet run -c Release -- --diagnostics

# Full BenchmarkDotNet suite
dotnet run -c Release -- --filter '*ProductCode*'
dotnet run -c Release -- --filter '*OrderNumber*'
dotnet run -c Release -- --filter '*MultiRow*'
```

## Disclaimer

This benchmark application and its database setup were generated with the assistance of [Claude Code](https://claude.ai/claude-code) by Anthropic. The benchmark methodology uses [BenchmarkDotNet](https://benchmarkdotnet.org/) for statistical rigor. Results will vary based on hardware, SQL Server configuration, and data volume.
