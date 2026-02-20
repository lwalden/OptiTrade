# Phase 1: Foundation & First Trade
## Weeks 1-8 | ~15-20 hrs/week | Total: ~120-160 hours

**Phase Goal:** Connect to IBKR, retrieve options chains, calculate Greeks, execute a single iron condor lifecycle in paper mode, with the paper/live toggle functional.

**Exit Criteria:**
- [ ] System connects to IB Gateway (paper mode)
- [ ] Can retrieve and filter SPX options chains by DTE, delta, IV rank
- [ ] Calculates Greeks for any options contract
- [ ] Executes a 4-leg iron condor as a combo order
- [ ] Monitors position P&L in real-time
- [ ] Closes position at 50% profit target or 200% loss stop
- [ ] Paper/live toggle works via environment variable
- [ ] All actions logged to database with timestamps
- [ ] Basic CLI interface for manual commands

---

### Sprint 1.1: Project Scaffold & IBKR Connection (Weeks 1-2)

**Deliverables:**
- Project repository initialized with AIAgentMinder
- Python project structure (Poetry/uv for dependency management)
- IB Gateway installed and configured (paper account)
- Connection module that connects, heartbeats, and reconnects
- Paper/live toggle via `OPTIMIND_MODE` env var (controls port selection)

**Tasks:**
```
1.1.1  Initialize git repo with AIAgentMinder scaffold
       - DECISIONS.md, PLAN.md, SESSION_LOG.md
       - .claude/ directory with project rules and permissions
       - Pre-commit hooks for linting (ruff) and type checking (mypy)

1.1.2  Set up Python project structure
       /optimind
         /core           # Configuration, constants, logging
         /broker          # Broker abstraction layer
           /ibkr          # IBKR-specific implementation
         /data            # Market data retrieval and storage
         /strategies      # Strategy definitions
         /risk            # Risk management
         /execution       # Order management
         /ai              # AI/LLM integration (Phase 3)
         /mcp             # MCP server (Phase 3)
         /monitor         # Position monitoring
         /cli             # Command-line interface
       /tests
       /config

1.1.3  Install and configure IB Gateway
       - Download IB Gateway (stable channel)
       - Configure paper trading account
       - Enable API access, set ports (paper: 4002, live: 4001)
       - Document setup steps in SETUP.md

1.1.4  Build broker connection module
       - Install ib_async (pip install ib_async)
       - BrokerConnection class with connect/disconnect/reconnect
       - Auto-reconnect on drop with exponential backoff
       - Connection health monitoring (heartbeat every 30s)
       - Paper/live port selection from OPTIMIND_MODE env var

1.1.5  Build broker abstraction interface
       - Abstract BrokerAdapter base class
       - IBKRAdapter implementing the interface
       - Methods: connect(), get_account(), get_positions(),
         place_order(), cancel_order(), get_options_chain()
       - This abstraction enables Tradier adapter later (Phase 4)

1.1.6  Verify connection with basic tests
       - Connect to paper account
       - Retrieve account summary (NLV, buying power, margin)
       - Retrieve current positions (should be empty)
       - Log all data to console and SQLite
```

**Key Technical Notes:**
- ib_async uses asyncio — the entire system should be async-native from day 1
- IB Gateway is preferred over TWS for automated trading (lighter, more stable, no UI required)
- Paper account uses separate port (4002) — the toggle is literally just a port number change
- Connection to IB Gateway requires the gateway process to be running — document this as a startup prerequisite

---

### Sprint 1.2: Options Chain Retrieval & Greeks (Weeks 3-4)

**Deliverables:**
- Retrieve full SPX options chains from IBKR
- Filter chains by DTE range, moneyness, delta targets
- Calculate Greeks (delta, gamma, theta, vega) for any contract
- IV rank calculation for underlyings
- Data cached in SQLite for analysis

