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

### Ending a Session
Run `/handoff` to write a clear briefing for the next session. Hooks handle timestamp and auto-commit automatically.

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

## Autonomy Boundaries

**CAN autonomously:** create/edit files, install packages, run builds/tests, create branches and PRs, scaffold code, add tests and scripts.

**MUST ask human before:** changing mode to live, executing live trades, merging PRs, changing hard risk limits, creating GitHub repos, signing up for services, providing API keys, approving major architectural changes.

## Financial Safety Rules

- Default mode is paper.
- Risk limits in `optimind/core/constants.py` are safety-critical.
- Risk framework updates require explicit rationale and ADR entry.
- Never store credentials in code. Use `.env` files (gitignored).

## Documentation Rules

- `docs/PROJECT_STRATEGY.md` is canonical strategy/roadmap.
- `docs/strategy-roadmap.md` is non-canonical pointer only.
- Use `DECISIONS.md` for all significant architecture/process changes. It is the durable record — if a session decision matters beyond the 3-note rolling window in `PROGRESS.md`, add an ADR before running `/handoff`.
- Use `PROGRESS.md` for durable state and gate scoreboard.

## Git and Branch Policy

- Never commit directly to main — always use feature branches.
- Branch naming: `feature/`, `fix/`, `chore/` prefix.
- All changes via PR — Claude creates, human reviews and merges.
- Feature branches auto-committed at session end (hook: `.claude/hooks/session-end-commit.js`).
- `main` is excluded from auto-commit — human commits main manually. This applies to docs-only changes.
