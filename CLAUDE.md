# b2xtranslator

.NET library translating legacy binary Office formats (.doc, .xls, .ppt) to OOXML.
Projects: Common, Doc, Xls, Ppt, Shell, UnitTests, Benchmarks (see `b2xtranslator.sln`).
Build/test: `dotnet build b2xtranslator.sln`, `dotnet test UnitTests`. Benchmarks: see `docs/perf/baseline.md`.

## Rules

- Think before coding. Ask instead of guessing.
- Keep changes minimal and surgical; do not refactor adjacent code.
- Define "done" up front; use tests (`UnitTests`) as the success criterion.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
- Optional automation: `graphify hook install` adds post-commit/post-checkout hooks that refresh the graph. Not required; run `graphify update .` manually otherwise and commit `graphify-out/` changes (backup dirs `graphify-out/20*/` and `cache/` are gitignored).
