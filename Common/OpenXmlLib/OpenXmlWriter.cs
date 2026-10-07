using System;
using System.Text;
using System.Xml;
using System.IO;
using System.IO.Compression; // Replaces using b2xtranslator.ZipUtils;

namespace b2xtranslator.OpenXmlLib
{
    public sealed class OpenXmlWriter : IDisposable
    {
        /// <summary>Hold the settings required in Open XML ZIP files.</summary>
        readonly static XmlWriterSettings xmlWriterSettings = new XmlWriterSettings
        {
            OmitXmlDeclaration = false,
            CloseOutput = false,
            Encoding = Encoding.UTF8,
            Indent = true,
            ConformanceLevel = ConformanceLevel.Document
        };

        /// <summary>Hold the XML writer to populate the current ZIP entry.</summary>
        XmlWriter xmlEntryWriter;

        /// <summary>Holds the XML of the current ZIP entry until the entry is closed.</summary>
        MemoryStream xmlEntryBuffer;

        /// <summary>Hold an optional file output stream, only populated if opened on a file.</summary>
        FileStream fileOutputStream;

        /// <summary>Holds the ZIP archive the XML is being written to.</summary>
        ZipArchive outputArchive;

        /// <summary>Holds the current ZIP entry, created by <see cref="AddPart"/>.</summary>
        ZipArchiveEntry currentEntry;

        /// <summary>Holds the open stream to write to <see cref="currentEntry"/></summary>
        Stream entryStream;


        /// <summary>Compression level for every ZIP entry.</summary>
        readonly CompressionLevel compressionLevel;

        /// <summary>Settings for the XML written through this writer (rels, [Content_Types].xml).</summary>
        readonly XmlWriterSettings entryWriterSettings;

        public OpenXmlWriter()
            : this(CompressionLevel.Optimal, true)
        {
        }

        public OpenXmlWriter(CompressionLevel compressionLevel, bool indent)
        {
            this.compressionLevel = compressionLevel;
            this.entryWriterSettings = xmlWriterSettings.Clone();
            this.entryWriterSettings.Indent = indent;
        }

        public void Dispose() => this.Close();

        // ZipArchiveMode.Create streams each entry straight to the output; Update mode would
        // buffer every entry's uncompressed bytes in memory until the archive is disposed.
        public void Open(string fileName)
        {
            this.Close();
            this.fileOutputStream = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
            this.outputArchive = new ZipArchive(this.fileOutputStream, ZipArchiveMode.Create);
        }

        /// <summary>Writes a new package from <paramref name="output"/>'s current position; pass an empty stream.</summary>
        public void Open(Stream output)
        {
            this.Close();
            this.outputArchive = new ZipArchive(output, ZipArchiveMode.Create);
        }

        public void Close()
        {
            // close streams
            if (this.xmlEntryWriter != null)
            {
                this.xmlEntryWriter.Close();
                this.xmlEntryWriter = null;
                this.xmlEntryBuffer.WriteTo(this.entryStream);
                this.xmlEntryBuffer = null;
            }

            if (this.entryStream != null)
            {
                this.entryStream.Close();
                this.entryStream = null;
            }

            this.currentEntry = null;

            if (this.outputArchive != null)
            {
                this.outputArchive.Dispose();
                this.outputArchive = null;
            }

            if (this.fileOutputStream != null)
            {
                this.fileOutputStream.Close();
                this.fileOutputStream.Dispose();
                this.fileOutputStream = null;
            }
        }

        public void AddPart(string fullName)
        {
            if (this.xmlEntryWriter != null)
            {
                this.xmlEntryWriter.Close();
                this.xmlEntryWriter = null;
                this.xmlEntryBuffer.WriteTo(this.entryStream);
                this.xmlEntryBuffer = null;
            }

            if (this.entryStream != null)
            {
                this.entryStream.Close();
                this.entryStream = null;
            }

            // the path separator in the package should be a forward slash
            // Optimal goes through the parameterless overload so default output keeps today's entry headers.
            string entryName = fullName.Replace('\\', '/');
            this.currentEntry = this.compressionLevel == CompressionLevel.Optimal
                ? this.outputArchive.CreateEntry(entryName)
                : this.outputArchive.CreateEntry(entryName, this.compressionLevel);

            // Create the stream for the current entry
            this.entryStream = this.currentEntry.Open();
        }

