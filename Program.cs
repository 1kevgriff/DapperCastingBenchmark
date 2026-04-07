using BenchmarkDotNet.Running;
using DapperBenchmarkCasting.Benchmarks;
using DapperBenchmarkCasting.Helpers;
using DapperBenchmarkCasting.Setup;

const string connectionString = "Server=.;Database=DapperBenchmarkCasting;Trusted_Connection=true;TrustServerCertificate=true";

if (args.Length > 0 && args[0] == "--setup")
{
    DatabaseSetup.EnsureDatabaseSeeded(connectionString);
    return;
}

if (args.Length > 0 && args[0] == "--diagnostics")
{
    SqlStatisticsCollector.RunDiagnostics(connectionString);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
