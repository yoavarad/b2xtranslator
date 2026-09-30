# ADR: Unmapped PPT slide layout types

Status: proposed (approve in PR)

## Problem
`Utils.SlideLayoutTypeToFilename` threw `NotImplementedException` for layout types with no
ECMA-376 `ST_SlideLayoutType` counterpart, aborting the whole conversion.

## Options
- (a) Emit a `cust` layout built from the actual placeholders: most faithful, but needs new
  layout generation code; large scope.
- (b) Fall back to a generic layout and log a warning: small, converts every file.
- (c) Keep throwing only for impossible values: still aborts on real-world files.

## Decision (recommended: b)
- `TwoRowsBottomTwoColumns`, unmapped `TwoColumnsLeftTwoRows` / `TwoColumnsRightTwoRows` /
  `TwoRowsAndTitle` placeholder combos: fall back to `obj` (title + body), log via `Trace`.
- `TitleMaster`, `MasterNotes`, `NotesTitleAndBody`, `Handout` and any other value: fall back to
  `blank`, log via `Trace`.
- Shapes on the slide are still written from the slide itself; only the inherited layout is generic.
- (a) can be a follow-up if fidelity matters.
