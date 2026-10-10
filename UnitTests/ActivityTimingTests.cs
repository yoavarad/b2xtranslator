using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using b2xtranslator.DocFileFormat;
using b2xtranslator.OpenXmlLib.SpreadsheetML;
using b2xtranslator.OpenXmlLib.WordprocessingML;
using b2xtranslator.Spreadsheet.XlsFileFormat;
using b2xtranslator.StructuredStorage.Reader;
using b2xtranslator.Tools;
using NUnit.Framework;
using DocConverter = b2xtranslator.WordprocessingMLMapping.Converter;
using XlsConverter = b2xtranslator.SpreadsheetMLMapping.Converter;

namespace UnitTests
{
    [TestFixture]
    public class ActivityTimingTests
    {
        static readonly string[] Stages = { "open-storage", "parse", "map", "write" };

        [OneTimeSetUp]
        public void SetUp()
        {
            // Xls strings use legacy code pages (e.g. 1252).
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        static string FilePath(string name)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "b2xtranslator.sln")))
                dir = dir.Parent;
            Assert.IsNotNull(dir, "b2xtranslator.sln not found above " + AppContext.BaseDirectory);
            return Path.Combine(dir.FullName, "UnitTests", "files", name);
        }

        static Activity[] Record(Action convert)
        {
            var stopped = new ConcurrentQueue<Activity>();
            using (var listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == Instrumentation.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = stopped.Enqueue
            })
            {
                ActivitySource.AddActivityListener(listener);
                convert();
            }
            return stopped.ToArray();
        }

        static void AssertStages(Activity[] activities)
        {
            foreach (var stage in Stages)
            {
                var matching = activities.Where(a => a.OperationName == stage).ToArray();
                Assert.That(matching, Is.Not.Empty, "missing stage activity: " + stage);
                Assert.That(matching.All(a => a.Duration > TimeSpan.Zero), "zero duration for stage: " + stage);
            }
        }

        [Test]
        public void DocConversion_EmitsStageActivities()
        {
            var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".docx");
            try
            {
                var activities = Record(() =>
                {
                    using (var reader = new StructuredStorageReader(FilePath("simple.doc")))
                    {
                        var doc = new WordDocument(reader);
                        var outType = DocConverter.DetectOutputType(doc);
                        var docx = WordprocessingDocument.Create(DocConverter.GetConformFilename(output, outType), outType);
                        DocConverter.Convert(doc, docx);
                    }
                });
                AssertStages(activities);
            }
            finally
            {
                if (File.Exists(output)) File.Delete(output);
            }
        }

        [Test]
        public void XlsConversion_EmitsStageActivities()
        {
            var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
            try
            {
                var activities = Record(() =>
                {
                    using (var reader = new StructuredStorageReader(FilePath("simple.xls")))
                    {
                        var xls = new XlsDocument(reader);
                        var outType = XlsConverter.DetectOutputType(xls);
                        using (var xlsx = SpreadsheetDocument.Create(XlsConverter.GetConformFilename(output, outType), outType))
                        {
                            XlsConverter.Convert(xls, xlsx);
                        }
                    }
                });
                AssertStages(activities);
            }
            finally
            {
                if (File.Exists(output)) File.Delete(output);
            }
        }
    }
}
