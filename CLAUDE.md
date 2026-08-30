# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Primary reference

**Read [AGENTS.md](AGENTS.md) first.** It is the authoritative, actively maintained instructions file for this
repo and covers: build/run commands, unit and functional (Playwright) test commands, coding conventions
(naming, `sealed`, file-scoped namespaces, collection expressions, async suffixing), StyleCop/CA rules,
YesSql database patterns, docs/XML-doc requirements, PR guidelines, and the `ocat-*` admin edit-view CSS
conventions. This CLAUDE.md file only adds architecture context that isn't already there — do not duplicate it.

## Keeping this file current

When a change adds/removes a top-level architecture piece described above (a new `src/` project category, a
new persistence/rendering pattern, a new guided skill directory, etc.), update the relevant section of this
file in the same PR. Skip this for changes that don't affect the architecture summary (bug fixes, routine
feature work, test-only changes) — do not duplicate AGENTS.md content here.

## Guided skills

Task-specific, step-by-step skills live in `.agents/skills/` (e.g. `orchardcore-module-creator`,
`orchardcore-data-migration`, `orchardcore-display-management`, `orchardcore-tenants`,
`orchardcore-workflow-activity`, `orchardcore-query-indexing`, `orchardcore-unit-test`, etc.). Check there
before hand-rolling a pattern for module creation, migrations, display/placement, workflows, indexing,
localization, tenants, or docs — the table in AGENTS.md maps task type to skill name.

## Architecture

Orchard Core is a **modular, multi-tenant application framework** (the "Framework") with a CMS built on top
of it (the "CMS"). Understanding the module/feature/tenant system is more important than any single file.

### Solution layout

- `src/OrchardCore/` — Framework libraries. Each folder is a separate NuGet-packable project. Names ending
  in `.Abstractions` or `.Core` split public contracts (interfaces/DTOs, safe for other modules to depend on
  without pulling in implementation) from the implementation.
- `src/OrchardCore.Modules/` — Built-in CMS modules (Contents, Menu, Media, Lucene, Workflows, GraphQL,
  Deployment, etc.). Each module is its own project with its own `Startup.cs`, `Manifest.cs`, and optional
  `Assets.json`.
- `src/OrchardCore.Themes/` — Built-in front-end/admin themes.
- `src/OrchardCore.Cms.Web/` — The runnable CMS host application (references the module/theme sets it wants
  enabled).
- `src/OrchardCore.Mvc.Web/` — A minimal host demonstrating using the Framework without the full CMS feature
  set.
- `src/OrchardCore.Build/` — Shared MSBuild props/targets (`TargetFrameworks.props`, `OrchardCore.Commons.props`)
  imported by every project; this is where the target framework and shared analyzers/versions are pinned.
- `test/` — `OrchardCore.Tests` (unit), `OrchardCore.Abstractions.Tests`, `OrchardCore.Tests.Functional`
  (Playwright E2E), `OrchardCore.Tests.Modules` (fixtures/test-only modules).

### Module and feature model

Every module declares itself via an assembly-level attribute in `Manifest.cs`:

```csharp
[assembly: Module(Name = "Menu", Dependencies = ["OrchardCore.Contents", ...], Category = "Navigation")]
```

A module can expose multiple named *features* (via `[assembly: Feature(...)]`) that can be independently
enabled/disabled per tenant, each with its own dependency graph. `Startup.cs` in each module registers
services (`ConfigureServices`) and typically wires MVC areas/routes; a module can have multiple
feature-scoped `Startup` classes gated with `[Feature("...")]` / `[RequireFeatures(...)]`. Do not assume a
module's services are always active — check feature dependencies before adding cross-module references.

### Multi-tenancy (shells)

Each tenant runs in its own isolated **shell**: its own DI container, its own set of enabled
features/settings, and (usually) its own database connection/table prefix or schema, all coordinated by the
shell host. Framework-level singletons live in the host container; almost everything else (content
management, sessions, most services) is shell-scoped. When adding a service, be deliberate about its
lifetime/scope — a singleton registered incorrectly can leak state across tenants. See the
`orchardcore-tenants` skill for creating/inspecting tenants and feature profiles.

### Content pipeline

Content items are dynamic, schema-less documents composed of **Content Parts** and **Content Fields**
attached to a **Content Type** definition. Behavior is added via `ContentHandlerBase` subclasses (lifecycle
events: creating, created, publishing, published, etc.), **Content Part Drivers** (editor/display shape
building), and **Content Field editors**. Rendering goes through the Shapes/Display Management system
(`IShapeFactory`, `IDisplayManager`, `placement.json` per module) rather than direct Razor-to-model binding —
see the `orchardcore-display-management` skill before changing how something renders.

### Persistence

Data access goes through **YesSql** (a document-database abstraction over relational stores — SQLite,
SQL Server, MySQL, PostgreSQL) via `ISession`. Documents are POCOs; queryable projections are defined as
`MapIndex`/`ReduceIndex` classes with a matching `IndexProvider<TDocument>`, and are what you `Where(...)`
against instead of ad hoc SQL. Search/indexing (Lucene, Elasticsearch, Azure AI Search) is a separate,
pluggable layer on top of this — see `orchardcore-query-indexing`.

### Recipes

Declarative JSON "recipes" provision a tenant (enable features, create content types/items, set roles and
settings) and are executed by recipe step handlers (`IRecipeStepHandler`). Used for setup, module scaffolding
defaults, and deployment plans. See `orchardcore-recipe-creator`.

### Frontend assets

Module/theme front-end assets (SCSS/TS/JS/Vue) are declared per-project in `Assets.json` and built through
the Vite-based asset pipeline (`yarn build` / `yarn lint` / `yarn check` at the repo root). See
`orchardcore-asset-manager` for troubleshooting the pipeline itself.
