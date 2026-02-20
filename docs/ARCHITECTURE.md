# OptiMind: Technical Architecture
## Detailed Module Specifications & Data Flow

**Last Updated:** 2026-02-19

---

## System Overview

OptiMind is an event-driven, async Python application that connects to Interactive Brokers via IB Gateway, processes market data through quantitative and AI analysis layers, generates trade signals, validates against risk limits, and executes options strategies. The system operates in two modes: paper (development/testing) and live (production), controlled by a single environment variable.

## Project Structure

```
optimind/
├── __main__.py                 # Entry point
├── config/
│   ├── settings.py             # Pydantic settings (env vars, defaults)
│   ├── strategies.yaml         # Strategy configurations
│   ├── risk_limits.yaml        # Risk parameters (overrides for non-hardcoded)
│   ├── watchlist.yaml          # Underlyings to monitor
│   └── sectors.yaml            # Sector correlation mapping
│
├── core/
│   ├── models.py               # Pydantic data models (Position, Trade, Order, etc.)
│   ├── events.py               # Event bus (pub/sub for system events)
│   ├── database.py             # SQLAlchemy models and session management
│   ├── logging.py              # Structured logging configuration
│   └── constants.py            # Hard-coded limits (CANNOT be overridden by config)
│
├── broker/
│   ├── base.py                 # Abstract BrokerAdapter interface
│   ├── ibkr/
│   │   ├── adapter.py          # IBKRAdapter implementation
│   │   ├── connection.py       # IB Gateway connection management
│   │   ├── orders.py           # Order construction (combo/BAG orders)
│   │   └── data.py             # Market data retrieval
│   └── tradier/
│       ├── adapter.py          # TradierAdapter implementation (Phase 4)
│       └── ...
│
├── data/
│   ├── market_data.py          # Real-time market data manager
│   ├── options_chain.py        # Options chain retrieval and filtering
│   ├── greeks.py               # Greeks calculation (py_vollib + IBKR validation)
│   ├── iv_surface.py           # IV rank, percentile, surface analysis
│   └── orats.py                # ORATS data integration (Phase 3)
│
├── strategies/
│   ├── base.py                 # StrategyBase abstract class
│   ├── registry.py             # Strategy registration and discovery
│   ├── iron_condor.py          # Iron condor strategy
│   ├── butterfly.py            # Butterfly spread strategy
│   ├── credit_spread.py        # Bull put / bear call spreads
│   ├── calendar_spread.py      # Calendar/horizontal spreads (Phase 3)
│   └── straddle.py             # Pre-earnings straddle (Phase 3)
│
├── risk/
│   ├── manager.py              # Pre-trade risk checks
│   ├── portfolio_greeks.py     # Aggregate portfolio Greeks monitoring
│   ├── circuit_breakers.py     # Daily/weekly/monthly loss limits
│   ├── margin.py               # Margin utilization tracking
│   └── correlation.py          # Sector correlation enforcement
│
├── execution/
│   ├── engine.py               # Order execution with SmartPricing
│   ├── guided.py               # Guided execution mode (approve/reject)
│   └── position_manager.py     # Position lifecycle management
│
├── monitor/
│   ├── position_monitor.py     # Real-time position P&L and Greeks tracking
│   ├── threat_detector.py      # Adjustment trigger detection
│   ├── adjustment_engine.py    # Rolling and transformation logic
│   └── scheduler.py            # Time-based task scheduling
│
├── ai/
│   ├── client.py               # Claude API client wrapper
│   ├── regime.py               # Market regime detection (quant + AI)
│   ├── trade_rationale.py      # Trade reasoning generation
│   ├── portfolio_review.py     # AI portfolio assessment
│   └── prompts/                # Prompt templates (Jinja2)
│       ├── regime_analysis.j2
│       ├── trade_rationale.j2
│       ├── adjustment_reasoning.j2
│       └── portfolio_review.j2
│
├── mcp/
│   ├── server.py               # MCP server main
│   └── tools.py                # MCP tool definitions
│
├── dashboard/
│   ├── app.py                  # Streamlit dashboard
│   └── pages/                  # Dashboard pages
│
├── cli/
│   ├── main.py                 # CLI entry point (Typer)
│   └── commands/               # CLI command modules
│
└── tax/
    ├── lot_tracker.py          # Tax lot tracking
    ├── section_1256.py         # Section 1256 60/40 treatment
    ├── wash_sale.py            # Wash sale detection
    └── reports.py              # Tax report generation
```

