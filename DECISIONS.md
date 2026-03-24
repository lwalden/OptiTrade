# DECISIONS.md - Architectural Decision Log

Record significant decisions to prevent re-debating and documentation drift.

## ADR Format

- Status: active | superseded
- Date: YYYY-MM-DD
- Decision
- Rationale
- Alternatives considered

---

## ADR-001: Runtime and backtest language split
**Status:** active
**Date:** 2026-02-19
**Decision:** Use Python 3.12+ for production runtime; use C# only for LEAN backtests.
**Rationale:** Python aligns with runtime libraries (`ib_async`, `pydantic`, async ecosystem). C# is LEAN native and the fastest path for robust options backtests.
**Alternatives:** all-Python (LEAN feature lag), all-C# runtime (ecosystem friction for current runtime architecture).

## ADR-002: Interactive Brokers primary broker (Phases 1-3)
**Status:** active
**Date:** 2026-02-19
**Decision:** IBKR via `ib_async` is the primary broker for Phases 1-3.
**Rationale:** robust multi-leg options execution and mature API surface.
**Alternatives:** Tradier, Alpaca.

## ADR-003: System-wide paper/live toggle
**Status:** active
**Date:** 2026-02-19
**Decision:** `OPTIMIND_MODE=paper|live` controls the entire system mode.
**Rationale:** avoids mixed-mode execution mistakes.
**Alternatives:** per-strategy or per-trade mode toggles.

## ADR-004: Full custom build
**Status:** active
**Date:** 2026-02-19
**Decision:** Build a full custom system instead of stitching third-party automation tools.
**Rationale:** risk framework, adaptive weighting, and integration depth are first-class requirements.
**Alternatives:** OA+AAT combinations and partial platform stacks.

## ADR-005: `ib_async` over `ib_insync`
**Status:** active
**Date:** 2026-02-19
**Decision:** use maintained `ib_async`.
**Rationale:** maintained async IBKR wrapper.
**Alternatives:** native IBKR API, unmaintained `ib_insync`.

## ADR-006: SQLite dev -> PostgreSQL production
**Status:** active
**Date:** 2026-02-19
**Decision:** SQLAlchemy on SQLite in development; PostgreSQL for production.
**Rationale:** local simplicity with production migration path.
**Alternatives:** PostgreSQL from day one, DuckDB.

## ADR-007: uv dependency workflow
**Status:** active
**Date:** 2026-02-19
**Decision:** uv is the dependency and virtualenv manager.
**Rationale:** speed and simple workflow.
**Alternatives:** Poetry, pip+venv.

## ADR-008: hatchling backend
**Status:** active
**Date:** 2026-02-19
**Decision:** use hatchling in `pyproject.toml`.
**Rationale:** lightweight standard backend.
**Alternatives:** setuptools, flit, poetry-core.

## ADR-009: pydantic-settings + SecretStr
**Status:** active
**Date:** 2026-02-19
**Decision:** use `pydantic-settings` with `OPTIMIND_` prefix and `SecretStr` for secrets.
**Rationale:** type-safe config and secret-safe serialization.
**Alternatives:** dotenv + manual parsing, dynaconf.

## ADR-010: Performance target framing
**Status:** active
**Date:** 2026-02-20
**Decision:** 8-15% annual return is a hypothesis until validation gates pass.
**Rationale:** prevents planning from being treated as proven edge.
**Alternatives:** treat target as committed KPI before evidence.

## ADR-011: Gate-first phase progression
**Status:** active
**Date:** 2026-02-20
**Decision:** no phase advancement without passing `docs/VALIDATION_GATES.md`.
**Rationale:** reduces build-before-proof risk.
**Alternatives:** calendar-only progression.

## ADR-012: LLM model lock
**Status:** active
**Date:** 2026-02-20
**Decision:** lock runtime default to `claude-sonnet-4-6` unless changed by ADR.
**Rationale:** cost/performance consistency and reproducibility.
**Alternatives:** ad hoc model switching.

