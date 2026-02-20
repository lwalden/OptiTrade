# Product Brief & Roadmap

> The "why" and "what" behind this project. Claude references this on-demand for decision context.
> Run `/plan` to fill this in interactively, or edit manually.

---

## What & Why

**Problem:** Off-the-shelf options platforms don't implement Optionetics-style delta-neutral strategies with intelligent, adaptive regime detection. Managing iron condors, butterflies, and calendar spreads across $100K-$150K requires discipline and real-time risk awareness that generic tools don't provide.
**Vision:** A personal algorithmic trading system that executes delta-neutral options strategies on Interactive Brokers with AI-assisted market regime detection and trade reasoning -- targeting 8-15% annual return at half-market volatility.
**Target Users:** Solo developer/operator (single user, high personal financial impact)

---

## MVP Features (Phase 1)

<!-- Populated by /plan -->

1. IB Gateway connection and paper trading toggle -- Acceptance: can connect, retrieve account info, and place a paper trade
2. Options chain data retrieval and Greeks calculation -- Acceptance: can fetch chain for any ticker, compute delta/gamma/theta/vega
3. Iron condor strategy scaffold -- Acceptance: can construct, validate, and submit a paper iron condor with correct leg sizing
4. Pre-trade risk validation -- Acceptance: rejects trades that violate position limits or max loss thresholds defined in risk config
5. Basic position monitoring -- Acceptance: tracks open positions and P&L in real-time from IB data feed

**Out of Scope (Phase 1):** Live trading (paper only), LLM intelligence layer, Tradier integration, automated exit management, web UI

---

## Technical Stack

| Component | Choice | Why |
|-----------|--------|-----|
| Language | Python 3.12+ | Ecosystem for quant finance; async support |
| Broker | Interactive Brokers via ib_async | Best API for retail options; async-native |
| Greeks | py_vollib | Industry-standard Black-Scholes implementation |
| ORM | SQLAlchemy | Flexible, battle-tested; supports SQLite (dev) and Postgres (prod) |
| Validation | Pydantic | Type-safe settings, trade models, risk config |
| Data | pandas | Options chain manipulation, time series |
| AI layer | Claude API | Market regime detection, trade reasoning (Phase 3) |
| Config | YAML | Human-readable strategy and risk limit files |

---

## Quality Tier

**Tier:** Rigorous
**Rationale:** Personal tool but high financial impact ($100K+ capital at risk). No corporate overhead but quality must be production-grade.
**Testing:**
- Unit tests for all strategy logic, risk calculations, Greeks math, and data transformations (pytest)
- Integration tests for IB Gateway adapter in paper mode
- Tests run at key checkpoints and always before commits
- No E2E automation tests (IB paper trading account serves as E2E validation)
- No CI/CD pipeline (single developer, local workflow)

---

## Phases

**Phase 1 (Foundation):** Core infrastructure -- IB connection, data layer, base strategy scaffold, risk engine, paper trading
**Phase 2 (Strategies):** Full strategy implementations -- iron condor, butterfly, calendar spread, straddle/strangle; automated entry/exit management
**Phase 3 (AI Layer):** LLM intelligence -- market regime detection, trade reasoning, adaptive strategy selection via Claude API
**Phase 4 (Production):** Live trading hardening -- Tradier integration, monitoring, alerting, fail-safes, live toggle

---

## Open Questions

<!-- TODO: Database choice (SQLite vs Postgres) | WHEN: Phase 1 start | BLOCKS: data layer design -->
<!-- TODO: MCP server for portfolio querying -- design scope | WHEN: Phase 3 | BLOCKS: AI layer integration -->

---

## Human Actions Needed

- IB Gateway paper trading account credentials in `.env` — Before Phase 1 testing
- Claude API key in `.env` — Before Phase 3

---

*Initialized by /setup | Last Updated: 2026-02-19*