---

## Market Data Layer: Critical Rules

### IBKR API Pacing
IBKR enforces a 50 message/second rate limit. Exceeding it causes disconnection. The market data layer must:
- Throttle all `reqMktData` and `reqHistoricalData` calls to **40 messages/second** (20% safety margin)
- Batch options chain requests: retrieve all strikes for one expiry before moving to the next
- Implement exponential backoff on `PACING_VIOLATION` errors received from IBKR
- Use a semaphore with max **10 concurrent market data subscriptions** — never make unbounded concurrent requests
- Log pacing near-misses (>35 msg/sec) for monitoring

### Data Caching (mandatory)
Market data is cached to minimize API calls and provide resilience against connectivity gaps:

| Data | Cache TTL | Rationale |
|---|---|---|
| Options chain snapshots | 60 seconds | Chain data doesn't change tick-by-tick; constant re-fetch wastes pacing budget |
| IV rank per underlying | 1 hour | IV rank is a slow-moving daily metric; recalculating every minute is wasteful |
| Historical volatility data (for IV rank) | 24 hours | Historical data does not change intraday |
| Position Greeks (open positions) | Never cache | Always use live tick data — stale Greeks cause incorrect risk decisions |
| Underlying price (for risk checks) | 5 seconds max | Short cache acceptable; Greeks validation needs recent underlying price |

Cache implementation: in-memory dict with TTL tracking for Phase 1-3; SQLite-backed for Phase 4 production (survives process restarts).

### Data Integrity Validation
All market data passes through `MarketDataValidator` before entering the event bus. No raw IBKR tick data reaches the Risk Manager directly. See RISK_FRAMEWORK.md — Data Integrity Layer for full validation rules and field-level bounds.

---

## Data Models (core/models.py)

```python
# Key Pydantic models — these define the data contracts across the system

class MarketContext(BaseModel):
    """Snapshot of market conditions for AI analysis."""
    timestamp: datetime
    vix_spot: float
    vix_3m: float
    vix_slope: str  # "contango" | "backwardation" | "flat"
    spx_price: float
    spx_rv10: float  # 10-day realized volatility
    spx_rv30: float  # 30-day realized volatility
    iv_ranks: dict[str, float]  # {"SPX": 45.2, "QQQ": 62.1, ...}
    sector_performance: dict[str, float]  # {"XLK": 1.2, "XLE": -1.5, ...}
    regime_quantitative: str  # From rule-based engine
    regime_ai: str | None  # From Claude, if available

class Position(BaseModel):
    """An open options position (may be multi-leg)."""
    id: str
    strategy: str  # "iron_condor", "butterfly", etc.
    underlying: str
    legs: list[PositionLeg]
    entry_date: datetime
    entry_credit: float  # Positive = credit received
    current_pnl: float
    current_pnl_pct: float  # As % of max profit
    max_profit: float
    max_loss: float
    greeks: PositionGreeks
    dte: int  # Days to nearest expiration
    threat_level: str  # "GREEN" | "YELLOW" | "RED"
    adjustment_count: int
    status: str  # "PENDING" | "OPEN" | "CLOSING" | "CLOSED"

class TradeSetup(BaseModel):
    """A proposed trade before execution."""
    strategy: str
    underlying: str
    legs: list[OrderLeg]
    expected_credit: float
    max_risk: float
    probability_of_profit: float
    greeks: PositionGreeks
    rationale: str  # AI-generated or rule-based
    risk_check_result: RiskCheckResult

class RiskCheckResult(BaseModel):
    """Result of pre-trade risk validation."""
    approved: bool
    checks: list[RiskCheck]  # Each check with pass/fail and detail
    rejection_reason: str | None
    suggested_adjustment: str | None  # e.g., "Reduce to 2 contracts"
```