## ADR-013: Canonical planning source
**Status:** active
**Date:** 2026-02-20
**Decision:** `docs/PROJECT_STRATEGY.md` is canonical. `docs/strategy-roadmap.md` is pointer-only.
**Rationale:** remove duplicate planning documents drifting out of sync.
**Alternatives:** dual-maintained roadmap docs.

## ADR-014: Cost model scenarios
**Status:** active
**Date:** 2026-02-20
**Decision:** ROI reporting must use low/base/high cost scenarios from `docs/COST_MODEL.md`.
**Rationale:** avoids optimistic single-point estimates.
**Alternatives:** single monthly cost number.

## ADR-016: MCP server selection
**Status:** active
**Date:** 2026-02-21
**Decision:** Add GitHub and DB Hub MCP servers to `.mcp.json` now. Defer Hugging Face to a Phase 3 entry evaluation. Skip Azure, Postman, Firecrawl, Microsoft Learn, and vscode-mcp-server.
**Rationale:** GitHub MCP enables PR/issue management directly in Claude without context switching. DB Hub enables direct DB introspection for SQLite (dev) and PostgreSQL (prod), aligned with ADR-006. Hugging Face deferred because the AI layer is Phase 3 scope and its value depends on measurable gaps the baseline regime classifier cannot close — decision criteria documented in `docs/PHASE_3_AI_LAYER.md`. Skipped servers have no dependency in the current or planned stack.
**Alternatives considered:** All eight candidate servers evaluated; six rejected as redundant or out of scope (see session notes 2026-02-21).

## ADR-017: GitHub MCP server endpoint
**Status:** active
**Date:** 2026-02-21
**Decision:** Use the official remote HTTP endpoint `https://api.githubcopilot.com/mcp/` (GitHub-hosted, Go binary) instead of the deprecated npm package `@modelcontextprotocol/server-github`.
**Rationale:** The npm package is deprecated; development moved to `github/github-mcp-server` (Go). The remote HTTP endpoint requires no Docker or local build, is always current, and authenticates via PAT bearer token header — the simplest path to a maintained server.
**Alternatives considered:** Local Docker (`ghcr.io/github/github-mcp-server`), building Go binary from source, keeping deprecated npm package.

## ADR-018: Iron condor wing width 5pt → 10pt, sizing 1 → 5 contracts
**Status:** active
**Date:** 2026-03-03
**Decision:** Increase SPY iron condor wing width from 5 points to 10 points, and default sizing from 1 contract to 5 contracts per spread.
**Rationale:** Phase 1 gate backtesting (112 trades, 2019-2025) showed the strategy is profitable on a per-trade basis (68.75% win rate, profit factor 1.33) but fails the slippage drag gate (42.83% vs ≤30% threshold) and CAGR gate (-0.57% vs >0%). Root cause: at 5-point wings, gross credit per trade (~$50-75 gross P&L target) is too thin relative to fixed friction ($0.65 commission × 4 legs × N contracts). Widening to 10-point wings roughly doubles collected credit (~$2.00 vs ~$1.00) while increasing max risk proportionally. Scaling to 5 contracts multiplies gross P&L by 5× while friction scales proportionally — the combination brings projected slippage drag to ~14%, well below the 30% threshold, and should turn CAGR positive. The `max_contracts: 5` cap in strategies.yaml already anticipated this sizing.
**Alternatives considered:** Keeping 5-point wings and reducing commission model (not realistic — IBKR charges are fixed); switching to SPX (100× multiplier solves friction mathematically but changes risk profile and capital requirements significantly, deferred to Phase 2); alternative data sources (assessed — data gaps were algorithm bugs, not missing data).

