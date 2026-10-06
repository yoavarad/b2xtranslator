# Utilization and hotspot report

Profile of one full conversion (open storage, parse, map, write) per format on the
**large** corpus tier (`perf/corpus/large/large.{doc,xls,ppt}`, ~10 MB each). Taken 2026-10-07.

- Machine: Intel Core Ultra 7 155H, 22 logical cores, 31.5 GB RAM, Windows 11. .NET SDK 10.0.401, Release build, base commit `75d4329`.
- Tools: `perf/ProfileDriver` (loops the conversion, prints stage times from the `b2xtranslator` activities, CPU, GC and memory
  counters) and `dotnet-trace` (`dotnet-sampled-thread-time`, 3000 iterations per format), see [profiling.md](profiling.md).
- Evidence: `perf/traces/{doc,xls,ppt}.nettrace` (open in PerfView or Visual Studio, or run
  `dotnet-trace report <file> topN --inclusive`).

Reproduce:

```sh
python perf/corpus/generate.py
dotnet build -c Release perf/ProfileDriver
dotnet perf/ProfileDriver/bin/Release/net10.0/ProfileDriver.dll doc perf/corpus/large/large.doc 1000 [--alloc]
dotnet-trace collect --profile dotnet-sampled-thread-time -o doc.nettrace -- dotnet perf/ProfileDriver/bin/Release/net10.0/ProfileDriver.dll doc perf/corpus/large/large.doc 3000
```

## Important limitation

The large tier is the small file plus a ~10 MB `PerfPadding` stream the translators ignore (see
`perf/corpus/README.md`). Per-record work is that of the tiny small-tier document, repeated by the driver.
Per-record findings are valid as ratios, but memory peaks are NOT representative of a real large document.
Task #136 adds a content-scaled corpus.

## Utilization (1000 iterations, no allocation listener)

| Metric | doc | xls | ppt |
|---|---|---|---|
| Wall per conversion | 12.1 ms | 6.7 ms | 12.6 ms |
| CPU per conversion | 8.1 ms | 6.2 ms | 12.6 ms |
| CPU utilization (cores) | 0.67 | 0.92 | 1.00 |
| Peak working set | 66 MB | 59 MB | 68 MB |
| GC heap after run | 2.0 MB | 5.0 MB | 7.7 MB |
| Allocated per conversion | 1150 KB | 541 KB | 1216 KB |
| Allocation rate | 93 MB/s | 79 MB/s | 94 MB/s |
| Gen0 / Gen1 / Gen2 collections | 94 / 2 / 1 | 44 / 1 / 0 | 99 / 98 / 5 |
| GC pause share | 1.7% | 0.5% | 4.2% |
| LOH allocations | none seen | none seen | none seen |
| Max threads (process) | 8 | 8 | 8 |

CPU is at most one core: the converters are single-threaded (the extra threads are runtime, tiered JIT and finalizer threads).
Wall time exceeds CPU time for doc and xls because of file system waits on the output zip.

## Stage timing (mean ms per conversion)

| Stage | doc | xls | ppt |
|---|---|---|---|
| open-storage | 0.34 | 0.22 | 0.26 |
| parse | 1.38 | 1.36 | 2.18 |
| map | 3.87 | 0.74 | 5.11 |
| write | 4.11 | 2.66 | 3.61 |

`map` plus `write` is 66-85% of the time. Run-to-run noise is about 2x (a 200-iteration run gave 5.7 / 3.9 / 7.5 ms
per conversion), so read these as relative shares only.

## Allocation breakdown (sampled by type, `--alloc`)

- doc: `byte[]` 32%, dictionary `Entry` nodes 14% (+ `Entry[]` 8%), `XmlName`/`XmlDocument`/`XmlElement` ~17% (DOM-based mapping).
- xls: `byte[]` 38%, `string` 38%.
- ppt: `byte[]` 26%, `string` 21%, `Type[]` 2.8%, `ConstructorInfo[]` 1.6%, `Object[]` 2.2% (reflection overhead), `Record[]` 1.6%.

## Ranked CPU hotspots (inclusive share of samples)