---

## Event-Driven Architecture (core/events.py)

The system uses an internal event bus for loose coupling between modules:

```
Events:
  MARKET_DATA_UPDATED    → Triggers position monitoring, scanner refresh
  SCAN_COMPLETE          → Triggers guided-mode notification
  TRADE_PROPOSED         → Triggers risk check
  RISK_APPROVED          → Triggers execution
  RISK_REJECTED          → Triggers notification with reason
  ORDER_FILLED           → Triggers position creation
  POSITION_UPDATED       → Triggers threat detection
  THREAT_DETECTED        → Triggers adjustment engine
  ADJUSTMENT_PROPOSED    → Triggers guided-mode notification (or auto-execute)
  EXIT_TRIGGERED         → Triggers close order
  POSITION_CLOSED        → Triggers P&L recording, tax lot creation
  CIRCUIT_BREAKER_FIRED  → Triggers system halt/notification
  REGIME_CHANGED         → Triggers strategy weight adjustment
```

This event-driven design means modules don't call each other directly — they publish events that other modules subscribe to. This makes testing easier and prevents circular dependencies.

---

## Key Data Flows

### 1. Trade Entry Flow (Guided Mode)
```
Scheduler (10:30 AM) → Scanner
  Scanner → retrieves options chains → calculates Greeks/IV
  Scanner → identifies candidates meeting strategy criteria
  Scanner → publishes SCAN_COMPLETE with candidates

SCAN_COMPLETE → Strategy Engine
  Strategy Engine → selects best candidate
  Strategy Engine → constructs TradeSetup
  Strategy Engine → publishes TRADE_PROPOSED

TRADE_PROPOSED → Risk Manager
  Risk Manager → runs all pre-trade checks
  If APPROVED → publishes RISK_APPROVED
  If REJECTED → publishes RISK_REJECTED → Notification

RISK_APPROVED → Guided Execution Mode
  Guided Mode → stores in pending_trades table
  Guided Mode → sends notification to user
  User reviews via CLI → `optimind approve <id>`
  On approval → publishes to Execution Engine

Execution Engine → builds ComboOrder
  → submits to IBKR with SmartPricing
  → monitors for fill
  On fill → publishes ORDER_FILLED

ORDER_FILLED → Position Manager
  → creates Position record
  → begins monitoring cycle
```

### 2. Position Monitoring Flow
```
Every 60 seconds during market hours:

Position Monitor → queries IBKR for position data
  → calculates current P&L, Greeks
  → publishes POSITION_UPDATED

POSITION_UPDATED → Threat Detector
  If threat_level changes → publishes THREAT_DETECTED

POSITION_UPDATED → Exit Logic
  If profit_target hit → publishes EXIT_TRIGGERED
  If stop_loss hit → publishes EXIT_TRIGGERED
  If DTE <= threshold → publishes EXIT_TRIGGERED

EXIT_TRIGGERED → Execution Engine
  → builds close order
  → executes with SmartPricing
  → on fill → publishes POSITION_CLOSED

POSITION_CLOSED → Performance Tracker, Tax Lot Tracker, Trade Journal
```

