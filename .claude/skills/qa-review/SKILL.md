---
name: qa-review
description: Validates an implementation against its stated requirement, generates test cases, and catches real functional issues by running the test suite. Use after code is implemented to confirm it actually does what was asked, not just that it compiles.
---

# qa-review

Validates functionality against the requirement it was meant to satisfy. Does not check
style/formatting (see `code-checker`) or security (see `security-review`).

## Workflow

1. Read the requirement/phase description being validated against, and read the actual diff
   (`git diff`) to see what was implemented.
2. Identify the changed public surface (new/changed methods, handlers, endpoints, shapes, recipes,
   migrations) that the requirement depends on.
3. Generate or extend xUnit test cases for that surface, following this repo's test conventions
   (see the `orchardcore-unit-test` skill under `.agents/skills/` and existing tests under
   `test/OrchardCore.Tests/`) — SiteContext-based integration tests where a tenant/session is
   needed, Moq for mocking OrchardCore services.
4. Run the tests:
   ```bash
   dotnet test test/OrchardCore.Tests/OrchardCore.Tests.csproj --filter-method "*.<NewOrAffectedTests>"
   ```
   For user-facing flows (admin UI, front-end pages, workflows), also run the relevant functional
   suite:
   ```bash
   dotnet test test/OrchardCore.Tests.Functional/OrchardCore.Tests.Functional.csproj --filter-class "*<AffectedArea>*"
   ```
5. Beyond "tests pass," re-read the requirement text and check the implementation actually
   satisfies it end-to-end — a green test suite that tests the wrong thing is not a pass.
6. Report every finding as:
   ```
   { severity: "error" | "warning", area, description }
   ```
   - `error`: the requirement is not met, a test fails, or a real functional bug/edge case is hit
     (e.g. null-handling, tenant isolation, wrong content-type behavior).
   - `warning`: works but has a functional gap, missing edge-case coverage, or an ambiguous
     requirement interpretation worth flagging.
   - If nothing is wrong, report a clean pass explicitly with a short note on what was exercised.

## Gotchas

- Don't just restate that "it builds" — build success is `code-checker`'s job, not this skill's.
- Prefer running the narrowest test filter that covers the change; the full unit suite is slow.
- Functional (Playwright) tests are heavier — only run them for changes with real UI/user-flow
  impact, not for internal refactors.

## References

- `.agents/skills/orchardcore-unit-test/` — this repo's test-writing conventions in depth.
- `AGENTS.md` — `dotnet test` command variants and functional test filtering.
- `test/OrchardCore.Tests/`, `test/OrchardCore.Tests.Functional/` — existing test patterns to mirror.