**Tasks:**
```
1.2.1  Build options chain retrieval module
       - Request SPX option chain parameters (strikes, expirations)
       - Filter to relevant strikes (e.g., within 2 standard deviations)
       - Filter to target DTE range (30-60 days)
       - Retrieve live quotes for filtered contracts
       - Handle IBKR's pacing rules (50 msg/sec limit)

1.2.2  Build Greeks calculation module
       - Install py_vollib for Black-Scholes IV and Greeks
       - Calculate implied volatility from market prices
       - Calculate delta, gamma, theta, vega for each contract
       - Validate against IBKR's provided Greeks (they supply these too)
       - Log discrepancies > 5% for investigation

1.2.3  Build IV rank/percentile calculation
       - Retrieve 1-year historical volatility data for underlying
       - Calculate current IV rank (where current IV sits in 52-week range)
       - Calculate IV percentile (% of days IV was lower)
       - Store historical IV data in SQLite for trend analysis
       - IV rank > 50% is primary filter for premium-selling strategies

1.2.4  Build options scanner/screener
       - Scan underlyings (start with SPX, SPY, QQQ, IWM)
       - For each: calculate IV rank, retrieve chain, identify candidates
       - Iron condor candidate: 30-delta short strikes, 45 DTE, IV rank > 50%
       - Output ranked list of candidates with expected value calculations
       - Cache results in SQLite with timestamp

1.2.5  Build data storage layer
       - SQLite database schema for:
         * options_chains (snapshots of chain data)
         * greeks_history (Greeks over time for open positions)
         * iv_history (daily IV rank/percentile per underlying)
         * trades (all orders placed and fills received)
         * positions (current open positions with entry data)
       - Migration script for schema changes
       - Data retention policy (90 days for chains, indefinite for trades)
```

**Key Technical Notes:**
- SPX options are index options — cash-settled, European-style, Section 1256 tax treatment
- IBKR provides Greeks via reqMktData() — use these as primary, py_vollib as validation
- IV rank > 50% means current IV is above median for the past year — good for premium selling
- Pacing rules are critical — batch requests and throttle to avoid disconnection

---

### Sprint 1.3: Iron Condor Construction & Execution (Weeks 5-6)

**Deliverables:**
- Iron condor strategy definition with configurable parameters
- Combo order builder that constructs 4-leg orders
- Order execution with SmartPricing-like behavior (start mid, walk to natural)
- Fill confirmation and position tracking
- Paper trade execution of first iron condor

**Tasks:**
```
1.3.1  Define strategy data model
       - Strategy base class with common parameters:
         * underlying, target_dte, max_risk, profit_target_pct, stop_loss_pct
       - IronCondorStrategy extending base:
         * short_put_delta, short_call_delta, wing_width
         * Default: 30-delta shorts, $50 wings on SPX, 45 DTE
       - StrategyResult: selected contracts, expected P&L, Greeks, max risk

1.3.2  Build iron condor contract selector
       - Given strategy params, select optimal contracts from chain:
         * Find puts with delta closest to -0.30
         * Find calls with delta closest to +0.30
         * Find wing contracts $50 OTM from shorts
         * Validate all 4 contracts exist and are liquid (bid-ask < $0.50)
       - Calculate max profit (net credit received)
       - Calculate max loss (wing width - credit)
       - Calculate breakeven points
       - Calculate probability of profit (from deltas)

1.3.3  Build combo order builder
       - Construct IBKR ComboLeg objects for 4-leg iron condor:
         * BUY 1 far OTM put (lower wing)
         * SELL 1 near OTM put (short put)
         * SELL 1 near OTM call (short call)  
         * BUY 1 far OTM call (upper wing)
       - Build as BAG contract with combo legs
       - Set order type: LMT (limit order) at calculated mid-price
       - Set TIF: GTC (good-til-cancelled)

1.3.4  Build SmartPricing execution logic
       - Start with limit order at mid-price of the combo
       - If not filled after 60 seconds, adjust $0.05 toward natural
       - Repeat up to 5 times (max walk of $0.25)
       - If still unfilled, alert and hold (don't chase)
       - Log each price adjustment with timestamp
       - Configurable patience parameters (interval, max_adjustments, step_size)

1.3.5  Build position tracker
       - On fill: record entry data (contracts, credit, Greeks, timestamp)
       - Calculate current P&L from live quotes
       - Track position Greeks in real-time
       - Maintain position state: PENDING → OPEN → CLOSING → CLOSED
       - Store in SQLite positions table

1.3.6  Execute first paper iron condor 🎯
       - Run the full pipeline: scan → select → construct → execute
       - Verify fill in paper account
       - Verify position appears in monitoring
       - Document the experience — what worked, what was surprising
       - This is the Phase 1 milestone moment
```