### 3. AI Regime Assessment Flow
```
Twice daily (10:15 AM, 2:00 PM ET):

Market Context Collector → gathers all data points (via MarketDataValidator)
  → VIX, term structure, IV ranks, sector performance, etc.
  → publishes MARKET_DATA_UPDATED

Regime Engine (Quantitative) → applies rule-based classification  [ALWAYS runs first]
  → produces regime_quantitative  ← this is the safety-net baseline

Regime Engine (AI) → wraps Claude API call in asyncio.wait_for(timeout=5.0):

  [SUCCESS within 5s]:
    → formats MarketContext as structured snapshot
    → sends to Claude API with regime analysis prompt
    → parses structured JSON response
    → produces regime_ai
    → operative_regime = regime_ai (AI result used when available)

  [TIMEOUT (>5 seconds) or API error]:
    → logs: "AI regime assessment unavailable"
    → regime_ai = None
    → Check: is there a prior successful AI assessment < 2 hours old?
        [YES — recent AI result available]:
          → operative_regime = last_successful_regime_ai  (regime persistence)
          → logs: "Using cached AI regime from {timestamp} — {regime}"
          → emits: AI_CACHE_USED event
        [NO — no recent AI result]:
          → operative_regime = regime_quantitative
          → logs: "No recent AI regime available — using quantitative baseline"
          → emits: AI_FALLBACK_TRIGGERED event (for monitoring dashboards)
    → system continues operating normally — AI failure is NON-FATAL

  [Response received but invalid JSON / schema mismatch]:
    → logs raw response for debugging
    → same fallback path as timeout above
    → emits: AI_FALLBACK_TRIGGERED event

If operative_regime changed (vs previous period) → publishes REGIME_CHANGED
  → Strategy Weighting Engine adjusts allocation percentages
  → Scanner uses new weights for next scan
```

**Design principle:** The quantitative regime detector is NOT a backup — it is the permanent safety net. The AI layer enhances the assessment when available. The system must operate identically whether Claude responds in 1 second or is unreachable for hours. Any exception from the Anthropic SDK (APIError, AuthenticationError, RateLimitError) must be caught in `ai/client.py` and must not propagate to the main trading loop.

**Regime persistence (graceful degradation ladder):**
1. Fresh AI response (< 5s, valid JSON) → use `regime_ai` directly
2. AI timeout/error, but prior successful AI assessment < 2 hours old → use cached `regime_ai` (regime persistence — a brief API blip should not flip strategy weights)
3. No recent AI assessment → use `regime_quantitative` (pure rules-based baseline)

The 2-hour window reflects the twice-daily (10:15 AM, 2:00 PM) assessment cadence. A timeout on the 10:15 AM call still has the 2:00 PM prior-day result available. A timeout on both intraday calls falls through to quantitative.

---

## Database Schema (SQLite → PostgreSQL)