## ADR-019: n8n for operations automation (Phase 2+)
**Status:** active
**Date:** 2026-03-14
**Decision:** Use n8n (self-hosted, local for dev, Azure for prod) as the operations automation layer for trade notifications, reporting, health monitoring, and gate evaluation. Uses built-in Anthropic Chat Model node for Claude API integration. Workflow JSON version-controlled in `d:\Source\n8n-automation-hub` repo.
**Rationale:** OptiMind's event-driven architecture produces events (trade execution, risk alerts, position updates) that need external notification and reporting. n8n provides visual workflow orchestration with webhook triggers, cron scheduling, and direct Claude API integration without adding Python dependencies to the trading runtime. Keeps the core trading loop clean (Python/async) while offloading ops concerns to a dedicated workflow engine. The Gate Evaluation Runner workflow is useful immediately in Phase 1 for automating gate script execution and report delivery.
**Alternatives considered:** Custom Python scripts for each notification (tight coupling, no visual debugging), Prefect/Airflow (heavyweight for this use case), manual operations (does not scale with absentee-owner model).

**Planned workflows:**

| Workflow | Trigger | Phase |
|----------|---------|-------|
| Gate Evaluation Runner | Manual | Phase 1 (now) |
| Trade Alerts | Webhook (from OptiMind events) | Phase 2 |
| Daily P&L Report | Cron (market close + 30min) | Phase 2 |
| Risk Alert Escalation | Webhook (from risk monitor) | Phase 2 |
| IBKR Health Monitor | Cron (1min during market hours) | Phase 2 |
| Market Regime Monitor | Cron (daily pre-market) | Phase 3 |

## ADR-015: Sprint 1.1 implemented before Sprint 1.0
**Status:** active
**Date:** 2026-02-20
**Decision:** Implement Sprint 1.1 (Python scaffold and IBKR connection) before Sprint 1.0 (LEAN backtest), despite the phase doc listing 1.0 first.
**Rationale:** Building the Python runtime scaffold first reduces the risk of misaligned assumptions in the LEAN backtest. Understanding IBKR connectivity, data models, and configuration structure before writing C# backtest logic leads to better parity between backtest and runtime. Sprint numbers reflect dependency priority (backtest viability is the strategic prerequisite), not implementation order.
**Alternatives:** strict sequential order matching sprint numbers; skipping scaffold until after backtest viability confirmed.

---

## Project State Snapshot | 2026-02-21 | Migrated from PROGRESS.md

### Phase Gate Scoreboard

| Gate | Status | Notes |
|---|---|---|
| Phase 1 Strategy Viability | in_progress | YAML configs done; LEAN backtest and gate evaluator not yet implemented |
| Phase 2 Execution and Risk | pending | blocked on Phase 1 gate |
| Phase 3 AI Value | pending | blocked on Phase 2 gate |
| Phase 4 Live Readiness | pending | blocked on Phase 1-3 gates |

### Next Steps (as of last active session)

1. Review and merge PR #8 (`feature/sprint-1.0-config-yaml`) — YAML configs for Sprint 1.0
2. Implement `scripts/generate_lean_config.py` — reads `optimind/config/strategies.yaml`, emits `backtests/lean/Config/StrategyConstants.cs`, prints SHA256 `parameter_hash`
3. Implement `scripts/evaluate_phase1_gate.py` — validates `backtests/lean/results/phase1_baseline.json` against Phase 1 gate criteria
4. Scaffold `backtests/lean/` C# LEAN algorithm structure (Algorithm/, Config/, lean.json)

---

## Known Debt

| ID | Description | Impact | Logged |
|---|---|---|---|
| KD-001 | `scripts/generate_lean_config.py` not implemented | Blocks Phase 1 gate evaluation | 2026-02-21 |
| KD-002 | `scripts/evaluate_phase1_gate.py` not implemented | Blocks Phase 1 gate pass | 2026-02-21 |
| KD-003 | LEAN backtest scaffold not yet built | Blocks backtest viability validation | 2026-02-21 |
