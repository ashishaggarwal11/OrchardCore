---
name: code-checker
description: Checks style and formatting issues in changed .NET and frontend code — runs the analyzer/StyleCop build and yarn lint/check, and reports violations as file/line/rule findings. Use after implementing or editing code, before handing it off for review.
---

# code-checker

Checks the current changes for style/formatting/analyzer violations. Does not review functionality
or security — see `qa-review` and `security-review` for those.

## Workflow

1. Identify what changed: `git diff --name-only` (or `git status --porcelain` for untracked files).
2. **For .NET changes** — build the affected project(s) with warnings-as-errors, matching this
   repo's CI (`pr_ci.yml`):
   ```bash
   dotnet build <affected.csproj or .sln> -c Release -p:TreatWarningsAsErrors=true --warnaserror -p:RunAnalyzers=true
   ```
   If the change is repo-wide or the affected project is unclear, build the solution file instead of
   guessing a project.
3. **For frontend changes** (`.js`/`.ts`/`.vue`/`.scss` under any `Assets.json`-driven project) —
   run from the repo root:
   ```bash
   yarn lint
   yarn check
   ```
4. Parse the build/lint output into findings shaped as:
   ```
   { file, line, rule, message }
   ```
5. Report:
   - If clean, say so explicitly (no findings).
   - If not clean, list every finding with file/line/rule so the caller can jump straight to the
     location — do not summarize or drop entries.
   - Do not silently auto-fix. Only fix what's evident from the analyzer's own message (e.g. a
     StyleCop ordering rule) when you are the one asked to also correct the code; if you are only
     asked to check, report findings and stop.

## Gotchas

- `dotnet build` without `--warnaserror` will not surface StyleCop violations as failures — always
  include the flags above, they mirror CI exactly (`AGENTS.md`, `pr_ci.yml`).
- Building the full solution is slow; prefer the smallest `.csproj` that covers the change.
- Frontend checks require `yarn install` to have been run at least once in the repo.

## References

- `AGENTS.md` — StyleCop/analyzer conventions, `yarn lint` / `yarn check` commands.
- `src/OrchardCore.Build/OrchardCore.Commons.props` — where analyzer/AnalysisLevel settings are pinned.