| # | Hotspot | doc | xls | ppt | Evidence | Task |
|---|---|---|---|---|---|---|
| 1 | Zip write and file I/O: `OpenXmlPackage.Close` > `ZipArchive.WriteFile` > `OSFileStreamStrategy.Write` | 40% | 49% | 21% | all `.nettrace` | #107 |
| 2 | Reflection `ConstructorInfo.Invoke` in `Record.ReadRecord` (`Record.cs:273-287`) | 2.4% | n/a | 15-18% (+3.8% `DefaultBinder.SelectMethod`) | `ppt.nettrace` | #100 |
| 3 | Reflection `MethodInfo.Invoke` in `ShapeTreeMapping.DynamicApply` | n/a | n/a | 8.7-9% | `ppt.nettrace` | #106 |
| 4 | `DynamicResolver+DestroyScout.Finalize` (finalizer cost of reflection-emitted dynamic methods) | 0 | 0 | 30% (finalizer thread) | `ppt.nettrace` | #100, #106 |
| 5 | `VirtualStream.Read` / `VirtualStreamReader` ctor (per-sector seeks, `VirtualStream.cs:112-191`) | 2.7% | 3.2% (reader ctor 5.7%) | 4-5% (`StructuredStorageReader` ctor 13%) | all | #103 |
| 6 | `Monitor.Enter_Slowpath` lock contention in a single-threaded run | 10.5% | n/a | 6% | `doc.nettrace`, `ppt.nettrace` | #134 |
| 7 | `OpenXmlPartContainer.TargetDirectoryAbsolute` / `TargetFullName` | 1.9% | 2.5% | 1.3% | all | #107 |
| 8 | `MemoryStream.CopyTo` and capacity growth (whole-part buffering, `OpenXmlPart.cs:18`) | 2.6% | 2.6% + 1.5% | 2.8% | all | #107 |
| 9 | `CharacterPropertiesMapping.convertSprms` / `XmlElement.get_Attributes` (DOM attribute lookups) | 10.4% / 8.5% | n/a | n/a | `doc.nettrace` | #135 |
| 10 | `Array.Copy` / `List.AddWithResize` in `WorkbookExtractor.extractData` | n/a | 6.0% / 5.7% | n/a | `xls.nettrace` | #105 |

## Verdict on the survey's suspected hotspots

| Suspect | Verdict | Evidence |
|---|---|---|
| Reflection ctor Invoke per record (`Record.cs:273-287`) | **Confirmed for PPT** (15-18% inclusive, about 25% with binder and finalizer overhead); minor for doc (2.4%). | `ppt.nettrace`, `doc.nettrace` |
| Reflection ctor per record in XLS (`BiffRecord.cs:140-146`) | **Not visible** (below 0.1%, absent from the top 400). The corpus xls is tiny and uses few record types; re-check with #136. | `xls.nettrace` |
| Nested record body copies (`Record.cs:67-74`, `RegularContainer.cs`) | **Inconclusive**: `byte[]` is the top allocation type everywhere (26-38%), but no copy method ranks in CPU samples. Needs a deeply nested corpus (#136). | `--alloc` output |
| Per-sector seek in `VirtualStream.cs:112-191` | **Weakly confirmed**: 2.7-5% inclusive; understated because the padding stream is never read. | all traces |
| Whole-part MemoryStream buffering (`OpenXmlPart.cs:18`) | **Confirmed but small**: 2.6-4%. The dominant cost is the zip and file write itself (21-49%), not the buffering. | all traces |

Unexpected: zip write and file I/O dominate every format (#1), the dynamic-resolver finalizer dominates PPT samples (#4),
doc mapping pays heavily for `XmlDocument` attribute access (#9), and a `Monitor` lock shows 6-10% in a single-threaded run (#6).

## Re-prioritized story #94 tasks

| Order | Task | Reason |
|---|---|---|
| 1 | #107 stream OOXML parts to the zip, configurable compression | 21-49% of CPU in all formats |
| 2 | #100 cached factory delegates for records | 15-18% of PPT CPU plus finalizer cost |
| 3 | #106 replace reflection Apply dispatch in PPT | about 9% of PPT CPU, tied to #100 |
| 4 | #134 (new) lock contention in write path | 6-10% of doc/ppt CPU |
| 5 | #135 (new) Doc DOM attribute lookups | about 8-10% of doc CPU |
| 6 | #103 structured storage reader | 3-13%, understated by the corpus |
| 7 | #105 XLS extraction | about 6% |
| 8 | #102 record body copies | unproven; defer until #136 provides a nested corpus |
| 9 | #101, #104 | no evidence in these profiles |

New tasks filed from this report: #134 (lock contention), #135 (Doc DOM attribute lookups), #136 (content-scaled corpus).
