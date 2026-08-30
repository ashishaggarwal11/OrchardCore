---
description: Orchestrate planner/developer/validator agents to implement a requirement doc phase by phase
model: sonnet
argument-hint: [path-to-requirement.md]
---

Act as the orchestrator that turns a requirement document into working code, using the `planner`,
`developer`, and `validator` agents.

1. **Resolve the requirement file.**
   - If a path was supplied as `$ARGUMENTS`, use it.
   - Otherwise, ask the user for the path to the requirement markdown file. Do not guess or invent one.
   - Read the file. If it doesn't exist, report that clearly and stop.

2. **Derive the plan slug and locate the plan file.**
   - Build a kebab-case slug from the requirement filename (this must match the convention `planner` uses:
     `plan/<slug>.md`).
   - Check whether `plan/<slug>.md` already exists.

3. **Create or resume the plan.**
   - If `plan/<slug>.md` does not exist: call the `planner` agent with the full requirement content and the
     target slug, and have it create `plan/<slug>.md` with ordered, scoped phases, each starting `pending`.
   - If `plan/<slug>.md` already exists: read it directly to recover the current phases and their statuses.
     Do not re-invoke `planner` to regenerate phases that already exist.

4. **Drive each phase in order**, skipping any phase already marked `done`:
   - **Mark in-progress:** call `planner` and ask it to flip that phase's status to `in-progress` in the
     plan file. `planner` owns all edits to the plan file — never edit it yourself.
   - **Implement:** call the `developer` agent with just that phase's scope, files/areas, and acceptance
     criteria as recorded in the plan file. Capture its report (what changed, where, test coverage,
     code-checker status).
   - **Validate:** call the `validator` agent with that phase's acceptance criteria and the developer's
     reported output/diff.
   - **On pass:** call `planner` to mark the phase `done`, then move to the next phase.
   - **On fail:** relay validator's itemized findings back to `developer` for a fix-up pass, then
     re-validate. Repeat for up to 3 developer↔validator cycles for this phase.
   - **If still failing after 3 cycles:** call `planner` to mark the phase `blocked`, stop processing
     further phases, and report the unresolved validator findings to the user instead of proceeding.

5. **Report completion.** Once every phase is `done`, or the loop stopped early on a `blocked` phase, give
   the user a concise summary: which phases completed, any blocked phase and why, and the plan file path
   (`plan/<slug>.md`) for reference.

Constraints:
- Only `planner` ever writes to the plan file. Your job is to sequence calls to `planner`, `developer`, and
  `validator` and relay information between them.
- Give `developer` and `validator` only the single phase's data they need — never the whole plan file.
