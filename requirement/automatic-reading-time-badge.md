# Requirement Plan: Automatic Reading-Time Badge

## Summary

Add a small, self-contained OrchardCore extension that calculates and displays an estimated
reading time for content items that contain a standard HTML body (`HtmlBodyPart`). The value is
shown as a badge in the admin Contents list and exposed as `readingTimeMinutes` in the standard
JSON API representation of the content item.

## Goals

- Compute reading time from the readable text of a content item's `HtmlBodyPart.Html`.
- Show a `N min read` badge next to applicable items in the admin Contents list.
- Expose the same value as a numeric `readingTimeMinutes` field in the content item's JSON API
  representation.
- Recalculate on every create/update so the value is never stale.
- Never error, and never show a value, for content with no readable text.

## Non-goals

- No changes to OrchardCore core/framework projects.
- No new third-party/NuGet dependency.
- No attachment of a new part to content types via the Content Type editor/migration — the
  feature works automatically wherever `HtmlBodyPart` is already used.
- No front-end/public-facing rendering of the badge (admin list only, per the request).

## Functional Requirements

### Word counting

- Convert `HtmlBodyPart.Html` to plain text before counting.
- Strip HTML tags, element names, and attributes — none of these count as words.
- Strip the *contents* of `<script>` and `<style>` blocks, not just their tags.
- Decode HTML entities (e.g. `&amp;`, `&nbsp;`) after tag stripping, so entities don't get glued
  to adjacent words and `&nbsp;`-only text is treated as whitespace.
- **Decision:** all HTML attributes are stripped uniformly, including `alt` text on `<img>`. An
  image with no visible text content — regardless of its `alt` attribute — has zero readable
  words. This matches the requirement's explicit example (`<img src="image.jpg">` → no reading
  time) and keeps the algorithm consistent (no tag is special-cased).
- Words are whitespace-separated tokens of the resulting plain text.

### Reading-time calculation

- Reading speed: 200 words per minute.
- `minutes = ceil(wordCount / 200)` for `wordCount > 0`.
- Because of the ceiling, any item with at least one readable word yields a minimum of 1 minute —
  no separate floor/minimum rule is needed.
- Display format in the admin list: `1 min read`, `2 min read`, etc. (localized, pluralization
  handled through the standard localization pipeline even though the English phrase does not
  change between singular/plural).

### Content with no readable text

- `wordCount == 0` must produce **no value** — not `0 min read`, and no exception.
- Cases that must reduce to zero readable words: empty string, `<p></p>`, `<p>   </p>`,
  `<img src="image.jpg">` (no readable text), and equivalently `<script>`/`<style>`-only bodies.

### Staying current

- Recalculate whenever a content item is created or its `HtmlBodyPart` body is saved (draft or
  published), so the badge and API value always reflect the latest saved body.

### Admin display and API consistency

- The admin Contents list shows the badge only for items that have a computed value.
- The API representation exposes `readingTimeMinutes` only for items that have a computed value.
- **Omit-when-absent rule (decision):** when there is no readable text, the computed part is
  entirely removed from the content item's JSON (`ContentItem.Remove<ReadingTimePart>()`) rather
  than stored as `null`. This means:
  - No badge is rendered in the admin list.
  - The `readingTimePart` key (and therefore `readingTimeMinutes`) is entirely **absent** from the
    JSON API response — not present with a `null` value.
  - This avoids growing every content item's JSON with a permanent `null` placeholder and keeps
    "not applicable" distinct from "computed as zero" (which can't happen anyway, per the ceiling
    rule).
- Both surfaces (badge and API) read the same stored value — there is exactly one calculation, run
  once per save, so they can never disagree.
- The extension must not affect listing, saving, or API responses for content items that don't
  have an `HtmlBodyPart`.

## Architecture / Design Decisions

### Why this doesn't require attaching a new part via the Content Type editor

OrchardCore lets a module register an *additional* handler and display driver against a CLR part
type that another module already declared, via `services.AddContentPart<HtmlBodyPart>()` called a
second time. Registrations are additive, not exclusive. That means this extension can react to
`HtmlBodyPart` and contribute to its admin summary display on **every** content type that already
uses `HtmlBodyPart`, with zero configuration and no content-type migration.

### New module: `OrchardCore.ReadingTime`

Self-contained module at `src/OrchardCore.Modules/OrchardCore.ReadingTime/`:

- `Manifest.cs` — `Dependencies = ["OrchardCore.Html"]`, `Category = "Content Management"`.
- `Models/ReadingTimePart.cs` — a plain `ContentPart` with a single `int? ReadingTimeMinutes`
  property. It is populated/removed purely through `ContentItem.Alter<T>()` /
  `ContentItem.Remove<T>()` JSON manipulation — it is **never** registered with
  `IContentDefinitionManager`, so it never appears in the Content Type Definition editor and
  cannot affect unrelated content types.
