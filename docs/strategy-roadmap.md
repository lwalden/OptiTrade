# OptiMind: Strategy Roadmap
**Project:** opti-trade (codename: OptiMind)
**Owner:** Laurance
**Created:** 2026-02-19
**Status:** Phase 1 — in development

---

## What & Why

**Vision:** A fully custom, AI-enhanced options trading system implementing Optionetics-style delta-neutral strategies (iron condors, butterflies, calendar spreads, straddles/strangles) with an LLM-powered intelligence layer for market regime detection, trade reasoning, and adaptive strategy selection. Trades on Interactive Brokers with a paper/live toggle. Targets **8–15% annual returns** on $100K–$150K deployed capital at half-market volatility.

**Target Users:** Solo developer/operator (Laurance). Single user, high personal financial impact.

**Why build this instead of using existing platforms:**

| Gap in existing platforms | Why it matters |
|---|---|
| No LLM market regime detection | AI layer IS the edge — adapts strategy mix to conditions |
| No calendar spreads or straddles | Full Optionetics toolkit requires these |
| No portfolio-level Greek limits | Prevents correlated blowups that destroy accounts |
| No correlation-aware position limits | 3 "uncorrelated" iron condors can all fail together |
| No MCP integration for Claude | Natural-language portfolio review |
| No adaptive strategy weighting | Static allocation underperforms regime-aware |
| No custom adjustment logic | Optionetics "transform, don't close" philosophy |

---

## Out of Scope (Forever)

- Equity/stock trading — options only
- High-frequency or intraday scalping
- Market-making or quoting
- Managing accounts for other people
- Mobile app — CLI + web dashboard is sufficient
- Cryptocurrency

---

## Phase 1: Foundation & First Trade (Weeks 1–8)

**Goal:** Connect to IBKR, execute a single iron condor lifecycle in paper mode.

**MVP Features:**

1. IBKR connection via ib_async (paper port 4002, live port 4001, toggle via `OPTIMIND_MODE`) — Acceptance: connects, heartbeats, auto-reconnects on drop
2. Options chain retrieval for SPX/SPY/QQQ/IWM — Acceptance: returns chains filtered by DTE range and delta target within pacing limits
3. Greeks calculation (py_vollib + IBKR validation) + IV rank — Acceptance: delta/gamma/theta/vega within 5% of IBKR-provided values; IV rank calculated from 52-week history
4. Iron condor construction and combo order execution — Acceptance: 4-leg BAG order placed as single combo, SmartPricing walks from mid toward natural in 60s intervals
5. Position monitoring + auto-exit — Acceptance: closes at 50% profit target, 200% stop loss, and at DTE thresholds (21/14/7 days)
6. CLI interface (`status`, `scan`, `trade`, `close`, `mode`, `history`) — Acceptance: all commands functional in paper mode
7. Full lifecycle in paper mode: scan → select → execute → monitor → close — Acceptance: one complete iron condor cycle documented

**Out of Scope (Phase 1):** Live trading, LLM intelligence layer, Tradier integration, web UI, calendar spreads, straddles

**Quality tier:** Standard — unit tests for risk calculations and strategy logic; integration smoke tests for broker adapter; hypothesis-based property tests for risk bounds

**Sprint breakdown:**
- Sprint 1.1 (Weeks 1–2): Python scaffold, IBKR connection, paper/live toggle
- Sprint 1.2 (Weeks 3–4): Options chains, Greeks, IV rank, scanner
- Sprint 1.3 (Weeks 5–6): Iron condor construction, combo orders, SmartPricing
- Sprint 1.4 (Weeks 7–8): Position monitoring, exit logic, CLI, Phase 1 integration test

**Monthly cost:** $5–10 (IBKR market data, nonprofessional)

---

## Phase 2: Strategy Engine & Risk Layer (Weeks 9–18)

**Goal:** Multi-strategy support, full risk management, position adjustment logic, guided-execution mode.

**Key deliverables:**
- Strategy registry with iron condors, butterflies, credit spreads (bull put / bear call)
- Full pre-trade risk check suite (all hard limits from `core/constants.py` enforced)
- Portfolio-level Greeks monitoring (delta, gamma, vega aggregation across all positions)
- Circuit breakers: 3% NLV daily halt / 5% NLV emergency close-all
- Adjustment engine: rolling positions, transforming iron condors → butterflies
- Guided execution mode: scan → notify → human approves via `optimind approve <id>`
- Threat detection: GREEN / YELLOW / RED position status
- Sector correlation enforcement (max 3 positions per sector)
- Event-driven architecture operational: full event bus with all TRADE_* / POSITION_* events

**Exit criteria:** 3+ concurrent strategies in paper mode for 4+ weeks, zero risk limit violations.

---

## Phase 3: AI Intelligence Layer (Weeks 19–28)

**Goal:** Claude API integration for regime detection and trade reasoning; MCP server for portfolio queries; adaptive strategy weighting.

