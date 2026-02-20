# OptiMind: AI-Powered Options Trading System
## Project Strategy & Development Roadmap

**Project Codename:** OptiMind
**Owner:** Laurance
**Created:** 2026-02-19
**Status:** Planning
**Target Production:** Phase 1 live trading by Month 4, full system by Month 10-12

---

## Vision

A fully custom, AI-enhanced options trading system implementing Optionetics-style delta-neutral strategies (iron condors, butterflies, calendar spreads, straddles/strangles) with an LLM-powered intelligence layer for market regime detection, trade reasoning, and adaptive strategy selection. The system trades on Interactive Brokers with a system-wide paper/live toggle and targets 8-15% annual returns on $400K deployed capital with half-market volatility.

## Why Full Custom Over Existing Platforms

| Gap in existing platforms | Why it matters |
|---|---|
| No LLM market regime detection | The AI layer IS the edge — adapting strategy mix to market conditions |
| No calendar spreads or straddles | Optionetics used the full toolkit, not just iron condors |
| No portfolio-level Greek limits | Prevents correlated blowups that destroy accounts |
| No correlation-aware position limits | Without this, 3 "uncorrelated" iron condors can all fail together |
| No MCP integration for Claude | Natural-language portfolio review and trade discussion |
| No adaptive strategy weighting | Static allocation underperforms regime-aware allocation |
| No custom adjustment logic | The Optionetics "transform, don't close" philosophy requires programmable adjustments |

## Technical Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        OptiMind System                          │
│                                                                 │
│  ┌──────────┐   ┌───────────┐   ┌──────────┐   ┌────────────┐  │
│  │  Data     │──▶│  AI       │──▶│  Signal  │──▶│  Risk      │  │
│  │  Ingest   │   │  Analysis │   │  Engine  │   │  Manager   │  │
│  └──────────┘   └───────────┘   └──────────┘   └─────┬──────┘  │
│       │              │                                │         │
│       │         ┌────┴────┐                    ┌──────▼──────┐  │
│       │         │  Claude │                    │  Execution  │  │
│       │         │  API    │                    │  Layer      │  │
│       │         └─────────┘                    │ ┌─────────┐ │  │
│       │                                        │ │  PAPER  │ │  │
│  ┌────▼─────┐   ┌───────────┐                  │ │   or    │ │  │
│  │  IB      │   │  Position │◀─────────────────│ │  LIVE   │ │  │
│  │  Gateway │   │  Monitor  │                  │ └─────────┘ │  │
│  └──────────┘   └───────────┘                  └─────────────┘  │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                   MCP Server Layer                        │   │
│  │  Portfolio Query │ Trade Discussion │ Risk Dashboard       │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## Technology Stack

