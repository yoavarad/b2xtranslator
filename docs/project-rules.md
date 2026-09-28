# Project Rules

Conventions, preferences, and domain knowledge.

## YDK PR base branch (master, not main)

The default branch here is `master`. `ydk task done` has no base option and
`.ydk/config.yaml` has no base-branch key, so ydk core falls back to `main`
unless the task recorded a base.

Workaround: always start tasks with an explicit base:

    ydk task start <id> --base origin/master

`start` records `base_branch` for the task in the main repo's
`.ydk/active-task.json` (e.g. `{"tasks": {"45": {"base_branch": "origin/master"}}}`),
which `ydk task done` then uses for the PR base. Do not hand-write
active-task.json. `ydk task quick` does not write active-task.json, so avoid
it for PR-producing work. Upstream ydk bug: `done`/`quick` hard-code `main`
(core/task_lifecycle.py, core/quickdev.py); should read the repo default branch.
