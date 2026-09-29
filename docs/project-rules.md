# Project Rules

Conventions, preferences, and domain knowledge.

## YDK PR base branch

The default branch is `main` (renamed from `master`). ydk core falls back to
`main`, so no `--base` workaround is needed: `ydk task start <id>` and
`ydk task done <id>` target `main` by default.
