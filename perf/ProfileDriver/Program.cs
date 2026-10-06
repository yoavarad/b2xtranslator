using b2xtranslator.DocFileFormat;
using b2xtranslator.WordprocessingMLMapping;
using b2xtranslator.SpreadsheetMLMapping;
using b2xtranslator.PresentationMLMapping;
using b2xtranslator.OpenXmlLib.PresentationML;
using b2xtranslator.OpenXmlLib.SpreadsheetML;
using b2xtranslator.OpenXmlLib.WordprocessingML;
using b2xtranslator.PptFileFormat;
using b2xtranslator.Spreadsheet.XlsFileFormat;
using b2xtranslator.StructuredStorage.Reader;
using b2xtranslator.Tools;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text;

// usage: ProfileDriver <doc|xls|ppt> <input> <iterations> [--alloc]
// Loops full conversions and prints per-stage times, CPU, GC, memory. --alloc samples allocations by type.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
TraceLogger.LogLevel = TraceLogger.LoggingLevel.None;
string format = args[0], input = args[1];
int iters = int.Parse(args[2]);
string output = Path.Combine(Path.GetTempPath(), "b2x-prof-" + Guid.NewGuid().ToString("N") + "." + format + "x");

var stages = new ConcurrentDictionary<string, (long ticks, int n)>();
using var al = new ActivityListener
{
    ShouldListenTo = s => s.Name == Instrumentation.SourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = a => stages.AddOrUpdate(a.OperationName, (a.Duration.Ticks, 1), (_, v) => (v.ticks + a.Duration.Ticks, v.n + 1)),
};
ActivitySource.AddActivityListener(al);
AllocListener? alloc = args.Length > 3 && args[3] == "--alloc" ? new AllocListener() : null;

void One()
{
    File.Delete(output);
    using var reader = new StructuredStorageReader(input);
    switch (format)
    {
        case "doc":
            var d = new WordDocument(reader);
            b2xtranslator.WordprocessingMLMapping.Converter.Convert(d, WordprocessingDocument.Create(output, b2xtranslator.WordprocessingMLMapping.Converter.DetectOutputType(d)));
            break;
        case "xls":
            var x = new XlsDocument(reader);
            using (var xs = SpreadsheetDocument.Create(output, b2xtranslator.SpreadsheetMLMapping.Converter.DetectOutputType(x)))
                b2xtranslator.SpreadsheetMLMapping.Converter.Convert(x, xs);
            break;
        default:
            var p = new PowerpointDocument(reader);
            b2xtranslator.PresentationMLMapping.Converter.Convert(p, PresentationDocument.Create(output, b2xtranslator.PresentationMLMapping.Converter.DetectOutputType(p)));
            break;
    }
}

One(); // warm-up
stages.Clear();
var proc = Process.GetCurrentProcess();
long a0 = GC.GetTotalAllocatedBytes(true);
int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
TimeSpan cpu0 = proc.TotalProcessorTime;
int maxThreads = 0;
var sw = Stopwatch.StartNew();
for (int i = 0; i < iters; i++)
{
    One();
    if ((i & 15) == 0) { proc.Refresh(); maxThreads = Math.Max(maxThreads, proc.Threads.Count); }
}
sw.Stop();
proc.Refresh();
double wall = sw.Elapsed.TotalMilliseconds, cpu = (proc.TotalProcessorTime - cpu0).TotalMilliseconds;
long allocd = GC.GetTotalAllocatedBytes(true) - a0;
var mi = GC.GetGCMemoryInfo();
Console.WriteLine($"format={format} iters={iters} wall_ms_per_iter={wall / iters:F2} cpu_ms_per_iter={cpu / iters:F2} cpu_util_cores={cpu / wall:F2}");
Console.WriteLine($"alloc_KB_per_iter={allocd / 1024.0 / iters:F0} alloc_MB_per_s={allocd / 1048576.0 / (wall / 1000):F0} gen0={GC.CollectionCount(0) - g0} gen1={GC.CollectionCount(1) - g1} gen2={GC.CollectionCount(2) - g2}");
Console.WriteLine($"peak_working_set_MB={proc.PeakWorkingSet64 / 1048576.0:F0} peak_private_MB={proc.PeakVirtualMemorySize64 / 1048576.0:F0} gc_heap_MB={GC.GetTotalMemory(false) / 1048576.0:F1} loh_size_MB={mi.GenerationInfo[3].SizeAfterBytes / 1048576.0:F1} max_threads={maxThreads} pause_pct={mi.PauseTimePercentage:F1}");
foreach (var kv in stages.OrderBy(k => k.Key))
    Console.WriteLine($"stage {kv.Key,-13} mean_ms={new TimeSpan(kv.Value.ticks).TotalMilliseconds / kv.Value.n:F3} n={kv.Value.n}");
File.Delete(output);
alloc?.Dump();

sealed class AllocListener : EventListener
{
    readonly ConcurrentDictionary<string, long> _bytes = new();
    long _loh;
    protected override void OnEventSourceCreated(EventSource s)
    {
        if (s.Name == "Microsoft-Windows-DotNETRuntime")
            EnableEvents(s, EventLevel.Verbose, (EventKeywords)0x1); // GC keyword: AllocationTick (~100KB sampled)
    }
    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        if (e.EventName?.StartsWith("GCAllocationTick") != true) return;
        string? type = null; long amount = 100_000; uint kind = 0;
        for (int i = 0; i < e.PayloadNames!.Count; i++)
            switch (e.PayloadNames[i])
            {
                case "TypeName": type = e.Payload![i] as string; break;
                case "AllocationAmount64": amount = Convert.ToInt64(e.Payload![i]); break;
                case "AllocationKind": kind = Convert.ToUInt32(e.Payload![i]); break;
            }
        if (kind == 1) Interlocked.Add(ref _loh, amount);
        _bytes.AddOrUpdate(type ?? "?", amount, (_, v) => v + amount);
    }
    public void Dump()
    {
        long tot = _bytes.Values.Sum();
        Console.WriteLine($"alloc_sampled_total_MB={tot / 1048576.0:F1} loh_sampled_MB={_loh / 1048576.0:F1}");
        foreach (var kv in _bytes.OrderByDescending(k => k.Value).Take(15))
            Console.WriteLine($"alloc {kv.Value * 100.0 / tot,5:F1}% {kv.Key}");
    }
}

