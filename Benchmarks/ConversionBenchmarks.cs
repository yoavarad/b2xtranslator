using BenchmarkDotNet.Attributes;
using b2xtranslator.DocFileFormat;
using b2xtranslator.OpenXmlLib.PresentationML;
using b2xtranslator.OpenXmlLib.SpreadsheetML;
using b2xtranslator.OpenXmlLib.WordprocessingML;
using b2xtranslator.PptFileFormat;
using b2xtranslator.Spreadsheet.XlsFileFormat;
using b2xtranslator.StructuredStorage.Reader;
using b2xtranslator.Tools;
using System;
using System.IO;
using System.Text;

namespace b2xtranslator.Benchmarks
{
    /// <summary>
    /// Full conversion (open storage, parse, map, write the OOXML package) of one perf corpus
    /// file, exactly as the Shell tools doc2x / xls2x / ppt2x do it. The package API only writes
    /// to a path, so the output goes to a temp file that is deleted before each run.
    /// </summary>
    [MemoryDiagnoser]
    public class ConversionBenchmarks
    {
        private string _input = string.Empty;
        private string _output = string.Empty;

        [Params("doc", "xls", "ppt")]
        public string Format { get; set; } = string.Empty;

        [Params("small", "large")]
        public string Tier { get; set; } = string.Empty;

        [GlobalSetup]
        public void Setup()
        {
            // xls parsing needs code page 1252; register it as UnitTests does (the xls2x tool does not, so it fails today).
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            TraceLogger.LogLevel = TraceLogger.LoggingLevel.None;
            _input = Corpus.PathFor(Format, Tier);
            _output = Path.Combine(Path.GetTempPath(), $"b2x-bench-{Format}-{Tier}-{Guid.NewGuid():N}.{Format}x");
        }

        [GlobalCleanup]
        public void Cleanup() => File.Delete(_output);

        [Benchmark]
        public void Convert()
        {
            File.Delete(_output);
            using var reader = new StructuredStorageReader(_input);
            switch (Format)
            {
                case "doc":
                    {
                        var doc = new WordDocument(reader);
                        var docx = WordprocessingDocument.Create(_output, WordprocessingMLMapping.Converter.DetectOutputType(doc));
                        WordprocessingMLMapping.Converter.Convert(doc, docx); // disposes docx
                        break;
                    }
                case "xls":
                    {
                        var xls = new XlsDocument(reader);
                        using var xlsx = SpreadsheetDocument.Create(_output, SpreadsheetMLMapping.Converter.DetectOutputType(xls));
                        SpreadsheetMLMapping.Converter.Convert(xls, xlsx);
                        break;
                    }
                case "ppt":
                    {
                        var ppt = new PowerpointDocument(reader);
                        var pptx = PresentationDocument.Create(_output, PresentationMLMapping.Converter.DetectOutputType(ppt));
                        PresentationMLMapping.Converter.Convert(ppt, pptx); // disposes pptx
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(Format), Format, "Unknown format.");
            }
        }
    }
}
