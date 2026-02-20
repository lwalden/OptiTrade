# CLAUDE.md - Project Instructions

Claude reads this file automatically at session start.
Keep this file concise and authoritative.

## Required Reading Order

1. `PROGRESS.md`
2. `docs/VALIDATION_GATES.md`
3. `DECISIONS.md`
4. `docs/PROJECT_STRATEGY.md` (canonical plan and consistency matrix)
5. On-demand: architecture and phase docs

If no context is injected, read files manually in this order.

## Session Protocol

### Start

1. Read `PROGRESS.md` including gate scoreboard.
2. Run `git status`.
3. Resume from `Next Priorities`.

### During

- Write changes directly to files.
- Keep work in small, verifiable checkpoints.
- Run tests before commit when tests exist.
- Update docs whenever behavior changes.

### End

- Run `/handoff`.
- Ensure `PROGRESS.md` and gate scoreboard reflect the new state.

## Canonical Project Identity

**Project:** opti-trade (OptiMind)
**Type:** single-operator algorithmic options system
**Primary runtime:** Python 3.12+
**Backtesting language:** C# for QuantConnect LEAN only
**Dependency manager:** uv (locked)
**LLM runtime model:** `claude-sonnet-4-6` (locked unless ADR says otherwise)

## Canonical Planning Facts

- Phase 1 is paper-only and spans Weeks 1-10.
- First live trade is Phase 4 only (target window Month 8-10).
- 8-15% annual return is a planning hypothesis, not a committed result.
- Phase advancement is blocked unless `docs/VALIDATION_GATES.md` criteria are passed.

## Autonomy and Boundaries

Claude CAN autonomously:

- create and edit files,
- run builds/tests,
- create branches and PRs,
- add tests and scripts.

Claude MUST ask human before:

- changing live/paper mode to live,
- executing live trades,
- merging PRs,
- changing hard risk limits without explicit instruction.

## Financial Safety Rules

- Default mode is paper.
- Risk limits in `optimind/core/constants.py` are safety-critical.
- Risk framework updates require explicit rationale and ADR entry.

## Documentation Rules

- `docs/PROJECT_STRATEGY.md` is canonical strategy/roadmap.
- `docs/strategy-roadmap.md` is non-canonical pointer only.
- Use `DECISIONS.md` for all significant architecture/process changes. It is the durable record — if a session decision matters beyond the 3-note rolling window in `PROGRESS.md`, add an ADR before running `/handoff`.
- Use `PROGRESS.md` for durable state and gate scoreboard.

## Git and Branch Policy

- Feature branches get auto-committed at session end (hook: `.claude/hooks/session-end-commit.js`).
- `main` branch is intentionally excluded from auto-commit — changes on `main` must be committed manually by the human. This applies to docs-only changes as well.
- Always do implementation work on a feature branch, not on `main`.

## Context Budget

| File | Target Size | Rule |
|---|---|---|
| `CLAUDE.md` | short | Keep only operating rules and canonical facts |
| `PROGRESS.md` | short | Keep current state + gate scoreboard + top priorities |
| `DECISIONS.md` | medium | Keep active and superseded ADRs explicit |
