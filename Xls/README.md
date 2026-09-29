# b2xtranslator.xls

Translates Excel binary (`.xls`, BIFF8) files to SpreadsheetML (`.xlsx`).

- `XlsFileFormat` - BIFF record parsers and the in-memory data model. Entry point: `XlsDocument`.
- `SpreadsheetMLMapping` - mappings that write worksheets, styles, charts and shared strings into an OOXML package.

Depends on `Common`. Command-line front end: `Shell/xls2x`.

Build: `dotnet build Xls`.