```sql
-- Core tables

CREATE TABLE positions (
    id TEXT PRIMARY KEY,
    strategy TEXT NOT NULL,
    underlying TEXT NOT NULL,
    entry_date TIMESTAMP NOT NULL,
    close_date TIMESTAMP,
    entry_credit REAL,
    close_debit REAL,
    max_profit REAL,
    max_loss REAL,
    realized_pnl REAL,
    status TEXT DEFAULT 'OPEN',
    adjustment_count INTEGER DEFAULT 0,
    rationale TEXT,
    regime_at_entry TEXT
);

CREATE TABLE position_legs (
    id INTEGER PRIMARY KEY,
    position_id TEXT REFERENCES positions(id),
    contract_symbol TEXT,
    right TEXT,  -- 'C' or 'P'
    strike REAL,
    expiry DATE,
    action TEXT,  -- 'BUY' or 'SELL'
    quantity INTEGER,
    fill_price REAL,
    close_price REAL
);

CREATE TABLE orders (
    id TEXT PRIMARY KEY,
    position_id TEXT REFERENCES positions(id),
    order_type TEXT,  -- 'ENTRY', 'EXIT', 'ADJUSTMENT'
    status TEXT,  -- 'SUBMITTED', 'FILLED', 'CANCELLED', 'REJECTED'
    submitted_at TIMESTAMP,
    filled_at TIMESTAMP,
    limit_price REAL,
    fill_price REAL,
    price_adjustments INTEGER DEFAULT 0,
    commission REAL
);

CREATE TABLE market_context (
    id INTEGER PRIMARY KEY,
    timestamp TIMESTAMP NOT NULL,
    vix_spot REAL,
    vix_3m REAL,
    vix_slope TEXT,
    spx_price REAL,
    regime_quantitative TEXT,
    regime_ai TEXT,
    regime_confidence REAL,
    raw_data JSON  -- Full structured snapshot
);

CREATE TABLE iv_history (
    id INTEGER PRIMARY KEY,
    underlying TEXT NOT NULL,
    date DATE NOT NULL,
    iv_rank REAL,
    iv_percentile REAL,
    iv_current REAL,
    iv_52w_high REAL,
    iv_52w_low REAL,
    UNIQUE(underlying, date)
);

CREATE TABLE greeks_snapshots (
    id INTEGER PRIMARY KEY,
    position_id TEXT REFERENCES positions(id),
    timestamp TIMESTAMP NOT NULL,
    delta REAL,
    gamma REAL,
    theta REAL,
    vega REAL,
    pnl REAL,
    underlying_price REAL
);

CREATE TABLE risk_events (
    id INTEGER PRIMARY KEY,
    timestamp TIMESTAMP NOT NULL,
    event_type TEXT,  -- 'TRADE_CHECK', 'CIRCUIT_BREAKER', 'MARGIN_ALERT'
    result TEXT,  -- 'APPROVED', 'REJECTED', 'TRIGGERED'
    detail JSON
);

CREATE TABLE tax_lots (
    id INTEGER PRIMARY KEY,
    position_id TEXT REFERENCES positions(id),
    open_date DATE,
    close_date DATE,
    instrument TEXT,
    proceeds REAL,
    cost_basis REAL,
    gain_loss REAL,
    is_section_1256 BOOLEAN,
    holding_period TEXT,  -- 'SHORT' or 'LONG' (or '60/40' for 1256)
    wash_sale_adjustment REAL DEFAULT 0
);
```

---

## Configuration System

```yaml
# config/strategies.yaml
strategies:
  iron_condor:
    enabled: true
    underlyings: [SPX, SPY, QQQ, IWM]
    params:
      target_dte: 45
      short_delta: 0.30
      wing_width_spx: 50  # $50 wide on SPX
      wing_width_spy: 5   # $5 wide on SPY/QQQ
      iv_rank_min: 50
      profit_target_pct: 50
      stop_loss_pct: 200
      dte_tighten: 21
      dte_close: 14
      max_concurrent: 3
    schedule:
      scan_times: ["10:30", "14:00"]
      timezone: "US/Eastern"

  butterfly:
    enabled: true
    underlyings: [AAPL, MSFT, GOOGL, AMZN, NVDA]
    params:
      target_dte: 30
      wing_width: 10
      iv_rank_max: 30
      trend_indicator: "ma_crossover_10_30"
      profit_target_pct: 50
      dte_close: 14
      max_concurrent: 2
    schedule:
      scan_times: ["10:30"]
      timezone: "US/Eastern"

  # ... credit_spread, calendar_spread, straddle configs
```

```python
# core/constants.py — HARD LIMITS (not configurable)
# These exist in code, not config, to prevent accidental override

MAX_RISK_PER_TRADE_PCT = 2.5      # % of NLV
MAX_DEPLOYED_CAPITAL_PCT = 40.0    # % of NLV
MAX_POSITIONS_PER_UNDERLYING = 2
MAX_SECTOR_POSITIONS = 3
DAILY_LOSS_HALT_PCT = 3.0          # Halt new entries
DAILY_LOSS_EMERGENCY_PCT = 5.0     # Close all positions
WEEKLY_LOSS_LIMIT_PCT = 5.0
MONTHLY_LOSS_LIMIT_PCT = 10.0
PORTFOLIO_DELTA_LIMIT_PCT = 10.0   # ±% of NLV
MAX_MARGIN_UTILIZATION_REGT = 60.0
MAX_MARGIN_UTILIZATION_PM = 40.0
MAX_ADJUSTMENTS_PER_POSITION = 2
```

