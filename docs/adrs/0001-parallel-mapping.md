# ADR 0001: Parallel mapping of independent parts (slides / sheets)

Status: proposed (spike, task #108). No production code changed.

## Question
Can PPT slides, XLS worksheets (maybe DOC headers/footers) be mapped concurrently inside one conversion,
and is it better than callers converting many files in parallel?

## Findings

### Thread safety (static review)
- `TraceLogger` (`Common/Tools/TraceLogger.cs`): static `_logLevel` / `EnableTimeStamp` fields are
  plain (non-volatile) but only written at startup; `WriteLine` goes to `System.Diagnostics.Trace`, which
  is thread-safe (its listeners lock; this is the `Monitor.Enter_Slowpath` already seen in `docs/perf/utilization.md`).
  Safe to call concurrently, but output interleaves.
- PPT: `SlideMapping` ctor calls `ctx.Pptx.PresentationPart.AddSlidePart()` (mutates the shared package part
  tree). `ConversionContext` holds unsynchronised shared `Dictionary`s mutated during slide mapping:
  `AddedImages`, `MasterIdToLayoutManager` (`GetOrCreateLayoutManagerByMasterId`), `InstanceIdToLayoutPart`,
  `LayoutFilenameToLayoutPart`, `CodeToLayoutPart`, `TitleMasterIdToLayoutPart` (get-or-create of layout parts,
  numbered file names). Notes mapping then looks up `SlideMappings` (needs slides done first).
  Slide/part numbering and relationship ids are allocation-order dependent: parallel mapping makes output
  non-deterministic unless ids are pre-assigned in slide order.
- XLS: `WorkbookMapping` loops `BoundSheet8`s calling `AddWorksheetPart()` and `sheet.Convert(new WorksheetMapping(sharedXlsContext, part))`.
  The shared `ExcelContext` (shared strings table, styles, sheet records read from one BIFF stream with
  a shared `IStreamReader` position) is mutated by each sheet; shared strings indices are first-come order.
- Parsed records read through one `VirtualStream`/`StructuredStorageReader` (single seek position, not thread-safe).
- Package writer (`OpenXmlPackage.Close`) writes zip entries sequentially; a ZipArchive is not thread-safe.
  Only the in-memory XML generation (map) could run in parallel; writing stays serial.

### Upper bound (Amdahl)
From `docs/perf/utilization.md` stage means (ms): ppt map 5.11 of ~12.6 total (40%), xls map 0.74 of ~5 (15%),
doc map 3.87 of ~9.7. Write (zip I/O) and parse remain serial. With P workers and k independent parts the ideal
speedup is 1/((1-m)+m/P): ppt m=0.40 -> max 1.6x at P=4, 1.7x at P=8; xls m=0.15 -> <= 1.15x. And PPT slide
mapping also depends on shared layout/master state above, so realistic gain is lower. The perf corpus
has 1 slide / 1 sheet, so a per-part prototype cannot show speedup on it (needs the content-scaled corpus, task #136).
A prototype was NOT built: the needed locking/pre-assignment is the real work, and the bound is small.

### Measured: caller-level parallelism (separate conversions)
`perf/ProfileDriver`, 1500 conversions per process, small corpus, 22-logical-core laptop (hybrid CPU), concurrent processes:

| format | 1 proc | 4 procs | 8 procs | throughput vs 1 proc (4 / 8) |
|---|---|---|---|---|
| ppt | 10.1 s | 16.3 s | 19.5 s | 2.5x / 4.1x |
| xls | 6.5 s | 10.6 s | 17.2 s | 2.5x / 3.0x |
| doc | 9.5 s | 10.5 s | 15.1 s | 3.6x / 5.1x |

(throughput = N x 1500 conversions / wall.) Single-run, noisy (~2x run-to-run per utilization.md); scaling is
sub-linear mostly from shared disk/zip I/O and hybrid cores, but far exceeds the 1.1-1.7x intra-file bound.

## Decision: NO-GO for intra-file parallel mapping
- Gain bounded to ~1.6x (ppt) / ~1.15x (xls), only for files with many slides/sheets.
- Cost: lock or redesign shared `ConversionContext`/`ExcelContext`, deterministic id/part numbering, per-thread
  readers over the shared stream, serial writer; high regression risk in a record-heavy mapper codebase.
- Better ROI: reduce serial costs first (zip write #107, reflection #100/#106, lock contention #134).

## Recommendation: parallelise at the caller
Each conversion is independent: it creates its own `StructuredStorageReader`, context, and output package. The only
process-wide static state is `TraceLogger.LogLevel` / `EnableTimeStamp` (set once before starting workers) and the
`CodePagesEncodingProvider` registration. Callers (e.g. ole-extractor) should use `Parallel.ForEach` /
`Channel` workers over files with `MaxDegreeOfParallelism ~ physical cores`, a distinct output path per file,
`TraceLogger.LogLevel` set once up front, and no shared reader/writer instances.

## Follow-ups (only if re-evaluated)
- Revisit intra-file parallelism after #136 (content-scaled corpus) if profiles show map > 50% on many-slide decks.
- Optional: add a small doc/test asserting concurrent conversions of separate files produce identical output.
