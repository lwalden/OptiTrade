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
