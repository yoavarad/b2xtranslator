# b2xtranslator.doc

Translates Word binary (`.doc`) files to WordprocessingML (`.docx`).

- `DocFileFormat` - parsers for the binary structures (FIB, piece table, style sheet, character/paragraph properties, ...). Entry point: `WordDocument`.
- `WordprocessingMLMapping` - mappings that write the parsed document into an OOXML package built with `Common/OpenXmlLib`.

Depends on `Common`. Command-line front end: `Shell/doc2x`.

Build: `dotnet build Doc`.
