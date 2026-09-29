# b2xtranslator.ppt

Translates PowerPoint binary (`.ppt`) files to PresentationML (`.pptx`).

- `PptFileFormat` - record parsers for the PowerPoint binary format. Entry point: `PowerpointDocument`.
- `PresentationMLMapping` - mappings that write slides, masters, shapes and transitions into an OOXML package.

Depends on `Common`. Command-line front end: `Shell/ppt2x`.

Build: `dotnet build Ppt`.
