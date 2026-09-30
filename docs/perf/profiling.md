# Profiling conversions

The converters emit one `System.Diagnostics.Activity` per pipeline stage from a
single `ActivitySource` named `b2xtranslator` (`b2xtranslator.Tools.Instrumentation`).
Nothing is recorded unless a listener subscribes, so the default cost is a null check.

| Activity       | Where                                                        | Tag              |
|----------------|--------------------------------------------------------------|------------------|
| `open-storage` | `StructuredStorageReader` constructor                        | -                |
| `parse`        | `WordDocument` / `XlsDocument` / `PowerpointDocument` ctor   | `b2x.format`     |
| `map`          | `Converter.Convert` (Doc, Xls, Ppt mapping)                  | `b2x.format`     |
| `write`        | `OpenXmlPackage.Close` (serialize/zip the OOXML package)     | -                |

Stages are coarse on purpose: there are no per-record spans.

## Install the tools

```sh
dotnet tool install --global dotnet-counters
dotnet tool install --global dotnet-trace
```

Build the Shell tools first (`dotnet build -c Release Shell/doc2x`), then launch
them through the diagnostic tool with `--` so short conversions are captured from
process start. The same works for `xls2x` and `ppt2x`.

## Runtime counters: dotnet-counters

```sh
dotnet-counters monitor \
  --counters "System.Runtime[cpu-usage,gc-heap-size,alloc-rate,threadpool-thread-count]" \
  -- dotnet Shell/doc2x/bin/Release/net10.0/doc2x.dll input.doc -o out.docx
```

- `cpu-usage` - process CPU %.
- `gc-heap-size` - managed heap in MB; growth across a batch hints at retention.
- `alloc-rate` - bytes allocated per interval; the main driver of GC cost.
- `threadpool-thread-count` - should stay flat; the converters are single-threaded.

Use `dotnet-counters collect --format csv -o counters.csv ...` with the same
arguments to save the series instead of watching it live.

## Traces and stage activities: dotnet-trace

```sh
dotnet-trace collect \
  --providers 'Microsoft-Diagnostics-DiagnosticSource:0x2:5:FilterAndPayloadSpecs="[AS]b2xtranslator/*"' \
  --profile cpu-sampling \
  -o doc2x.nettrace \
  -- dotnet Shell/doc2x/bin/Release/net10.0/doc2x.dll input.doc -o out.docx
```

The `[AS]b2xtranslator/*` spec tells the runtime's DiagnosticSource bridge to
listen to every activity from the `b2xtranslator` source. Each stage shows up as
an `Activity1Start` / `Activity1Stop` event pair (nested stages use `Activity2*`)
whose `EventName` is `<stage>/Start` or `<stage>/Stop`; the time between a pair
is the stage duration. On Windows `cmd`, use outer double quotes and escape the
inner ones (`\"`).

## Viewing the activities

- **PerfView** (Windows): open `doc2x.nettrace`, then *Events* and filter on
  `Microsoft-Diagnostics-DiagnosticSource/Activity1` (Start/Stop). The CPU stacks view shows
  where time goes inside each stage.
- **Speedscope**: `dotnet-trace convert --format speedscope doc2x.nettrace` and
  load the output at https://www.speedscope.app for the CPU samples.
- **In process**: register an `ActivityListener` whose `ShouldListenTo` matches
  `Instrumentation.SourceName` and read `Activity.OperationName` / `Duration`
  in `ActivityStopped`. `UnitTests/ActivityTimingTests.cs` is a minimal example.
  An OpenTelemetry SDK can subscribe the same way with `AddSource("b2xtranslator")`.