---

## Technology Dependency Map

```
Core Runtime:
  Python 3.12+
  asyncio (standard library)
  pydantic >= 2.0 (data validation)
  sqlalchemy >= 2.0 (ORM)
  alembic (database migrations)

Broker Integration:
  ib_async >= 1.0 (IBKR API wrapper, successor to ib_insync)
  httpx (for Tradier REST API)

Options Analytics:
  py_vollib (Black-Scholes Greeks, IV calculation)
  QuantLib-Python (optional: advanced pricing, vol surface)
  numpy, pandas (data manipulation)
  scipy (optimization, statistics)

AI Layer:
  anthropic >= 0.40 (Claude API SDK)
  jinja2 (prompt templates)

MCP Server:
  mcp >= 1.0 (Model Context Protocol SDK)

Dashboard:
  streamlit >= 1.30 (web dashboard)
  plotly (interactive charts)

Infrastructure:
  apscheduler (task scheduling)
  typer (CLI framework — chosen over click)
  structlog (structured logging)
  exchange_calendars (market holiday handling)
  python-dotenv (env var management)
  aiosqlite >= 0.20 [REQUIRED — async SQLite driver; never use synchronous SQLite in async context]

Backtesting (Sprint 1.0 — separate from Python runtime, in backtests/lean/):
  QuantConnect LEAN (C# native algorithm engine)
  .NET SDK (C# compilation)
  LEAN CLI (pip install lean — Python orchestration tool for LEAN)
  scripts/generate_lean_config.py (Build-Time Config Translator: reads config/strategies.yaml,
    writes backtests/lean/Config/StrategyConstants.cs — prevents configuration drift)

Testing:
  pytest, pytest-asyncio
  hypothesis (property-based testing for risk module)
  pytest-cov (coverage)

Development:
  ruff (linting)
  mypy (type checking)
  poetry or uv (dependency management)
```

---

## Deployment Architecture

### Development (Local)
```
Developer Machine
├── IB Gateway (paper mode, port 4002)
├── OptiMind Python process
├── SQLite database (./data/optimind.db)
└── Streamlit dashboard (localhost:8501)
```

### Production (Azure)
```
Azure VM (D2s v5, Ubuntu 24.04)
├── IB Gateway (live mode, port 4001)
│   └── Managed by IBC (IB Controller) for auto-restart
├── OptiMind (systemd service, auto-restart)
│   ├── Main trading loop
│   ├── Position monitor
│   ├── Scheduler
│   └── MCP server
├── Streamlit dashboard (behind nginx reverse proxy)
│
├── Azure Database for PostgreSQL (flexible, $25/mo)
├── Azure Blob Storage (daily backups)
└── Azure Monitor (health metrics, alerting)

Access:
  Dashboard: HTTPS via Azure public IP + nginx
  MCP Server: SSH tunnel for Claude Desktop connection
  Emergency access: SSH to VM for manual intervention
```

---

## Security Considerations

| Concern | Mitigation |
|---|---|
| Broker credentials | Environment variables, never in code or config files |
| API keys (Claude, ORATS) | Environment variables with restricted permissions |
| Database access | Local file (SQLite) or password-protected (PostgreSQL) |
| Dashboard exposure | HTTPS + basic auth (Streamlit) or Azure AD (React) |
| MCP server | Local-only by default; SSH tunnel for remote access |
| Paper/live toggle | Env var requires explicit set; default is PAPER |
| Accidental live trade | First line of execution engine: verify mode matches intent |
| Source code | Private GitHub repo; no credentials in commits |
