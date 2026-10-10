# Project Rules

Conventions, preferences, and domain knowledge.

## YDK PR base branch

The default branch is `main` (renamed from `master`). ydk core falls back to
`main`, so no `--base` workaround is needed: `ydk task start <id>` and
`ydk task done <id>` target `main` by default.

## Windows Python hooks

Audit of `.claude/hooks` on Windows:

- `settings.json` runs `guard.py` via an absolute interpreter path written by
  `ydk init` (`sys.executable`); it works but is machine-specific.
- `check-task-complete.sh` called `python3`, which on Windows is often the
  Microsoft Store stub (not a real interpreter), so the `2>/dev/null` swallowed
  the failure and the hook silently allowed session end. It now picks the first
  working of `python3`, `python`, `py`.
- `ydk init` (`_install_claude_hooks`, `_install_hooks`) rewrites these files
  unconditionally, so local fixes are overwritten on re-init until fixed
  upstream in ydk.
