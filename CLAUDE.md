# CLAUDE.md - Project Instructions

Claude reads this file automatically at session start.
Keep this file concise and authoritative.

## Session Orientation

At session start: check `git status`, read `DECISIONS.md` for architectural context, read `docs/VALIDATION_GATES.md` for gate state. Use `claude --continue` to restore prior session history.

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

**CAN autonomously:** create/edit files, install packages, run builds/tests, create branches and PRs, scaffold code, add tests and scripts, install and use CLI tools, query cloud services and APIs.

**Only when explicitly asked:** Merge PRs.

**MUST ask human before:** changing mode to live, executing live trades, changing hard risk limits, creating GitHub repos, signing up for services, providing API keys, approving major architectural changes.

**Tool-first rule:** See `.claude/rules/tool-first.md` — never ask the user to do something you can do with a tool.

## Financial Safety Rules

- Default mode is paper.
- Risk limits in `optimind/core/constants.py` are safety-critical.
- Risk framework updates require explicit rationale and ADR entry.
- Never store credentials in code. Use `.env` files (gitignored).

## Documentation Rules

- CLAUDE.md target is ~65 lines. See `docs/PROJECT_STRATEGY.md` §Context Budget before adding content here.
- `docs/PROJECT_STRATEGY.md` is canonical strategy/roadmap.
- `docs/strategy-roadmap.md` is non-canonical pointer only.
- Use `DECISIONS.md` for all significant architecture/process changes. It is the durable record — add an ADR before running `/aam-handoff` when a decision matters beyond the current session.
- Gate scoreboard and next priorities are tracked in `DECISIONS.md` (Project State Snapshot section).

## Git and Branch Policy

- Never commit directly to main — always use feature branches.
- Branch naming: `feature/`, `fix/`, `chore/` prefix.
- All changes via PR — Claude creates, human reviews and merges.
- See `.claude/rules/git-workflow.md` for commit and branch discipline.
