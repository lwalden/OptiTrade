# Phase 2: Strategy Engine & Risk Layer
## Weeks 9-18 | ~15-20 hrs/week | Total: ~150-200 hours

**Phase Goal:** Multi-strategy support, full risk management framework, position adjustment logic, and guided-execution mode. System can run 3+ strategies concurrently in paper mode with all risk limits enforced.

**Entry Criteria:** Phase 1 complete — single iron condor lifecycle working in paper mode.

**Exit Criteria:**
- [ ] Butterfly spread strategy operational
- [ ] Credit spread (bull put / bear call) strategy operational  
- [ ] Risk manager enforces all hard limits (per-trade, portfolio, daily, weekly)
- [ ] Portfolio-level Greeks monitoring with alerts
- [ ] Position adjustment logic (roll threatened legs)
- [ ] Guided-execution mode: system recommends, human approves, system executes
- [ ] 3+ concurrent positions managed simultaneously in paper mode
- [ ] 4+ weeks of continuous paper trading without risk violations

---

### Sprint 2.1: Strategy Engine Architecture (Weeks 9-10)

**Deliverables:**
- Refactored strategy engine supporting multiple strategy types
- Strategy registry and configuration system
- Butterfly spread strategy implementation
- Credit spread strategy implementation

**Tasks:**
```
2.1.1  Refactor strategy engine into plugin architecture
       - StrategyBase abstract class:
         * scan(chain, market_context) → List[Candidate]
         * select(candidates) → TradeSetup
         * construct_order(setup) → ComboOrder
         * should_exit(position) → ExitSignal | None
         * should_adjust(position) → AdjustmentSignal | None
       - StrategyRegistry: register/discover strategies
       - StrategyConfig: YAML-based configuration per strategy
       - Each strategy is a self-contained module

2.1.2  Implement butterfly spread strategy
       - OTM directional butterfly (Fontanills style):
         * Identify target price using 10/30 MA crossover
         * Place butterfly body at target strike
         * Wings $10-$25 wide depending on underlying price
       - Entry criteria: strong trend identified, IV rank < 30% (cheap options)
       - Exit: 50% profit target, or close at 14 DTE, or $0 if OTM at expiry
       - Risk: defined — max loss is debit paid

2.1.3  Implement credit spread strategies
       - Bull put spread (bullish):
         * Sell OTM put, buy further OTM put
         * 30-delta short, wing width configurable
       - Bear call spread (bearish):
         * Sell OTM call, buy further OTM call
         * Same delta and width logic
       - These are "half iron condors" — useful when directionally biased
       - Entry: IV rank > 40%, directional bias from AI layer (Phase 3)
         (Until Phase 3: use simple 10/30 MA trend as proxy)

2.1.4  Build strategy scheduler
       - Define when each strategy runs its scan:
         * Daily at 10:30 AM ET (after initial volatility settles)
         * Additional scan at 2:00 PM ET for afternoon opportunities
       - Configurable schedule per strategy
       - Respect market hours and holidays (use exchange_calendars library)
       - Rate-limit scans to stay within IBKR pacing limits

2.1.5  Build strategy performance tracker
       - Per-strategy metrics: win rate, avg profit, avg loss, profit factor
       - Running Sharpe ratio calculation
       - Maximum drawdown tracking
       - Store in SQLite, queryable via CLI
```

---

### Sprint 2.2: Risk Management Framework (Weeks 11-13)

**Deliverables:**
- Pre-trade risk checks (all hard limits enforced)
- Portfolio-level Greek monitoring
- Daily/weekly/monthly loss tracking with circuit breakers
- Correlation-aware position limits

**Tasks:**
```
2.2.1  Build pre-trade risk checker
       - RiskManager class called before every order submission
       - Checks (must ALL pass or order is rejected):
         * Max risk per trade: max_loss <= 2.5% of NLV
         * Max deployed capital: sum of all position margin < 40% of NLV
         * Max positions per underlying: <= 2
         * Max sector correlation: <= 3 in same sector
         * Margin utilization: < 60% Reg-T or 40% PM
       - Returns: APPROVED, REJECTED(reason), or REDUCED(suggested_size)
       - Every check logged with full context

2.2.2  Build sector correlation mapping
       - Map common options underlyings to sectors:
         * SPX/SPY → Broad Market
         * QQQ/NDX → Tech
         * IWM/RUT → Small Cap
         * GLD/SLV → Precious Metals
         * TLT → Bonds
         * XLE → Energy, XLF → Financials, etc.
       - Correlation matrix: treat same-sector as correlated
       - Treat SPX positions as 50% correlated with ALL sectors
       - Configurable mapping in YAML

2.2.3  Build portfolio Greeks monitor
       - Calculate aggregate portfolio Greeks:
         * Total delta, gamma, theta, vega across ALL positions
       - Alert thresholds:
         * Delta: warn at ±7% of NLV, halt at ±10%
         * Vega: warn when total vega exposure > 1% of NLV
       - Update every 5 minutes during market hours
       - Log to greeks_history table for trend analysis

2.2.4  Build loss tracking and circuit breakers
       - Track realized + unrealized P&L continuously
       - Daily loss circuit breaker:
         * At -3% NLV: halt all new entries, alert
         * At -5% NLV: close ALL positions, system lockdown
         * Manual override required to restart after -5% (CLI command with confirmation)
       - Weekly loss limit: -5% NLV → halt new entries until Monday
       - Monthly loss limit: -10% NLV → halt for remainder of month
       - Track P&L watermarks (high-water mark for drawdown calculation)

2.2.5  Build margin monitoring
       - Query IBKR for real-time margin data:
         * Initial margin, maintenance margin, excess liquidity
         * Margin cushion percentage
       - Alert at 50% utilization, hard stop at 60%
       - Portfolio Margin accounts: separate thresholds (40% target)
       - Handle margin calls gracefully (alert + auto-reduce smallest position)
```

