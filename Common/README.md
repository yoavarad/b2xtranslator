# b2xtranslator.Common

Shared library used by the Doc, Xls and Ppt translators (`Common/b2xtranslator.csproj`, targets net8.0 and net10.0).

| Folder | Purpose |
| --- | --- |
| `StructuredStorage` | Reader/writer for OLE compound files (the container of .doc/.xls/.ppt) |
| `OpenXmlLib` | Writes OOXML packages (parts, relationships, content types); `ZipFactory` reads ZIP archives |
| `CommonTranslatorLib` | Base classes for the mapping/translation pipeline |
| `OfficeDrawing`, `OfficeGraph` | Escher drawing and chart record parsers shared across formats |
| `OlePropertySet` | OLE property set (document info) parsing |
| `Tools`, `Shell` | Logging and command-line helpers used by the `Shell` executables |

Build: `dotnet build Common`. Tests live in `UnitTests`.
