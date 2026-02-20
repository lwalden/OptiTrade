# CLAUDE.md - Project Instructions

> Claude reads this file automatically at the start of every session.
> Keep it concise -- every line costs context tokens.
>
> **Reading order:** PROGRESS.md first → DECISIONS.md before architectural choices → other docs on-demand.

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
- Run tests at key checkpoints and always before commits (when tests exist)

### Ending a Session
Run `/handoff` to write a clear briefing for the next session. Hooks handle timestamp and auto-commit automatically.

## Project Identity

**Project:** opti-trade
**Description:** A fully custom, AI-enhanced options trading system implementing Optionetics-style delta-neutral strategies (iron condors, butterflies, calendar spreads, straddles/strangles) with an LLM-powered intelligence layer for market regime detection, trade reasoning, and adaptive strategy selection. Trades on Interactive Brokers with paper/live toggle; targets 8-15% annual returns on $400K capital.
**Type:** other (algorithmic trading system)
**Stack:** Python 3.12+ / uv / ib_async / SQLAlchemy / Pydantic / pandas / py_vollib / Claude API / YAML config
**MCP Servers:** None currently active (available: Azure, Postman, Firecrawl, Hugging Face, GitHub, Miro, Microsoft Learn — start on demand)

**Developer Profile:**
- Senior developer, high experience level, full domain ownership (sole operator/user)
- Autonomy: aggressive — act freely, create branches/PRs/files without asking; skip corporate ceremony
- Notes and documentation oriented to single developer/owner/operator

## MVP Goals

Phase 1 deliverables (Weeks 1–8, paper mode only):
- IBKR connects via ib_async, paper/live toggle works via `OPTIMIND_MODE` env var
- Options chains retrieved for SPX/SPY/QQQ/IWM; Greeks within 5% of IBKR-provided values; IV rank from 52-week history
- Iron condor constructed and executed as 4-leg BAG combo with SmartPricing
- Position auto-exits at 50% profit target, 200% stop loss, and DTE thresholds (21/14/7)
- CLI functional (`status`, `scan`, `trade`, `close`, `mode`, `history`)
- One complete iron condor lifecycle (open → monitor → close) documented in paper mode

## Behavioral Rules

### Git Workflow
- **Never commit directly to main** -- always use feature branches
- Branch naming: `feature/short-description`, `fix/short-description`, `chore/short-description`
- All changes via PR. Claude creates PRs; human reviews and merges

### Credentials
- Never store credentials in code. Use `.env` files (gitignored).
- IB Gateway credentials, API keys, and broker tokens are always `.env` only

### Autonomy Boundaries
**You CAN autonomously:** Create files, install packages, run builds/tests, create branches and PRs, scaffold code, write and run tests
**Ask the human first:** Merge PRs, approve major architectural changes, execute live trades, change paper/live toggle

### Verification-First Development
- Write tests where useful (this is a high-impact personal tool -- quality matters)
- Run existing tests before commits
- Use pytest for Python tests

### Financial Safety
- Paper trading mode is the safe default; never switch to live without explicit human instruction
- Risk framework decisions are human-owned; Claude can propose but not unilaterally change risk limits

## Context Budget

| File | Target Size | Action if Exceeded |
|------|------------|-------------------|
| CLAUDE.md | ~80 lines | Don't add without removing something |
| PROGRESS.md | ~20 lines active | Self-trimming: only 3 session notes kept |
| DECISIONS.md | Grows over time | Delete superseded entries (git history preserves them) |

**Reading Strategy:**
- PROGRESS.md: Every session (auto-injected by hook)
- DECISIONS.md: Auto-injected if decisions exist; always check before architectural choices
- strategy-roadmap.md: On-demand
- docs/ARCHITECTURE.md, PHASE_*.md: On-demand for implementation context
