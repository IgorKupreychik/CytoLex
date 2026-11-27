---
name: next-task
description: Pick the next startable task from TASKS.md (or the task ID passed as an argument, e.g. T-012) and work on it, starting in plan mode. Use when the user types /next-task, or asks to start, continue or pick up the next task.
argument-hint: "[task-id]"
---

# next-task

Works on one task from `TASKS.md` at the repo root. Arguments: `$ARGUMENTS` (optional task ID such as `T-012`).

## 1. Select the task
1. Read `TASKS.md`: the **Summary** table, plus the chosen task's `### T-xxx — …` section.
2. If a task ID was given, use it. Check that it exists and isn't already `done`. If any of its **Depends** tasks isn't `done`, list the blocking tasks and their statuses, then ask whether to proceed anyway or pick another task. Don't silently override.
3. With no ID: choose the first task whose status is `todo` or `discussion` and whose **Depends** are all `done`. Order by **Pri** (1 first), then by ID. Ignore `in_progress` and `blocked` tasks.
   - If any task is already `in_progress`, mention it and ask whether to resume it instead.
   - If nothing is startable, say so and list the closest candidates with their blockers.
4. Tell the user which task was picked and why, in one or two lines.
5. **No ID given → confirm before starting.** Ask with `AskUserQuestion` whether to proceed with the picked task. Name its ID, title, status and Pri in the question. Offer "Proceed with T-xxx (Recommended)" first, and up to two other startable tasks (next by Pri, then ID) as alternatives. The user can also type another ID under "Other". Do nothing else for the task (no sign-off, plan mode or status change) until they answer. If they pick another task, select it as if its ID had been given (step 2). If they decline, stop. When an ID was given, don't ask again: that is the confirmation.

## 2. Sign-off first (before planning)
If the task's **Notes** ask for a person sign-off (tables or figures with `review.human: pending` in `curated/extract/tables/<version>.yaml`), do it **before** entering plan mode. The plan depends on the signed-off data.
1. Explain what the user is checking: the extracted table (chunk in `kb/source-text/`) must match the printed table. That means the same rows in the same order, each cell in its right column, and no missing or garbled characters. Explain why this matters (the KB and the parser rely on it).
2. Use the review images in `build/review/tables/<version>/`. If they're missing or stale, ask the user to run `uv run --directory tools kb tables`.
3. Go **one table at a time, page by page**. Show the image path, and show the extracted rows as a numbered table. Compare them yourself first and report any differences, row counts and oddities.
4. Get an explicit confirmation per table (`ok` or `mismatch`). On a mismatch, apply the backup plan in `docs/extraction-review-<version>.md`.
5. Record `review.human: ok` in the layout file and the sign-off date in `docs/extraction-review-<version>.md` (and remove the table from its `needs_review` list). Then ask the user to run `uv run --directory tools kb extract --version <version>` and wait until they have.

## 3. Plan (always in plan mode)
- Call `EnterPlanMode` (load it via ToolSearch if it's deferred) before doing anything else for the task.
- Read only what the task needs:
  - `docs/decisions.md` for relevant earlier decisions.
  - `CLAUDE.md`.
  - The specific files or KB chunks the task names.
- For KB tasks, read the extracted chunks under `kb/source-text/`, never the whole PDF.
- **`discussion` task:** run an interview with **one question per message**, using `AskUserQuestion`. Put the recommended option first, marked "(Recommended)". If the user wants to clarify a question instead of answering it, ask them in plain text what they'd like to clarify. The plan is the resulting decision plus where it gets recorded.
- **`todo` task:** write a plan that meets every acceptance criterion in the task's section.
- Finish with `ExitPlanMode` for approval.

## 4. Execute
1. On approval, set the task's status to `in_progress`, in **both** the Summary table and the task section.
2. Do the work.
   - A `discussion` task records the decision (with a short rationale and the date) in `docs/decisions.md`, and updates `CLAUDE.md` if it changes how to work in the repo.
   - Any task that changes commands, layout, setup or project status updates `CLAUDE.md` **and** `README.md` together.
3. Check each acceptance criterion and report it as ✅ or ❌, with evidence (command output, file paths).
4. New open questions or follow-up work: add them to `TASKS.md` as new tasks with the next free ID (`discussion` for decisions, `todo` for work), with Pri, Depends and a section. Mention them to the user.

## 5. Finish
- List any commands the user must run now (e.g. `uv sync --directory tools`, `kb extract`, `kb bands`, `kb build`) and why; see README.md → "When to run what".
- Ask the user to confirm the task is done. Only after they confirm:
  - Set status `done` in both places.
  - Write a short outcome in the task's **Notes**: what was produced, which files, and any deviations from the plan.
- If they don't confirm, leave it `in_progress` (or set it to `blocked`, with the reason in Notes).
- **Never commit.** Suggest a commit message, e.g. `T-002: source-text extraction script`.
- Finally, name the next startable task, so the user can run `/next-task` again.

## Rules
- Keep the Summary table and the task sections consistent. Every **Depends** ID must exist.
- Don't change the IDs, priorities or dependencies of other tasks unless the user asks. You may *propose* changes.
- Never commit the ISCN PDF or `kb/source-text/` (both are gitignored). The PDF path comes from `config/local.yaml`.
