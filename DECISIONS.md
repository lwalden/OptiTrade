# DECISIONS.md - Architectural Decision Log

> Record significant decisions to prevent re-debating them later.
> Claude reads this before making architectural choices.
>
> **When to log:** choosing a library/framework, designing an API, selecting an auth approach, changing a data model, making a build/deploy decision.

---

## ADR Format

Format: Lightweight

---

## ADR-001: Python-primary stack
**Date:** 2026-02-19
**Decision:** Python 3.12+ as the sole language for all system components.
**Rationale:** Ecosystem alignment with ib_async, py_vollib, pandas, QuantConnect LEAN, and MCP servers. Fastest path to production.
**Alternatives:** C#/.NET (deeper expertise but ecosystem friction), Polyglot Python+C# (added operational complexity)

---

## ADR-002: Interactive Brokers as primary broker
**Date:** 2026-02-19
**Decision:** IBKR via ib_async as the primary and only broker for Phases 1–3.
**Rationale:** Best API for multi-leg options execution, Portfolio Margin at $110K minimum, $0.65/contract commissions, combo/BAG order support.
**Alternatives:** Tradier (simpler, commission-free, but weaker multi-leg execution), Alpaca (no multi-leg options support)

---

## ADR-003: System-wide paper/live toggle via single env var
**Date:** 2026-02-19
**Decision:** `OPTIMIND_MODE=paper|live` is the single control point. Entire system is paper OR live — no per-strategy or per-trade toggle.
**Rationale:** Simplest mental model; eliminates mixed-mode bugs. Paper vs. live distinction is purely port selection (4002 vs. 4001).
**Alternatives:** Per-strategy toggle (more flexibility, more complexity and risk of accidental live execution), per-trade approval (handled separately by guided execution mode)

---

## ADR-004: Full custom over Option Alpha + AAT combo
**Date:** 2026-02-19
**Decision:** Build a fully custom system rather than combining existing platforms.
**Rationale:** AI regime detection layer is the core differentiator. Calendar spreads and straddles required for full Optionetics strategy set. Portfolio-level risk management unavailable in any existing platform. Adaptive strategy weighting not possible without custom code.
**Alternatives:** OA+AAT combo (75% coverage, 0 dev time), OA+QuantConnect (80% coverage, 3–6 months dev)

---

## ADR-005: ib_async over ib_insync
**Date:** 2026-02-19
**Decision:** Use ib_async (community fork) instead of ib_insync or the native IBKR Python API.
**Rationale:** ib_insync creator passed away in 2024 and the library is unmaintained. ib_async is the actively maintained community fork under a new org, with the same interface.
**Alternatives:** Native IBKR Python API (harder to use, no async abstractions), ib_insync (unmaintained, no future fixes)

---

## ADR-006: SQLite for development, PostgreSQL for production
**Date:** 2026-02-19
**Decision:** SQLAlchemy ORM targeting SQLite in Phase 1/2/3 (local dev), migrating to Azure Database for PostgreSQL at production deployment.
**Rationale:** SQLite needs zero infrastructure setup; SQLAlchemy abstracts the dialect difference. Migration at Azure deployment is a one-time alembic migration.
**Alternatives:** PostgreSQL from day one (unnecessary complexity in dev), DuckDB (good for analytics but not transactional workloads)

---

## ADR-007: uv for dependency management
**Date:** 2026-02-19
**Decision:** Use uv (Astral) for Python dependency management and virtual environments.
**Rationale:** Significantly faster than pip/Poetry; built-in venv management; compatible with pyproject.toml PEP 621 standard; growing ecosystem adoption.
**Alternatives:** Poetry (more ecosystem momentum, slower), pip+venv (no lock file, manual), pipenv (largely superseded)
