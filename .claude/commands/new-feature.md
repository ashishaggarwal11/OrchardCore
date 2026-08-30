---
description: Create a new git branch for feature work, branched from main by default
---

Create a new branch for feature work.

1. Determine the branch name:
   - If the user supplied one as an argument (`$ARGUMENTS`), confirm it with them before proceeding.
   - Otherwise, ask the user for the branch name. Do not invent one or proceed without an explicit name.
2. Determine the source branch: default to `main` unless the user specifies a different one.
3. Run `git status` to check the working tree is clean before switching branches. If there are
   uncommitted changes, warn the user and confirm how to proceed rather than discarding anything.
4. If the source branch tracks a remote, fetch it (e.g. `git fetch origin <source>`) so the new branch
   is cut from an up-to-date base.
5. Create and check out the new branch from the source branch (e.g.
   `git switch -c <branch-name> <source>`).
6. Report the new branch name, its base, and current status to the user.
