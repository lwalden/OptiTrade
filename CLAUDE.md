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

### Starting a Session
1. Read `PROGRESS.md` -- understand current state, active tasks, and priorities
2. Check `git status` for uncommitted work
3. Resume from "Next Priorities" in PROGRESS.md

> If no session context was injected above (you don't see PROGRESS.md content), read PROGRESS.md and DECISIONS.md manually before proceeding.

### During a Session
- Write code to files immediately -- don't accumulate changes in memory
- Commit at natural checkpoints (compiles, tests pass, logical unit complete)
- Prefer smaller, frequent commits over one large commit
- Use Claude's native Tasks for complex multi-step work; keep PROGRESS.md as the durable record

### Ending a Session
Run `/handoff` to write a clear briefing for the next session. Hooks handle timestamp and auto-commit automatically.

## Behavioral Rules

### Git Workflow
- **Never commit directly to main** -- always use feature branches
- Branch naming: `feature/short-description`, `fix/short-description`, `chore/short-description`
- All changes via PR. Claude creates PRs; human reviews and merges

### Credentials
- Never store credentials in code. Use `.env` files (gitignored).

### Autonomy Boundaries
**You CAN autonomously:** Create files, install packages, run builds/tests, create branches and PRs, scaffold code
**Ask the human first:** Create GitHub repos, merge PRs, sign up for services, provide API keys, approve major architectural changes

### Verification-First Development
- Confirm requirements before implementing
- Write tests appropriate to the project's quality tier (see strategy-roadmap.md)
- When Standard tier or above: write failing tests first, then implement

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

| File | Target Size | Action if Exceeded |
|------|------------|-------------------|
| CLAUDE.md | ~75 lines | Don't add without removing something |
| PROGRESS.md | ~20 lines active | Self-trimming: only 3 session notes kept |
| DECISIONS.md | Grows over time | Delete superseded entries (git history preserves them) |

**Reading Strategy:**
- PROGRESS.md: Every session (auto-injected by hook)
- DECISIONS.md: Auto-injected if decisions exist; always check before architectural choices
- strategy-roadmap.md: On-demand
