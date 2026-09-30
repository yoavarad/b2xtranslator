# Doc/PPT stub inventory (task #37)

Scope: `NotImplemented` / `TODO` / "not yet implemented" markers in `Doc/` and `Ppt/`.
Most repo-wide markers (~214 files) live in `Xls/` and are out of scope here.
Color-scheme, animation/transition and magic-constant items were handled by
#34, #35, #36 and are not repeated below.

Status: **done** = implemented in this task; **deferred** = left as is, with reason.

## PPT: crash paths (`throw NotImplementedException`)

| Location | Item | Status |
|---|---|---|
| `Utils.PlaceholderIdToXMLValue` | `VerticalTextTitle` -> `title`, `VerticalTextBody` -> `body` | **done** |
| `Utils.SlideLayoutTypeToFilename` | `VerticalTitleRightBodyLeft` (SL_VerticalTitleBody) -> `vertTitleAndTx` (default layout already shipped) | **done** |
| `Utils.SlideLayoutTypeToFilename` | `VerticalTitleRightBodyLeftTwoRows` -> `vertTitleAndTxOverChart` | done: default layout XML `vertTitleAndTxOverChart.xml` shipped |
| `Utils.SlideLayoutTypeToFilename` | `TwoRowsBottomTwoColumns` | **done (#86, falls back to `obj`, see adr-unmapped-slide-layouts.md)**; was: ECMA-376 ST_SlideLayoutType has `twoObjOverTx` (top columns) but no bottom-columns counterpart, so it would need a `cust` layout |
| `Utils.SlideLayoutTypeToFilename` | `TwoColumnsLeftTwoRows` / `TwoColumnsRightTwoRows` / `TwoRowsAndTitle` with other placeholder combos | deferred:  no spec-defined target; choosing one is a guess |
| `Utils.SlideLayoutTypeToFilename` | `TitleMaster`, `MasterNotes`, `NotesTitleAndBody`, `Handout` | deferred: master/notes/handout geometries (MS-PPT SlideLayoutType 2, 4, 5, 6). No slide-layout target, so they still hit the default throw |
| `Utils.PlaceholderIdToXMLValue` | `None` | deferred: not a real placeholder |
| `Utils.SlideSizeTypeToXMLValue` | default branch | deferred: every MS-PPT SlideSizeEnum value is already mapped |
| `TextHeaderAtom.HandleTextDataRecord` | unknown `ITextDataRecord` | deferred: can't be reached; `TextAtom` and `TextStyleAtom` are the only implementers |

## PPT: placeholder/partial output

| Location | Item | Status |
|---|---|---|
| `ShapeTreeMapping` (`p:ph` writers) | `orient="vert"` for vertical placeholders | deferred: needs a real-file conversion fixture |
| `Master/NotesMaster/HandoutMasterMapping` | `txStyles` for pre-PP2007 files (no roundTripTxStyles) | deferred: large feature, own task |
| `SlideMapping`, `NoteMapping` | `clrMapOvr`, notes `p:bg`, master slide data | deferred: optional elements, output is valid without them |
| `CharacterRunPropsMapping`, `TextMasterStyleMapping` | scheme-color TODOs, default values | deferred: color work was #34 |
| `FillMapping`, `*MasterMapping` | fallback color `FFFFFF`/`000000` | deferred: spec gives no fallback |
| `ShapeTreeMapping` | adjust values for shapes other than `roundRect`, coordinate conversion TODOs | deferred: #36 area, own task |
| `TextMapping` | assorted `//TODO` branches (L371, L692, L986) | deferred: behavior not specified in comments |
| `FontEntityAtom` | read remaining flags | deferred: flags aren't used by the mapping |
| `ParagraphRun` | "still a guess" bit layout | deferred: needs MS-PPT verification against samples |
| `Converter` | macro-type detection | deferred: works as is |

## Doc

| Location | Item | Status |
|---|---|---|
| `DocumentProperties.setDefaultCompatibilityOptions` | "Don't autofit tables next to wrapped objects", "Don't break constrained tables", "underline characters in numbered lists" | deferred: no matching DOP fields exist yet |
| `TablePropertyExceptions(byte[])` | "not yet implemented" comment | not a stub: base `PropertyExceptions(byte[])` already parses the grpprl |
| `ToolbarControlBitmap` | read TBCBitmap | deferred: toolbar customizations aren't converted |
