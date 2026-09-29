# Binary(doc,xls,ppt) to OpenXMLTranslator

.NET Core library to convert Microsoft Office binary files (`doc`, `xls` and `ppt`) to Open XML (`docx`, `xlsx` and `pptx`).
You can use the [Open XML SDK](https://github.com/OfficeDev/Open-XML-SDK) to mainpulate those.

Forked from a [.NET 2 Mono implementation](https://sourceforge.net/projects/b2xtranslator/) under the BSD license. 

* [Microsoft Office binary files documentation](https://msdn.microsoft.com/en-us/library/cc313105.aspx)
* [Open XML Standard](http://www.ecma-international.org/publications/standards/Ecma-376.htm)
* [Microsoft article on this implementation](https://blogs.msdn.microsoft.com/interoperability/2009/05/11/binary-to-open-xml-b2x-translator-interoperability-for-the-office-binary-file-formats/)
* [.NET 2 Mono implementation architecture](http://b2xtranslator.sourceforge.net/architecture.html)

All code retained from that version ©2009 DI<sup><u>a</u></sup>LOGIK<sup><u>a</u></sup> http://www.dialogika.de/  
.NET core port work and move to `System.IO.Compression` ©2017 Evolution https://www.evolutionjobs.com/

## Build and test

Requires the .NET 10 SDK (see `global.json`). Libraries target `net8.0` and `net10.0`; `UnitTests` targets `net10.0`.

```
dotnet build b2xtranslator.sln
dotnet test UnitTests
```

Some Doc tests need fixture files or Word interop and may be skipped or fail on machines without them.

## Layout

Per-module READMEs: [Common](Common/README.md), [Doc](Doc/README.md), [Xls](Xls/README.md), [Ppt](Ppt/README.md), [Shell](Shell/README.md).

Each format follows the same pipeline: a `*FileFormat` project parses the binary records, a `*Mapping` project writes them to an OOXML package via `Common/OpenXmlLib`.

## Contributing

Keep changes small and focused, add or update a test in `UnitTests` for behavior changes, and see [CLAUDE.md](CLAUDE.md) and [docs/project-rules.md](docs/project-rules.md) for project rules.