**Key Technical Notes:**
- IBKR combo orders are submitted as a single order with ComboLeg array — atomic execution
- Mid-price = average of combo's bid and ask (IBKR provides combo quotes)
- For SPX: 1 contract = 100x multiplier, so $5 wide wings = $500 max risk per contract
- Paper account fills may not perfectly simulate live fills — note this limitation

---

### Sprint 1.4: Position Monitoring & Exit Logic (Weeks 7-8)

**Deliverables:**
- Real-time position P&L monitoring
- Automatic exit at 50% profit target
- Automatic exit at 200% max loss (2x credit received)
- Position lifecycle management (open → monitor → close)
- CLI interface for system interaction
- Phase 1 complete — full iron condor lifecycle in paper mode

**Tasks:**
```
1.4.1  Build position monitor service
       - Async loop: poll positions every 60 seconds during market hours
       - Calculate current P&L as percentage of max profit
       - Calculate current Greeks exposure
       - Detect when profit target or stop loss is hit
       - Alert system (console log + optional email/SMS via simple webhook)

1.4.2  Build exit order logic
       - Profit target exit: close combo when P&L >= 50% of max profit
         * Example: collected $3.00 credit → close at $1.50 debit
       - Stop loss exit: close combo when loss >= 2x credit received
         * Example: collected $3.00 → close if position costs $6.00+
       - Use same SmartPricing logic for exit orders
       - Handle partial fills on exit (rare for combos, but handle it)

1.4.3  Build DTE-based management rules
       - At 21 DTE: tighten profit target to 25% (close if winning at all)
       - At 14 DTE: close any position regardless of P&L (gamma risk)
       - At 7 DTE: emergency close if somehow still open
       - These are parameterized but have strong defaults

1.4.4  Build trade journal / logging
       - Every trade action logged: entry, adjustments, exits
       - Include: timestamp, action, contracts, price, Greeks, reason
       - Running P&L summary per strategy and overall
       - Export to CSV for analysis

1.4.5  Build CLI interface
       - Commands:
         * `optimind status` — account summary, open positions, daily P&L
         * `optimind scan` — run scanner, show candidates
         * `optimind trade <strategy>` — execute strategy (paper or live per toggle)
         * `optimind close <position_id>` — manually close a position
         * `optimind mode` — show current mode (paper/live)
         * `optimind history` — recent trades and P&L
       - Use Click or Typer library for CLI framework

1.4.6  Phase 1 integration testing
       - Execute complete lifecycle in paper mode:
         1. Scan for SPX iron condor candidates
         2. Select best candidate
         3. Execute combo order
         4. Monitor position
         5. Close at profit target (or wait for stop)
       - Run for at least 1 full iron condor cycle (open to close)
       - Document any issues in DECISIONS.md
       - Phase 1 retrospective — what to improve for Phase 2
```

**Key Technical Notes:**
- Position monitoring should NOT run when market is closed — waste of API calls
- IBKR market data subscriptions: need Level 1 for SPX ($5/mo nonprofessional)
- The monitoring loop must handle market holidays gracefully
- CLI is the Phase 1 UI — no web dashboard yet (that's Phase 4)

---

## Phase 1 Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| IB Gateway connection instability | Medium | High | Auto-reconnect with exponential backoff; health monitoring |
| IBKR paper account doesn't perfectly simulate fills | Certain | Low | Document limitations; don't rely on paper P&L as accurate predictor |
| Python async complexity slows development | Low | Medium | ib_async handles most async complexity; use straightforward patterns |
| Options chain data retrieval pacing limits | Medium | Medium | Batch requests; cache aggressively; throttle to 40 msg/sec (below 50 limit) |
| Scope creep into Phase 2 features | High | Medium | Strict sprint boundaries; log "nice to haves" in BACKLOG.md |

## Phase 1 Dependencies

- Interactive Brokers account with paper trading enabled
- IB Gateway installed on development machine
- IBKR market data subscription ($5-10/mo for nonprofessional)
- Python 3.12+ development environment
- ib_async, py_vollib, pandas, sqlalchemy, click installed

## Phase 1 Monthly Cost

| Item | Cost |
|---|---|
| IBKR market data (nonprofessional) | $5-10/mo |
| Total Phase 1 | **$5-10/mo** |
