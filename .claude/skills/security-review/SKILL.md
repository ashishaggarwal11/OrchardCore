---
name: security-review
description: Reviews the current diff for security issues (injection, secrets, missing authz, unsafe deserialization) and, when a new third-party package was added, runs a dependency vulnerability check. Use after implementation, before merging, especially whenever a new NuGet or yarn dependency is introduced.
---

# security-review

Checks the pending diff for security issues. Does not check style/formatting (see `code-checker`)
or functional correctness (see `qa-review`).

## Workflow

1. Get the diff: `git diff` (and `git status --porcelain` for untracked new files).
2. Review the diff for common issues:
   - Injection risk (raw SQL/HTML/shell string interpolation instead of parameterized/escaped APIs).
   - Secrets or credentials committed in code/config.
   - Unsafe deserialization of untrusted input.
   - New endpoints/handlers/permissions missing authorization checks (`[Authorize]`,
     `IAuthorizationService`, permission providers) where existing similar handlers have them.
   - Missing tenant-isolation checks in shell-scoped services (see CLAUDE.md's multi-tenancy notes)
     that could leak data across tenants.
3. **If a new third-party dependency was added** — check any of `*.csproj`, `Directory.Packages.props`,
   or `package.json` for new package references in the diff. If found, run:
   ```bash
   dotnet list package --vulnerable --include-transitive
   ```
   and, for new JS/TS dependencies:
   ```bash
   yarn audit
   ```
   Note: this repo's CI builds with `-p:NuGetAudit=false` (see PR #16317) specifically so a newly
   disclosed NuGet advisory doesn't break unrelated PRs — this skill's explicit check is the
   deliberate substitute, so don't skip it just because the build succeeded.
4. Report every finding as:
   ```
   { severity: "error" | "warning", area, description }
   ```
   - `error`: exploitable vulnerability, a vulnerable dependency with a known CVE at a severity
     that matters for this package's usage, missing authz on a new sensitive endpoint, committed
     secret.
   - `warning`: defense-in-depth gap, low-severity/hard-to-reach advisory, or a pattern worth
     tightening but not immediately exploitable.
   - If nothing is wrong, report a clean pass explicitly, noting whether a dependency check was run.

## Gotchas

- Only run the vulnerability scan when a dependency actually changed — it's slow and noisy
  otherwise.
- A vulnerable transitive dependency your code doesn't actually exercise is still worth a
  `warning`, not necessarily an `error` — use judgment based on reachability.

## References

- `AGENTS.md` — dependency/versioning conventions.
- `Directory.Packages.props` — central package version pinning for the whole repo.
