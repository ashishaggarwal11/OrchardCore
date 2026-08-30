# Plan: Automatic Reading-Time Badge

Requirement source: full spec provided inline by requester (see task history / commit message for
this plan). Module: `OrchardCore.ReadingTime`, a self-contained module reacting to `HtmlBodyPart`
without registering a new attachable part.

Repo conventions confirmed during exploration:
- Similar module shape: `src/OrchardCore.Modules/OrchardCore.Html/` (`Manifest.cs`, `Startup.cs`,
  `Models/`, `Handlers/`, `Drivers/`, `ViewModels/`, `Views/`, `.csproj`).
- `RemoveTags(this string html, bool htmlDecode = false)` already exists in
  `src/OrchardCore/OrchardCore.ContentManagement.Abstractions/Utilities/StringExtensions.cs` and
  optionally HTML-decodes — usable directly instead of separate decode step.
- Unit tests for modules live under `test/OrchardCore.Tests/Modules/OrchardCore.<ModuleName>/`
  (e.g. `test/OrchardCore.Tests/Modules/OrchardCore.Contents/...`), not under a flat
  `ReadingTime/` folder — plan adjusts the spec's suggested test path
  (`test/OrchardCore.Tests/ReadingTime/`) to follow this existing convention:
  `test/OrchardCore.Tests/Modules/OrchardCore.ReadingTime/`.
- `services.AddContentPart<HtmlBodyPart>()` can be called again additively per the spec's
  architecture decision (second registration contributes handler/driver without redefining the
  part).

No ambiguities requiring escalation were found — the spec is fully self-contained with explicit
decisions for every edge case (word counting, omit-when-absent, API shape, no new dependency).

---

## Phase 1 — Scaffold the `OrchardCore.ReadingTime` module project (done)

**Scope**: Create the new module project skeleton with manifest, csproj, and startup wiring so it
builds and loads as a feature, with no functional logic yet.

**Files/areas touched**:
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/OrchardCore.ReadingTime.csproj`
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/Manifest.cs`
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/Startup.cs` (empty `ConfigureServices` for now)
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/Properties/AssemblyInfo.cs` (if other modules
  have one, per `OrchardCore.Html` pattern)
- Solution file (`OrchardCore.sln` or the relevant `.slnf`) — add new project reference if the
  repo convention requires explicit inclusion (check how `OrchardCore.Html` is referenced from
  `OrchardCore.Cms.Web`/solution).

**Acceptance criteria**:
- `Manifest.cs` declares `[assembly: Module(Name = "Reading Time", ..., Dependencies =
  ["OrchardCore.Html"], Category = "Content Management")]` (adjust `Name`/`Id` to match repo
  manifest conventions observed in `OrchardCore.Html/Manifest.cs`).
- Project builds successfully (`dotnet build src/OrchardCore.Modules/OrchardCore.ReadingTime/OrchardCore.ReadingTime.csproj`).
- Module is discoverable/loadable (no feature yet does anything, but `Startup` class exists and is
  a `sealed` `StartupBase` subclass per repo convention).
- No new third-party NuGet package reference added.