| Component | Choice | Rationale |
|---|---|---|
| **Primary language** | Python 3.12+ | Fastest path to production; ecosystem alignment (ib_async, py_vollib, pandas, QuantConnect) |
| **Broker API** | ib_async (successor to ib_insync) | Best Python wrapper for IBKR; async-native; combo order support |
| **Options pricing** | py_vollib + QuantLib-Python | Greeks, IV calculation, volatility surface modeling |
| **Data storage** | SQLite (dev) → PostgreSQL (prod) | Start simple, migrate when needed; Azure SQL as option |
| **AI/LLM** | Claude API (claude-sonnet-4-5) | Market regime analysis, trade rationale, portfolio review |
| **Backtesting** | QuantConnect LEAN (C# native) | Institutional-grade options backtesting with 20+ years data; C# is LEAN's native language — authored in Phase 1 Sprint 1.0 to validate strategies before the execution layer is built |
| **Config sync** | Build-time Config Translator (`scripts/generate_lean_config.py`) | Python script reads `config/strategies.yaml` and writes `backtests/lean/Config/StrategyConstants.cs` — single source of truth, eliminates configuration drift between C# backtests and Python runtime |
| **MCP Server** | Custom Python MCP server | Portfolio query, trade discussion via Claude Desktop/Code |
| **Hosting** | Local dev → Azure VM (prod) | IB Gateway runs locally; Azure for 24/7 production |
| **Dashboard** | Streamlit or Gradio (MVP) → React (later) | Fast iteration for monitoring UI |
| **CI/CD** | GitHub Actions | Automated testing, deployment |
| **Project governance** | AIAgentMinder | Session continuity, decision tracking, lifecycle hooks |

## Capital & Account Setup

- **Broker:** Interactive Brokers (existing or new account)
- **Account type:** Individual margin account with Portfolio Margin ($110K minimum)
- **Options approval:** Level 3+ (spreads, iron condors, butterflies)
- **Initial capital:** $400K
- **Paper trading port:** 7497 (TWS) or 4002 (IB Gateway)
- **Live trading port:** 7496 (TWS) or 4001 (IB Gateway)
- **System-wide toggle:** Single environment variable `OPTIMIND_MODE=paper|live` controls all execution

## Risk Parameters (Non-Negotiable Hard Limits)

These are coded as constants, not configurable parameters:

| Limit | Value | Enforcement |
|---|---|---|
| Max risk per trade | 2.5% of NLV ($10,000 at $400K) | Pre-trade check, order rejected if exceeded |
| Max total deployed capital | 40% of NLV | Pre-trade check |
| Max positions per underlying | 2 | Pre-trade check |
| Max correlated positions | 3 in same sector | Pre-trade check with sector mapping |
| Daily loss circuit breaker | 3% of NLV → halt new entries | Continuous monitoring |
| Daily loss emergency stop | 5% of NLV → close all positions | Continuous monitoring, requires manual restart |
| Weekly loss limit | 5% of NLV | Monday reset |
| Monthly loss limit | 10% of NLV | Monthly reset |
| Portfolio delta limit | ±10% of NLV | Continuous monitoring, alert at ±7% |
| Max margin utilization | 60% of available (Reg-T) / 40% (PM) | Pre-trade check |

---

## Development Phases

### Phase 1: Foundation, Backtesting & First Trade (Weeks 1-10)
**Goal:** Validate iron condor strategy parameters via QuantConnect LEAN C# backtests (Sprint 1.0) BEFORE building the execution layer. Then connect to IBKR, execute a single iron condor in paper mode, with paper/live toggle working.

### Phase 2: Strategy Engine & Risk Layer (Weeks 9-18)
**Goal:** Multi-strategy support (iron condors, butterflies, credit spreads), full risk management, position monitoring and adjustment logic, guided-execution mode operational.

### Phase 3: AI Intelligence Layer (Weeks 19-28)
**Goal:** Claude API integration for regime detection and trade reasoning, MCP server for portfolio queries, adaptive strategy weighting, calendar spreads and straddles added.

### Phase 4: Optimization & Production (Weeks 31-42+)
**Goal:** Walk-forward optimization using the Phase 1 LEAN backtests to confirm production parameters, performance analytics, second broker adapter (Tradier), production deployment on Azure, dashboard.

See `PHASE_*.md` documents for detailed sprint plans.

---

## Key Decisions Log

| Date | Decision | Rationale | Alternatives Considered |
|---|---|---|---|
| 2026-02-19 | Python-primary stack with C# for LEAN backtests | Python for all runtime components (ecosystem alignment with ib_async, py_vollib, MCP servers). C# for QuantConnect LEAN backtests — LEAN's native language, and developer has 11 years C# experience. Build-time Config Translator keeps both in sync from the YAML source of truth. | Pure Python everywhere (LEAN Python API lags C# API), Pure C# (ib_async ecosystem friction) |
| 2026-02-19 | Interactive Brokers primary | Best API, best multi-leg execution, Portfolio Margin at $110K, $0.65/contract | Tradier (simpler, commission-free but weaker execution), Alpaca (no multi-leg) |
| 2026-02-19 | System-wide paper/live toggle | Simplest mental model; entire system is paper OR live. Strategy-level toggle adds complexity without proportional value in early phases | Per-strategy toggle, per-trade approval |
| 2026-02-19 | Full custom over Option Alpha + AAT | AI layer is the differentiator; calendar/strangle support needed for full Optionetics; portfolio-level risk management not available in any platform | OA+AAT combo (75% coverage, 0 dev time), OA+QC (80%, 3-6 mo dev) |
| 2026-02-19 | ib_async over ib_insync | ib_insync creator passed away 2024; ib_async is the maintained community fork under new org | Native IBKR Python API (harder to use), ib_insync (unmaintained) |

## Success Criteria

1. **Phase 1 success:** Successfully execute and manage an iron condor lifecycle (open → monitor → close at profit target or stop) in paper mode within 8 weeks
2. **Phase 2 success:** Run 3+ concurrent strategies in paper mode for 4+ weeks with all risk limits enforced, zero violations
3. **Phase 3 success:** AI regime detection demonstrably improves strategy selection vs. static allocation in paper trading comparison over 8+ weeks
4. **Phase 4 success:** Walk-forward optimization of Phase 1 backtests confirms production parameters within 2% CAGR of historical results; system deployed to production; first live trade executed
5. **Overall success:** System generates 8-15% annualized returns in live trading over first 6 months with max drawdown under 8%

---

## File Index

| Document | Purpose |
|---|---|
| `PROJECT_STRATEGY.md` | This file — vision, architecture, decisions |
| `PHASE_1_FOUNDATION.md` | Weeks 1-8 detailed sprint plan |
| `PHASE_2_STRATEGIES.md` | Weeks 9-18 detailed sprint plan |
| `PHASE_3_AI_LAYER.md` | Weeks 19-28 detailed sprint plan |
| `PHASE_4_PRODUCTION.md` | Weeks 29-40 detailed sprint plan |
| `ARCHITECTURE.md` | Detailed technical architecture and module specs |
| `RISK_FRAMEWORK.md` | Complete risk management specification |