        /// <summary>
        /// Get or create an XML writer for the current ZIP entry. It writes to <see cref="xmlEntryBuffer"/>
        /// (copied to the entry when the entry is closed) because XmlWriter.Close flushes its stream, and
        /// flushing the entry's deflate stream would add sync-flush bytes to the compressed output.
        /// </summary>
        XmlWriter XmlWriter =>
            this.xmlEntryWriter ?? (this.xmlEntryWriter = XmlWriter.Create(this.xmlEntryBuffer = new MemoryStream(), this.entryWriterSettings));

        public void WriteRawBytes(byte[] buffer, int index, int count) =>
            this.entryStream.Write(buffer, index, count);

        public void Write(Stream stream) =>
            stream.CopyTo(this.entryStream);

        public void WriteStartElement(string prefix, string localName, string ns) =>
            this.XmlWriter.WriteStartElement(prefix, localName, ns);

        public void WriteStartElement(string localName, string ns) =>
            this.XmlWriter.WriteStartElement(localName, ns);

        public void WriteEndElement() =>
            this.XmlWriter.WriteEndElement();

        public void WriteStartAttribute(string prefix, string localName, string ns) =>
            this.XmlWriter.WriteStartAttribute(prefix, localName, ns);

        public void WriteAttributeValue(string prefix, string localName, string ns, string value) =>
            this.XmlWriter.WriteAttributeString(prefix, localName, ns, value);

        public void WriteAttributeString(string localName, string value) =>
            this.XmlWriter.WriteAttributeString(localName, value);

        public void WriteEndAttribute() =>
            this.XmlWriter.WriteEndAttribute();

        public void WriteString(string text) =>
            this.XmlWriter.WriteString(text);

        public void WriteFullEndElement() =>
            this.XmlWriter.WriteFullEndElement();

        public void WriteCData(string s) =>
            this.XmlWriter.WriteCData(s);

        public void WriteComment(string s) =>
            this.XmlWriter.WriteComment(s);

        public void WriteProcessingInstruction(string name, string text) =>
            this.XmlWriter.WriteProcessingInstruction(name, text);

        public void WriteEntityRef(string name) =>
            this.XmlWriter.WriteEntityRef(name);

        public void WriteCharEntity(char c) =>
            this.XmlWriter.WriteCharEntity(c);

        public void WriteWhitespace(string s) =>
            this.XmlWriter.WriteWhitespace(s);

        public void WriteSurrogateCharEntity(char lowChar, char highChar) =>
            this.XmlWriter.WriteSurrogateCharEntity(lowChar, highChar);

        public void WriteChars(char[] buffer, int index, int count) =>
            this.XmlWriter.WriteChars(buffer, index, count);

        public void WriteRaw(char[] buffer, int index, int count) =>
            this.XmlWriter.WriteRaw(buffer, index, count);

        public void WriteRaw(string data) =>
            this.XmlWriter.WriteRaw(data);

        public void WriteBase64(byte[] buffer, int index, int count) =>
            this.XmlWriter.WriteBase64(buffer, index, count);

        public WriteState WriteState =>
            this.XmlWriter.WriteState;

        public void Flush() =>
            this.XmlWriter.Flush();

        public string LookupPrefix(string ns) =>
            this.XmlWriter.LookupPrefix(ns);

        public void WriteDocType(string name, string pubid, string sysid, string subset) =>
            throw new NotImplementedException();

        public void WriteEndDocument() =>
            this.XmlWriter.WriteEndDocument();

        public void WriteStartDocument(bool standalone) =>
            this.XmlWriter.WriteStartDocument(standalone);

        public void WriteStartDocument() =>
            this.XmlWriter.WriteStartDocument();
    }
}