**Validation focus**: Confirm no core/framework files were modified (constraint: "No modification
to OrchardCore core/framework code"). Confirm no new dependency was added to
`Directory.Packages.props` or the `.csproj`. Confirm file-scoped namespaces and `sealed` classes
per repo StyleCop conventions.

---

## Phase 2 — `ReadingTimePart` model and `IReadingTimeService` calculation logic (pending)

**Scope**: Implement the plain, unregistered `ReadingTimePart` model and the pure calculation
service (`IReadingTimeService`/`ReadingTimeService`) that turns `HtmlBodyPart.Html` into a
nullable minute count, with no wiring into handlers/drivers yet.

**Files/areas touched**:
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/Models/ReadingTimePart.cs`
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/Services/IReadingTimeService.cs`
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/Services/ReadingTimeService.cs`
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/Startup.cs` (register
  `services.AddScoped<IReadingTimeService, ReadingTimeService>()` — safe to add now even though
  nothing consumes it yet)

**Acceptance criteria**:
- `ReadingTimePart` is a `sealed` `ContentPart` with a single `public int? ReadingTimeMinutes { get; set; }`
  property; it is **not** passed to `services.AddContentPart<T>()` in this phase and never
  registered with `IContentDefinitionManager`/content type editor.
- `IReadingTimeService` exposes a method (e.g. `int? ComputeReadingTimeMinutes(string html)`) that:
  1. Removes `<script>...</script>` and `<style>...</style>` blocks via case-insensitive,
     singleline regex.
  2. Strips remaining tags via the existing `RemoveTags(htmlDecode: true)` extension from
     `OrchardCore.ContentManagement.Utilities.StringExtensions` (no separate manual decode step
     needed since `RemoveTags` already supports `htmlDecode`).
  3. Splits on whitespace with empty entries removed (`string.Split` with
     `StringSplitOptions.RemoveEmptyEntries` or equivalent) and counts tokens.
  4. Returns `null` when the count is `0`; otherwise `(int)Math.Ceiling(wordCount / 200.0)`.
- No new third-party dependency introduced (regex and `WebUtility`/`RemoveTags` only).
- Method is deterministic and side-effect-free (pure function suitable for direct unit testing).

**Validation focus**: Correctness of edge cases per spec — empty string, whitespace-only
paragraph, image-only markup, script/style-only bodies, exact boundary word counts (200 vs 201
words). Confirm attribute values (including `alt` text) are never counted, per the spec's explicit
"strip all attributes uniformly" decision — this is a specific behavior worth a security/QA double
check since it's a deliberate deviation from "helpful" alt-text counting. Confirm regex is
anchored/scoped enough not to catastrophically backtrack on large inputs (ReDoS consideration for
`<script>`/`<style>` removal regex).

---

## Phase 3 — Unit tests for `ReadingTimeService` (pending)

**Scope**: Add the `ReadingTimeServiceTests` test class covering every case listed in the spec's
test plan, run against the Phase 2 implementation in isolation (no DI/content pipeline needed).

**Files/areas touched**:
- `test/OrchardCore.Tests/Modules/OrchardCore.ReadingTime/ReadingTimeServiceTests.cs`
- `test/OrchardCore.Tests/OrchardCore.Tests.csproj` (add project reference to
  `OrchardCore.ReadingTime.csproj` if not already covered by a wildcard/module test grouping —
  check how `OrchardCore.Contents` test references are wired in this csproj first)

**Acceptance criteria**: all of the following tests exist and pass:
- `ComputeMinutes_OneWord_ReturnsOneMinute`
- `ComputeMinutes_ExactlyTwoHundredWords_ReturnsOneMinute`
- `ComputeMinutes_TwoHundredAndOneWords_ReturnsTwoMinutes`
- `ComputeMinutes_HtmlTagsAndAttributes_ExcludedFromWordCount`
- `ComputeMinutes_ScriptAndStyleContent_ExcludedFromWordCount`
- `ComputeMinutes_EmptyString_ReturnsNull`
- `ComputeMinutes_EmptyParagraph_ReturnsNull`
- `ComputeMinutes_WhitespaceOnlyParagraph_ReturnsNull`
- `ComputeMinutes_ImageOnlyNoReadableText_ReturnsNull`
- Test naming and structure follow repo unit-test conventions (`{Subject}Tests`,
  `{Action}_{Condition}_{ExpectedResult}`), matching the framework used elsewhere in
  `test/OrchardCore.Tests` (xUnit + Shouldly/Assert style — confirm exact assertion library from
  a neighboring test file before writing).
- `dotnet test` targeting this test class passes with no failures.

**Validation focus**: Confirm each spec-listed test case is present verbatim (no silently dropped
or renamed cases) and actually exercises the described input/output, not a trivial always-true
assertion.

---

## Phase 4 — `ReadingTimePartHandler`: recompute on create/update (pending)

**Scope**: Wire a `ContentPartHandler<HtmlBodyPart>` that recalculates reading time on
`CreatedAsync`/`UpdatedAsync` and applies the omit-when-absent rule via `Alter<ReadingTimePart>()`
/ `Remove<ReadingTimePart>()`, then register it alongside a second `AddContentPart<HtmlBodyPart>()`
call in `Startup.cs`.

**Files/areas touched**:
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/Handlers/ReadingTimePartHandler.cs`
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/Startup.cs` (add
  `services.AddContentPart<HtmlBodyPart>().AddHandler<ReadingTimePartHandler>();` — additive
  registration per the spec's architecture decision, alongside the existing
  `OrchardCore.Html` module's own `AddContentPart<HtmlBodyPart>()` call)

**Acceptance criteria**:
- Handler overrides `CreatedAsync` and `UpdatedAsync` (or the equivalent lifecycle hook(s) that
  fire on both create and every draft/published save of the owning content item), reads
  `part.Html`, calls `IReadingTimeService`, and:
  - value present → `context.ContentItem.Alter<ReadingTimePart>(p => p.ReadingTimeMinutes = value)`
  - value absent (`null`) → `context.ContentItem.Remove<ReadingTimePart>()`
- Recalculation happens on every save (not just first create) so edits that change or remove body
  text update or remove the part accordingly — no stale values persist.
- Content items without `HtmlBodyPart` are entirely unaffected (handler is scoped to
  `HtmlBodyPart`, never touches other content).
- Handler class is `sealed`, async-suffixed, file-scoped namespace, per repo conventions.

**Validation focus**: This is the core "never stale" guarantee — validator should specifically
test edit-then-save-with-shorter/emptied body transitions (word count going from >0 to 0) to
confirm `Remove<ReadingTimePart>()` is actually invoked and not just skipped/left stale. Also
check that the handler doesn't run expensive computation when `HtmlBodyPart.Html` is unchanged if
the spec implies otherwise (spec says recompute "whenever ... saved," so recomputing
unconditionally on every save is acceptable and simpler — flag if a developer tries to
over-optimize with dirty-checking not requested by spec).

---

## Phase 5 — Unit tests for `ReadingTimePartHandler` (pending)

**Scope**: Add `ReadingTimePartHandlerTests` covering recalculation and removal behavior on
update, using the repo's standard approach for testing content handlers (in-memory `ContentItem`
plus a real or fake `IReadingTimeService`).

**Files/areas touched**:
- `test/OrchardCore.Tests/Modules/OrchardCore.ReadingTime/ReadingTimePartHandlerTests.cs`

**Acceptance criteria**:
- `UpdatedAsync_BodyEditedWithNewText_RecalculatesReadingTime` — start with one value, change body
  text, assert the persisted `ReadingTimePart.ReadingTimeMinutes` reflects the new word count (not
  the old one).
- `UpdatedAsync_BodyEditedToRemoveAllText_RemovesReadingTimePart` — start with a present part,
  update body to empty/whitespace/image-only, assert `ContentItem.As<ReadingTimePart>()` is
  `null` afterward (part fully removed from the content item's JSON, not set to `null` value).
- Tests pass with `dotnet test`.

**Validation focus**: Confirm the removal assertion checks for genuine absence (e.g.
`contentItem.Has<ReadingTimePart>()` is false, or the JSON has no `ReadingTimePart` key) rather
than merely `ReadingTimeMinutes == null` while the part object itself still exists — the spec is
explicit that the *part* must be removed, not nulled.

---

## Phase 6 — `ReadingTimePartDisplayDriver`: admin `SummaryAdmin` badge (pending)

**Scope**: Add the display driver and view model/view that render the `N min read` badge in the
admin Contents list, appearing only when `ReadingTimePart` is present on the content item.

**Files/areas touched**:
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/Drivers/ReadingTimePartDisplayDriver.cs`
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/ViewModels/ReadingTimeViewModel.cs`
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/Views/ReadingTimePart.SummaryAdmin.cshtml`
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/Startup.cs` (append
  `.UseDisplayDriver<ReadingTimePartDisplayDriver>()` to the same `AddContentPart<HtmlBodyPart>()`
  chain from Phase 4)

**Acceptance criteria**:
- `ReadingTimePartDisplayDriver` is a `ContentPartDisplayDriver<HtmlBodyPart>`. `Display` (or
  `DisplayAsync`) returns `null`/no shape when `HtmlBodyPart`'s owning `ContentItem` has no
  `ReadingTimePart` (i.e. `contentItem.As<ReadingTimePart>()` is `null`), and otherwise returns a
  shape at the `SummaryAdmin` display type, following the existing pattern used by other
  admin-list metadata badges in this repo (locate one concrete existing precedent — e.g. search
  `SummaryAdmin` shape usages in `OrchardCore.Contents`/`OrchardCore.Media` — and match its
  shape-building call signature).
- View renders localized text via `T.Plural(...)` or `IStringLocalizer`/`IViewLocalizer`
  producing `"{0} min read"` for the stored `ReadingTimeMinutes` value (English text identical for
  singular/plural per spec, but still routed through the localization pipeline).
- Badge appears only in the admin Contents list context (`SummaryAdmin`), not on the front-end
  public content display, per non-goals.
- Content types without `HtmlBodyPart`/without a computed `ReadingTimePart` show no badge and are
  visually unaffected.

**Validation focus**: Confirm `Display` truly returns no shape (not an empty/placeholder shape)
when absent, so the admin list layout doesn't shift or show an empty badge container. Confirm the
view only outputs text, no unescaped user-controlled HTML injection (badge text is a computed
integer plus a localized static string, but check the Razor view doesn't use `Html.Raw` or
similar unnecessarily — XSS review angle even though input is server-computed, not
user-string-templated).

---

## Phase 7 — Unit tests for `ReadingTimePartDisplayDriver` (pending)

**Scope**: Add `ReadingTimePartDisplayDriverTests` verifying the absent/present branching and that
the badge's displayed value matches the stored `ReadingTimePart.ReadingTimeMinutes` exactly (proof
of single-source-of-truth agreement with the API surface).

**Files/areas touched**:
- `test/OrchardCore.Tests/Modules/OrchardCore.ReadingTime/ReadingTimePartDisplayDriverTests.cs`

**Acceptance criteria**:
- `Display_ReadingTimePartAbsent_ReturnsNull` — content item without `ReadingTimePart` produces no
  shape/result from the driver's display method.
- `Display_ReadingTimePartPresent_ViewModelMatchesStoredValue` — content item with a
  `ReadingTimePart.ReadingTimeMinutes` of some value N produces a view model/shape whose value is
  exactly N (read from the stored part, no independent recomputation inside the driver).
- Tests pass with `dotnet test`.

**Validation focus**: Confirm the test genuinely asserts equality against the *stored* part value
(not a hardcoded literal that happens to match), so a future regression where the driver
recalculates independently instead of reading the stored value would be caught.

---

## Phase 8 — End-to-end wiring review, JSON API shape verification, and full test/build pass (pending)

**Scope**: Final integration pass — confirm `Startup.cs` registrations are complete and correctly
ordered, confirm the JSON API representation naturally exposes
`content.readingTimePart.readingTimeMinutes` with no custom serialization code, confirm the
omit-when-absent behavior holds end-to-end (create/update/API/admin-list), and run the full
affected test suite plus a solution build.

**Files/areas touched**:
- `src/OrchardCore.Modules/OrchardCore.ReadingTime/Startup.cs` (final review of full registration
  chain: part + handler + display driver + `IReadingTimeService`)
- Possibly `src/OrchardCore.Cms.Web/OrchardCore.Cms.Web.csproj` or module-inclusion list, if the
  module needs to be referenced/enabled by the CMS web host to be exercised by any
  integration-style check (verify whether other modules are auto-discovered or must be explicitly
  referenced, per repo convention, before adding this).
- No production code changes expected beyond fixes surfacing from this review; this phase is
  primarily verification.

**Acceptance criteria**:
- `services.AddContentPart<HtmlBodyPart>()` chain in `ReadingTime/Startup.cs` registers handler
  and display driver additively, without redefining or conflicting with `OrchardCore.Html`'s own
  registration.
- A content item with a populated `HtmlBodyPart.Html` serializes (via the standard content item
  JSON serialization path used by the existing content API) to include
  `content.readingTimePart.readingTimeMinutes` as a plain number, camelCased by the default JSON
  naming policy, with no bespoke `JsonConverter` or API controller changes required — confirmed by
  inspecting serializer output for a test content item (not necessarily a live HTTP round-trip
  test, unless one already exists cheaply in this repo's test harness).
- A content item with no readable text has no `readingTimePart` key at all in the serialized JSON
  (absent, not `null`).
- `dotnet build` succeeds for the solution/relevant projects with no new warnings introduced by
  this module.
- All tests from Phases 3, 5, and 7 pass together as a full run (`dotnet test` filtered to the
  `ReadingTime` test namespace, or full suite if fast enough).
- No core/framework files modified; no new third-party dependency added anywhere in the diff.

**Validation focus**: This phase is the Definition-of-Done gate — qa-review should re-check every
item in the requirement's "Definition of Done" section explicitly (badge correctness, API field
presence/absence, recalculation on every save, zero-readable-text omission, full test pass,
non-regression for content without `HtmlBodyPart`). security-review should check for any
unbounded regex/input-size concerns in the script/style-stripping regex against large HTML bodies,
and confirm no user-supplied string ever reaches a shape/view without going through the existing
Razor auto-encoding (i.e. no `Html.Raw`).