**Key deliverables:**
- Market regime engine: quantitative rule-based + Claude API cross-check (runs 2x/day at 10:15 AM / 2:00 PM ET)
- Regime-adaptive strategy weighting (iron condors favored in high-IV; butterflies in low-IV trending)
- Trade rationale generation (AI explains why a trade is proposed)
- AI portfolio review on demand
- MCP server: `get_positions`, `get_regime`, `discuss_trade`, `portfolio_summary`
- Calendar spreads and straddles/strangles added to strategy registry
- ORATS data integration for historical IV surface (optional)

**Exit criteria:** AI regime detection demonstrably improves strategy selection vs. static allocation over 8+ weeks paper comparison.

---

## Phase 4: Backtesting, Optimization & Production (Weeks 29–40+)

**Goal:** Validate with backtesting; deploy to Azure; first live trade.

**Key deliverables:**
- QuantConnect LEAN backtesting (Python API, 20+ years options data)
- Backtest vs. paper trading comparison (target: within 2% CAGR)
- Performance analytics dashboard (Streamlit)
- Tradier broker adapter (second broker)
- Azure VM deployment (Ubuntu 24.04, systemd service, IBC for IB Gateway auto-restart)
- SQLite → PostgreSQL migration
- Tax reporting: Section 1256 (60/40), wash sale detection, lot tracking
- First live trade execution (human approves explicit `OPTIMIND_MODE=live` toggle)

**Exit criteria:** System deployed to Azure; live trade executed; 8–15% annualized return trajectory established.

---

## Risk Parameters (Hard-Coded, Non-Negotiable)

These live in `optimind/core/constants.py` — not in YAML config — to prevent accidental override.

```
MAX_RISK_PER_TRADE_PCT       = 2.5%   of NLV
MAX_DEPLOYED_CAPITAL_PCT     = 40%    of NLV
MAX_POSITIONS_PER_UNDERLYING = 2
MAX_SECTOR_POSITIONS         = 3
DAILY_LOSS_HALT_PCT          = 3%     → halt new entries
DAILY_LOSS_EMERGENCY_PCT     = 5%     → close all positions, manual restart required
WEEKLY_LOSS_LIMIT_PCT        = 5%
MONTHLY_LOSS_LIMIT_PCT       = 10%
PORTFOLIO_DELTA_LIMIT_PCT    = ±10%   of NLV (alert at ±7%)
MAX_MARGIN_UTILIZATION_REGT  = 60%
MAX_MARGIN_UTILIZATION_PM    = 40%
MAX_ADJUSTMENTS_PER_POSITION = 2
```

---

## Technology Stack

| Component | Choice | Rationale |
|---|---|---|
| Language | Python 3.12+ | Ecosystem alignment (ib_async, py_vollib, QC LEAN) |
| Broker API | ib_async | Async-native IBKR wrapper; maintained community fork |
| Options pricing | py_vollib + QuantLib-Python | Greeks, IV calc, vol surface |
| Data storage | SQLite (dev) → PostgreSQL (prod) | Start simple, migrate when deploying |
| AI/LLM | Claude API (claude-sonnet-4-6) | Regime analysis, trade rationale, portfolio review |
| Backtesting | QuantConnect LEAN | Institutional-grade options backtesting |
| MCP Server | Custom Python MCP server | Portfolio query via Claude Desktop/Code |
| Hosting | Local dev → Azure VM D2s v5 (prod) | IB Gateway runs locally; Azure for 24/7 |
| Dashboard | Streamlit (MVP) → React (later) | Fast iteration for monitoring UI |
| Dep mgmt | uv or Poetry | TBD at scaffold time |
| Linting | ruff + mypy | Fast linting + type safety |
| Testing | pytest + pytest-asyncio + hypothesis | Standard Python testing stack |

---

## Success Criteria

1. **Phase 1:** Full iron condor lifecycle (open → monitor → close) in paper mode within 8 weeks
2. **Phase 2:** 3+ concurrent strategies, 4+ weeks paper, zero risk violations
3. **Phase 3:** AI regime detection improves strategy selection vs. static over 8+ weeks
4. **Phase 4:** Backtest within 2% CAGR of paper; live deployment; first live trade
5. **Overall:** 8–15% annualized return, max drawdown under 8%, over first 6 live months

---

## Open Questions

<!-- RESOLVED: uv for dependency management (ADR-007) -->
<!-- TODO: Streamlit vs Gradio for Phase 1 dashboard | WHEN: Phase 4 | BLOCKS: dashboard design -->

---

## Human Actions Needed

- IB Gateway paper trading account credentials in `.env` — Before Phase 1 Sprint 1.1 testing
- Claude API key in `.env` — Before Phase 3

---

*Last Updated: 2026-02-19 | Generated by /plan from PROJECT_STRATEGY.md + PHASE_1_FOUNDATION.md + ARCHITECTURE.md*
