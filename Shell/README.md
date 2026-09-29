# Shell

Command-line executables that wrap the translators:

- `doc2x` - `.doc` to `.docx`
- `xls2x` - `.xls` to `.xlsx`
- `ppt2x` - `.ppt` to `.pptx`

Usage: `dotnet run --project Shell/doc2x -- <input> [-o <output>]` (`-?` prints all options).

Shared argument parsing and logging live in `Common/Shell`.