- `Services/IReadingTimeService.cs` + `ReadingTimeService.cs` — pure, unit-testable calculation:
  1. Remove `<script>...</script>` and `<style>...</style>` blocks (regex, case-insensitive,
     singleline).
  2. Strip remaining tags using the existing in-repo `RemoveTags()` string extension
     (`OrchardCore.ContentManagement.Abstractions/Utilities/StringExtensions.cs`) — no new
     dependency.
  3. HTML-decode entities.
  4. Split on whitespace with empty entries removed; count tokens.
  5. `wordCount <= 0` → return `null`; otherwise `Math.Ceiling(wordCount / 200.0)`.
- `Handlers/ReadingTimePartHandler.cs` — `ContentPartHandler<HtmlBodyPart>`, overriding
  `CreatedAsync`/`UpdatedAsync` to recompute via `IReadingTimeService` and either
  `Alter<ReadingTimePart>()` (value present) or `Remove<ReadingTimePart>()` (no readable text).
- `Drivers/ReadingTimePartDisplayDriver.cs` — `ContentPartDisplayDriver<HtmlBodyPart>`. `Display`
  returns `null` (no shape, no badge) when `ReadingTimePart` is absent; otherwise returns a shape
  located at the `SummaryAdmin` display type (the same mechanism used for other admin-list
  metadata badges), rendering the localized `N min read` text.
- `ViewModels/ReadingTimeViewModel.cs`, `Views/ReadingTimePart.SummaryAdmin.cshtml`,
  `Startup.cs`, `.csproj`.

### Decision: API JSON shape

`readingTimeMinutes` is nested as `Content.ReadingTimePart.ReadingTimeMinutes` (camelCased by the
default JSON naming policy to `content.readingTimePart.readingTimeMinutes`), consistent with how
every other OrchardCore part appears in the content API (e.g.
`Content.AutoroutePart.Path`). This works automatically with any controller that serializes the
raw `ContentItem` — no custom `JsonConverter` or bespoke API controller is needed. A true top-level
sibling of `ContentItemId`/`ContentType` was considered and rejected because it would require
extra serialization plumbing for no functional benefit.

### Decision: no new dependency for HTML parsing

AngleSharp is centrally pinned in `Directory.Packages.props` but not referenced anywhere in
`src/` today. Rather than introduce its first real usage for this small feature, the calculation
uses a small in-repo regex (for `<script>`/`<style>` removal) plus the existing `RemoveTags()`
extension. This is adequate for well-formed WYSIWYG editor output and keeps the module free of new
third-party dependencies, per the constraint below. It is not a full HTML parser and won't handle
pathologically malformed markup — considered an acceptable tradeoff for this feature's scope.

## Test Plan

Automated tests to add under `test/OrchardCore.Tests/ReadingTime/`, following repo unit-test
naming conventions (`{Subject}Tests`, `{Action}_{Condition}_{ExpectedResult}`):

`ReadingTimeServiceTests`:

- `ComputeMinutes_OneWord_ReturnsOneMinute`
- `ComputeMinutes_ExactlyTwoHundredWords_ReturnsOneMinute`
- `ComputeMinutes_TwoHundredAndOneWords_ReturnsTwoMinutes`
- `ComputeMinutes_HtmlTagsAndAttributes_ExcludedFromWordCount` (tags, attribute values, and
  element names never count)
- `ComputeMinutes_ScriptAndStyleContent_ExcludedFromWordCount`
- `ComputeMinutes_EmptyString_ReturnsNull`
- `ComputeMinutes_EmptyParagraph_ReturnsNull` (`<p></p>`)
- `ComputeMinutes_WhitespaceOnlyParagraph_ReturnsNull` (`<p>   </p>`)
- `ComputeMinutes_ImageOnlyNoReadableText_ReturnsNull` (`<img src="image.jpg">`)

`ReadingTimePartHandlerTests`:

- `UpdatedAsync_BodyEditedWithNewText_RecalculatesReadingTime` (edit → save → value updates, never
  stale)
- `UpdatedAsync_BodyEditedToRemoveAllText_RemovesReadingTimePart`

`ReadingTimePartDisplayDriverTests`:

- `Display_ReadingTimePartAbsent_ReturnsNull` (no badge shape)
- `Display_ReadingTimePartPresent_ViewModelMatchesStoredValue` (badge and the value that will be
  serialized to the API come from the same stored `ReadingTimePart.ReadingTimeMinutes` — proves
  agreement by construction, since both read the identical value with no separate calculation)

## Constraints

- Implemented as a self-contained module under `src/OrchardCore.Modules/OrchardCore.ReadingTime/`
  using established repository conventions (file-scoped namespaces, `sealed` classes, async
  suffixing, StyleCop/CA rules, localization via `IStringLocalizer`/`T.Plural`).
- No modification to OrchardCore core/framework code.
- No new third-party dependency (see decision above).
- No push, merge, publish, or deploy — implementation and verification only.

## Definition of Done

- A content item with a standard HTML body displays the correct reading-time badge in the admin
  Contents list.
- The same content item's standard JSON API representation exposes the matching
  `readingTimeMinutes` value.
- Both results update after every saved edit to the body.
- Content with no readable text produces no badge and omits `readingTimeMinutes` from the API
  (key entirely absent, not `null`).
- All automated tests listed above pass, and unrelated content items continue to list, save, and
  return via the API without regression.