---

### Sprint 2.3: Position Adjustment Logic (Weeks 14-15)

**Deliverables:**
- Automated adjustment detection (when positions are threatened)
- Rolling logic for iron condor legs
- Position transformation capabilities (condor → spread)
- Guided mode: suggest adjustment, wait for approval

**Tasks:**
```
2.3.1  Build threat detection for iron condors
       - Monitor distance from short strikes to underlying price
       - Threat levels:
         * GREEN: price within 70% of breakeven range
         * YELLOW: price within 50% of short strike (approaching)
         * RED: price breached short strike
       - Additional triggers:
         * Delta of short leg exceeds 0.50 (was 0.30 at entry)
         * Unrealized loss exceeds 100% of credit received

2.3.2  Build rolling logic
       - Roll threatened side:
         * Close the threatened spread (e.g., bull put if market falling)
         * Open new spread at same delta but further OTM
         * Net credit/debit of roll tracked separately
       - Roll timing: at YELLOW threat level, not RED (too late)
       - Roll rules:
         * Only roll if net credit can be maintained or roll cost < 50% of original credit
         * Maximum 2 rolls per position (avoid infinite rolling)
         * Don't roll within 14 DTE (close instead)

2.3.3  Build position transformation logic
       - Iron condor → single credit spread (close profitable side, keep threatened)
       - Iron condor → inverted iron condor (if underlying makes large move)
       - Butterfly → vertical spread (if underlying trends strongly)
       - Each transformation logged as a new trade linked to original position

2.3.4  Build adjustment recommendation engine
       - Given threatened position, generate ranked adjustment options:
         1. Roll the threatened side (cost, new Greeks, new breakevens)
         2. Close the position (realized loss)
         3. Transform the position (new structure, new risk profile)
         4. Do nothing (current trajectory, time to expiration)
       - Each option includes: estimated cost, new max risk, probability analysis
       - In guided mode: present options via CLI, wait for user selection
       - In automated mode: execute highest-ranked option (Phase 4)

2.3.5  Integrate adjustments with position tracker
       - Adjusted positions maintain full history:
         * Original entry → adjustment 1 → adjustment 2 → exit
       - P&L calculation includes all legs and adjustments
       - Adjustment count tracked (for max-2-rolls rule)
```

---

### Sprint 2.4: Guided Execution Mode & Integration (Weeks 16-18)

**Deliverables:**
- Guided execution mode fully operational
- Notification system (email/SMS for trade recommendations)
- Complete system running 3+ strategies in paper mode
- 4+ weeks of continuous paper trading begins

**Tasks:**
```
2.4.1  Build guided execution mode
       - System flow in guided mode:
         1. Scanner identifies candidate → logs to pending_trades
         2. System sends notification with trade details:
            - Strategy, underlying, contracts, Greeks
            - Max risk, probability of profit, expected value
            - Clear human-readable description
         3. User reviews via CLI: `optimind pending`
         4. User approves: `optimind approve <trade_id>`
            or rejects: `optimind reject <trade_id> [reason]`
         5. On approval: system executes with SmartPricing
       - Adjustments follow same flow: recommend → notify → approve → execute
       - Exits at profit target are automatic (no approval needed)
       - Exits at stop loss are automatic (safety critical)

2.4.2  Build notification system
       - Simple webhook-based notifications:
         * New trade recommendation
         * Position adjustment needed
         * Daily summary (open positions, P&L, upcoming expirations)
         * Risk alerts (circuit breaker warnings)
       - Phase 2: email via SendGrid or similar (simple REST API)
       - Phase 4: Slack/Discord integration optional

2.4.3  Configure multi-strategy paper portfolio
       - Strategy allocation for paper testing:
         * Iron condor bot (SPX): 2-3 positions, 45 DTE, 30-delta
         * Bull put spread bot (SPY/QQQ): 1-2 positions, 30 DTE, 20-delta
         * Butterfly bot (individual stocks): 1 position, 30 DTE, directional
       - Total capital allocation: ~30% of paper account NLV
       - Run all strategies simultaneously

2.4.4  Build daily operations automation
       - Morning routine (10:00 AM ET):
         * Connect to IB Gateway (if not connected)
         * Refresh account data
         * Update IV rank for watchlist
         * Scan for new candidates
         * Check existing positions for adjustments
         * Generate daily briefing
       - End of day (3:45 PM ET):
         * Final position check
         * Generate daily summary
         * Log daily P&L
       - Scheduler: use APScheduler library

2.4.5  Run extended paper trading (4+ weeks)
       - Track all metrics during paper trading period
       - Document every trade decision and outcome
       - Identify system issues and edge cases
       - Weekly review: what worked, what didn't, what to adjust
       - This period validates the system before adding AI (Phase 3)
```

---

## Phase 2 Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Risk manager has bugs that allow oversized positions | Medium | Critical | Extensive unit tests; manual verification of first 10 trades; redundant checks |
| Multi-strategy interaction creates unexpected portfolio risk | Medium | High | Portfolio-level Greek monitoring; stress test with historical data |
| Position adjustment logic makes things worse | Medium | Medium | Conservative adjustment defaults; max-2-rolls limit; guided mode for oversight |
| Paper trading period reveals strategy doesn't work | Low | High | This is actually a success — better to learn in paper. Adjust parameters and retry |
| IBKR API rate limits hit with multiple strategies scanning | Medium | Low | Stagger scans; cache chains; throttle requests |

## Phase 2 Monthly Cost

| Item | Cost |
|---|---|
| IBKR market data | $5-10/mo |
| SendGrid email (free tier) | $0 |
| Total Phase 2 | **$5-10/mo** |
