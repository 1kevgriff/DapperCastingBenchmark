using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;

namespace DapperBenchmarkCasting.Config;

public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumn(RankColumn.Arabic);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(HtmlExporter.Default);
        WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));
        WithOption(ConfigOptions.JoinSummary, true);
    }
}
