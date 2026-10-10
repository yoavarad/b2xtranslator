# Binary(doc,xls,ppt) to OpenXMLTranslator

.NET library to convert Microsoft Office binary files (`doc`, `xls` and `ppt`) to Open XML (`docx`, `xlsx` and `pptx`).
You can use the [Open XML SDK](https://github.com/OfficeDev/Open-XML-SDK) to mainpulate those.

Forked from a [.NET 2 Mono implementation](https://sourceforge.net/projects/b2xtranslator/) under the BSD license. 

* [Microsoft Office binary files documentation](https://msdn.microsoft.com/en-us/library/cc313105.aspx)
* [Open XML Standard](http://www.ecma-international.org/publications/standards/Ecma-376.htm)
* [Microsoft article on this implementation](https://blogs.msdn.microsoft.com/interoperability/2009/05/11/binary-to-open-xml-b2x-translator-interoperability-for-the-office-binary-file-formats/)
* [.NET 2 Mono implementation architecture](http://b2xtranslator.sourceforge.net/architecture.html)

All code retained from that version ©2009 DI<sup><u>a</u></sup>LOGIK<sup><u>a</u></sup> http://www.dialogika.de/  
.NET core port work and move to `System.IO.Compression` ©2017 Evolution https://www.evolutionjobs.com/

Licensed under the 3-clause BSD license, see [LICENSE](LICENSE).

## Build and test

Requires the .NET 10 SDK (see `global.json`). Libraries (Common, Doc, Xls, Ppt) target `net8.0` and `net10.0`; `UnitTests`, the Shell tools and `Benchmarks` target `net10.0`.

```
dotnet build b2xtranslator.sln
dotnet test UnitTests
```

CI builds with `-p:CiWarningsAsErrors=true` (compiler warnings fail the build) and restores with `--locked-mode` against the committed `packages.lock.json` files.

Some Doc tests need fixture files or Word interop and may be skipped or fail on machines without them.

## Usage

Command line (see [Shell](Shell/README.md)):

```
dotnet run --project Shell/doc2x -- <input> [-o <output>]
```

`xls2x` and `ppt2x` work the same way. From code, open the OLE storage, parse, then map to a package:

```csharp
using var reader = new StructuredStorageReader("in.doc");
var doc = new WordDocument(reader);
var docx = WordprocessingDocument.Create("out.docx", WordprocessingMLMapping.Converter.DetectOutputType(doc));
WordprocessingMLMapping.Converter.Convert(doc, docx); // disposes docx
```

Xls (`XlsDocument` / `SpreadsheetDocument`) and Ppt (`PowerpointDocument` / `PresentationDocument`) follow the same pattern; see `Benchmarks/ConversionBenchmarks.cs`.

## Benchmarks

`Benchmarks/` is a BenchmarkDotNet project (not run by `dotnet test`):

```
python perf/corpus/generate.py                                           # once: creates the large-tier inputs
dotnet run -c Release --project Benchmarks -- --filter "*" --job short
```

Baseline numbers and profiling notes are in [docs/perf](docs/perf/baseline.md). A non-gating benchmark workflow runs weekly and on demand.

## Layout

Per-module READMEs: [Common](Common/README.md), [Doc](Doc/README.md), [Xls](Xls/README.md), [Ppt](Ppt/README.md), [Shell](Shell/README.md).

Each format follows the same pipeline: a `*FileFormat` folder parses the binary records, a `*Mapping` folder writes them to an OOXML package via `Common/OpenXmlLib`.

Other folders: `Benchmarks/` (BenchmarkDotNet), `perf/` (corpus generator, profiling driver), `docs/` (ADRs, perf reports, stub inventory, project rules).

## Contributing

Keep changes small and focused, add or update a test in `UnitTests` for behavior changes, and see [CLAUDE.md](CLAUDE.md) and [docs/project-rules.md](docs/project-rules.md) for project rules.

## History

### Upstream port (2017-2018, Keith Henry)

Port of the original DIaLOGIKa b2xtranslator (.NET 2 / Mono, SourceForge) to .NET Core on GitHub:

- Standardised licensing on 3-clause BSD across all contributors.
- Ported to .NET Core: compile fixes, removed Windows-desktop-only code and BiffView, simplified namespaces and folder layout.
- Replaced the ZipLib dependency and native zip code with `System.IO.Compression`; removed the `OpenXmlWriter` base class.
- Merged the core projects into one library, and each format translator into a single DLL depending only on the core.
- Moved tests to NUnit; fixed `StringTable` reading past EOF; used ISO-8859-1 where Windows-1252 was unavailable on .NET Core.
- Added a code of conduct; fixed the stsh test (Alexander Simakhin).

### Changes in this fork (2026, yoavarad)

Platform, build and CI
- Retargeted Common, Xls, Ppt and Doc to `net8.0` (#9, #11), then multi-targeted the libraries to `net8.0;net10.0`, moved Shell tools and tests from `netcoreapp2.0` to `net10.0`, and pinned the .NET 10 SDK in `global.json`.
- Fixed OLE directory/stream name truncation under .NET Core globalization (#3) and part path resolution on non-Windows platforms.
- GitHub Actions: build and test on Ubuntu and Windows, `dotnet format` and vulnerable-package checks, NuGet lock files with locked-mode restore and caching, PR legs build `net10.0` only while `main` builds all target frameworks (#127).
- Resolved all compiler warnings with identical behavior and made CI treat warnings as errors (`CiWarningsAsErrors`); deterministic CI builds; removed dead `key.snk` references.
- Conventions checks for branch names, commit messages and PR bodies; automatic deletion of merged PR branches.

Doc
- Fixed surrogate-pair writing in `DocumentMapping` (#11).
- Guarded the table grid builder against a no-progress hang.
- Mapped the remaining DOP compatibility options to `w:compat`.

Xls
- Corrected `Row` record flag bit positions (customHeight / autofit).

Ppt
- Valid PresentationML for slide transitions; correct DrawingML scheme color names; spacing and callout adjust values derived from named constants; adjust values for common preset shapes beyond roundRect, with an overflow guard.
- Vertical placeholders (including `orient="vert"`) and the `vertTitleAndTx` / `vertTitleAndTxOverChart` layouts (#120); generic fallback layout for unmapped slide layout types.
- `clrMapOvr` on slides, `p:bg` on notes pages, `p:otherStyle` in master text styles, `buSzPts` for point bullet sizes (ParagraphRun layout checked against MS-PPT).
- Fixed an `ObjectDisposedException` by leaving the caller's stream open when disposing `ZipReader`; the metroBlob zip is now opened from memory instead of a temp file (#150).

Robustness and storage
- Parsers hardened against malformed input: CFB sector-chain / DiFat cycle detection and bounds checks, Ppt UserEditAtom cycle detection, parse faults wrapped in library exceptions, plus seeded truncation/corruption fuzz tests (#92).
- No more silently swallowed exceptions: broad catches log via `TraceLogger`, enforced by a test (#32).
- Exception-driven control flow replaced by Try-pattern lookups (e.g. `StructuredStorageReader.TryGetStream`).
- Windowed Escher `RawData` is always copied so it never aliases the parent buffer.

Performance and benchmarks
- Reproducible doc/xls/ppt perf corpus with a SHA256 manifest, later content-scaled (paragraphs, cells, slides).
- BenchmarkDotNet project with baseline results, a weekly non-gating benchmark workflow, and `ActivitySource` stage timing (open, parse, map, write) for profiling (#114).
- Hot-path work: cached constructor delegates instead of per-record reflection; Plex/StringTable reflection hoisting and binary search; fewer allocations and seeks in the structured storage reader; Escher child records parsed from a window over the parent buffer; type-switch dispatch in `ShapeTreeMapping`; leaner Xls `FormulaMapping` / `SupBook`; hoisted attribute lookups and `HasAttributes` checks in Doc property mappings (#148).
- Zip entries are streamed (`ZipArchiveMode.Create`), with configurable `CompressionLevel` and `Indent` on `OpenXmlPackage`.

Docs and tooling
- Per-module READMEs, repo `CLAUDE.md` and project rules, Doc/Ppt stub inventory, ADR on unmapped slide layouts.
- Perf docs: baseline, profiling how-to, utilization and hotspot report; ADR 0001 on parallel mapping of slides/sheets (no-go within a file; parallelize callers instead).
- graphify knowledge graph of the repo (#2); ydk workflow config, hooks and GitHub templates.
- Tests made cross-platform; Word-comparison tests skipped when Office interop is unavailable.
