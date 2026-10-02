# Performance baseline

Reference numbers for the converters, produced by the `Benchmarks/` project
(BenchmarkDotNet). Later performance work compares against this table.

## What is measured

`ConversionBenchmarks.Convert` does one full conversion of a perf corpus file
(`perf/corpus`, see its README), the same way the Shell tools doc2x / xls2x / ppt2x do:
open the OLE storage, parse, map, and write the OOXML package. `[MemoryDiagnoser]` adds
Gen0/Gen1/Gen2 (collections per 1000 operations) and allocated bytes per operation.

Parameters: `Format` (`doc`, `xls`, `ppt`) x `Tier` (`small`, `large`).

Notes:

- The package API only writes to a path, so the output goes to a temp file. The
  benchmark deletes it at the start of each operation, which adds a small constant cost.
- The harness registers `CodePagesEncodingProvider` (as `UnitTests` do). Without it, xls
  parsing fails on code page 1252. The `xls2x` tool does not register it, which is why
  xls2x fails on the corpus today.
- The large tier is the small file plus a ~10 MB `PerfPadding` stream that the translators
  ignore (see `perf/corpus/README.md`). It measures OLE container handling at scale, not
  document content scaling. That is why small and large are close.

## Running

The project is part of `b2xtranslator.sln` but is not a test project, so `dotnet test`
never runs it.

```sh
python perf/corpus/generate.py                     # once: creates perf/corpus/large/*
dotnet run -c Release --project Benchmarks         # all benchmarks (default job)
dotnet run -c Release --project Benchmarks -- --filter "*" --job short   # quick smoke run
dotnet run -c Release --project Benchmarks -- --filter "*" --job dry     # one pass, sanity only
```

Results are written to `BenchmarkDotNet.Artifacts/` (git-ignored).

## Baseline (2026-10-02)

- Commit: `067ae57e508ff317370afcacb967967dcacacecc` (origin/main), plus the `Benchmarks/` project. Library code was unchanged.
- Machine: Intel Core Ultra 7 155H 1.40GHz, 1 CPU, 22 logical and 16 physical cores, 31.5 GB RAM
- OS: Windows 11 Home (10.0.26200.9457, 25H2)
- SDK: .NET SDK 10.0.401
- Runtime: .NET 10.0.12 (10.0.1226.42308), X64 RyuJIT x86-64-v3
- Tool: BenchmarkDotNet v0.15.8, DefaultJob

| Method  | Format | Tier  | Mean     | Error     | StdDev    | Median   | Gen0    | Gen1    | Gen2    | Allocated  |
|-------- |------- |------ |---------:|----------:|----------:|---------:|--------:|--------:|--------:|-----------:|
| Convert | doc    | large | 2.260 ms | 0.0552 ms | 0.1611 ms | 2.259 ms | 93.7500 | 39.0625 | 15.6250 | 1151.78 KB |
| Convert | doc    | small | 2.209 ms | 0.0476 ms | 0.1397 ms | 2.175 ms | 89.8438 | 42.9688 |       - | 1140.98 KB |
| Convert | ppt    | large | 3.326 ms | 0.0629 ms | 0.1556 ms | 3.300 ms | 93.7500 | 85.9375 |       - | 1221.46 KB |
| Convert | ppt    | small | 3.340 ms | 0.0766 ms | 0.2098 ms | 3.308 ms | 93.7500 | 85.9375 |       - | 1211.64 KB |
| Convert | xls    | large | 1.346 ms | 0.0401 ms | 0.1149 ms | 1.303 ms | 42.9688 |  3.9063 |       - |  541.97 KB |
| Convert | xls    | small | 1.394 ms | 0.0487 ms | 0.1389 ms | 1.360 ms | 42.9688 |       - |       - |  532.48 KB |

Gen0/Gen1/Gen2 are GC collections per 1000 operations. "-" means none were observed.
