# Phase 4: Backtesting, Optimization & Production
## Weeks 29-40+ | ~15-20 hrs/week | Total: ~180-240 hours

**Phase Goal:** Build performance analytics dashboard, run walk-forward optimization using the Phase 1 LEAN backtests to confirm production parameter choices, add second broker adapter (Tradier), harden for production, deploy to Azure, and execute first live trade.

**Entry Criteria:** Phase 3 complete — AI-enhanced paper trading running 8+ weeks with per-strategy and AI-vs-static comparison data.

**Exit Criteria:**
- [ ] Walk-forward optimization (using Phase 1 LEAN backtests) confirms live parameters within 2% CAGR of historical results
- [ ] Performance dashboard operational (Streamlit or React)
- [ ] Tradier broker adapter functional (secondary broker)
- [ ] System deployed to Azure VM with 24/7 uptime
- [ ] Automated startup, health monitoring, crash recovery
- [ ] Tax reporting module generates Schedule D data
- [ ] System-wide toggle switched from PAPER to LIVE
- [ ] First live iron condor executed successfully

---

### Sprint 4.1: Walk-Forward Optimization & Paper Trading Validation (Weeks 29-31)

**Note:** The initial QuantConnect LEAN backtests were built in Phase 1 Sprint 1.0 (C# algorithms, 2019-2025 backtest, walk-forward parameter validation). This sprint focuses on comparing those predictions against the actual paper trading data accumulated during Phases 2-3, and locking final production parameters.

**Deliverables:**
- Paper trading results compared to Phase 1 backtest predictions
- Updated walk-forward using 2024-2025 out-of-sample data
- Final production parameters locked in `config/strategies.yaml`
- Config Translator re-run to confirm C# backtests are in sync with final Python config

**Tasks:**
```
4.1.1  Compare backtest predictions to paper trading results
       - Pull paper trading results from SQLite: actual CAGR, win rate, profit factor,
         max drawdown by strategy
       - Compare to Phase 1 Sprint 1.0 backtest results for same parameters
       - If within 2% CAGR: validation passed — proceed to production parameters
       - If >2% divergence: investigate root cause:
         * Execution slippage (paper fills vs. backtest fills)
         * IV regime differences (was paper trading period representative?)
         * Logic divergence (does live code match backtest algorithm exactly?)
       - Document findings in DECISIONS.md

4.1.2  Run updated walk-forward with expanded out-of-sample data
       - Phase 1 used 2023-2025 as out-of-sample (limited data at the time)
       - Now update: in-sample 2019-2022, out-of-sample 2023-present
       - Re-run parameter sensitivity grid from Sprint 1.0.5 with more data
       - Lock final production parameters — update `config/strategies.yaml`
       - Run Config Translator (`scripts/generate_lean_config.py`) to regenerate
         `backtests/lean/Config/StrategyConstants.cs` from updated YAML

4.1.3  Lock production parameters
       - Document final parameter rationale in DECISIONS.md
       - Any deviation from Phase 1 defaults requires evidence:
         * Backtest improvement on out-of-sample data
         * Paper trading data supporting the change
       - These are the parameters the live system will trade
```

---

### Sprint 4.2: Performance Analytics Dashboard (Weeks 33-35)

**Deliverables:**
- Web-based dashboard for portfolio monitoring and analytics
- Real-time position display, P&L charts, Greeks visualization
- Historical performance analysis and reporting
- Tax lot tracking for year-end reporting

**Tasks:**
```
4.2.1  Build Streamlit dashboard (MVP)
       - Why Streamlit: fastest path to functional dashboard in Python
       - Pages:
         * Dashboard: account summary, daily P&L, open positions
         * Positions: detailed view of each open position with Greeks
         * Performance: equity curve, drawdown chart, monthly returns table
         * Strategies: per-strategy metrics and comparison
         * Risk: current risk utilization vs limits, portfolio Greeks
         * Trade Log: searchable trade history with rationale
         * AI Insights: latest regime assessment, strategy weights

4.2.2  Build real-time data feeds for dashboard
       - WebSocket connection to position monitor service
       - Auto-refresh every 60 seconds during market hours
       - Stale data indicator if feed disconnects
       - Mobile-responsive layout (monitor on phone)

4.2.3  Build performance analytics
       - Equity curve with benchmark overlay (SPX total return)
       - Drawdown chart (underwater plot)
       - Monthly returns heat map
       - Win rate by strategy, by month, by underlying
       - Profit factor (gross profit / gross loss)
       - Sharpe ratio (rolling 90-day)
       - Sortino ratio (penalizes downside vol only)
       - Risk-adjusted return vs SPX buy-and-hold

4.2.4  Build tax reporting module
       - Track every trade as a tax lot:
         * Open date, close date, proceeds, cost basis, gain/loss
       - Section 1256 contracts (SPX, XSP):
         * Mark-to-market at year-end (even if open)
         * 60% long-term, 40% short-term treatment
         * Form 6781 data export
       - Non-1256 contracts (SPY, individual stocks):
         * Standard capital gains treatment
         * Wash sale tracking (31-day lookback)
       - Export: CSV for accountant, PDF summary for records
       - This saves $500-1,000/year in CPA fees

4.2.5  Build reporting exports
       - Daily summary email: positions, P&L, alerts
       - Weekly report: performance vs benchmark, risk utilization
       - Monthly report: strategy comparison, tax impact, regime analysis
       - On-demand: full trade journal export (for CPA or self-review)
```

---

### Sprint 4.3: Second Broker & Production Hardening (Weeks 36-38)

**Deliverables:**
- Tradier broker adapter (second broker support)
- Production error handling, crash recovery, health monitoring
- Azure VM deployment with automated startup
- Comprehensive testing suite

**Tasks:**
```
4.3.1  Implement Tradier broker adapter
       - TradierAdapter implementing BrokerAdapter interface (from Phase 1)
       - Tradier REST API integration:
         * Account data, positions, orders
         * Options chain retrieval
         * Multi-leg order submission
       - Commission comparison logic:
         * IBKR: $0.65/contract
         * Tradier: $0/contract with $10/mo Pro subscription
         * For 50+ contracts/month, Tradier is cheaper
       - Broker selection per trade (future capability)
       - Note: Tradier lacks combo order types — multi-leg submitted as individual legs

4.3.2  Build production error handling
       - Exception hierarchy:
         * ConnectionError → auto-reconnect, alert if 3 failures
         * OrderRejected → log reason, alert, don't retry
         * PartialFill → monitor, alert, suggest manual intervention
         * DataError → fall back to cached data, alert
         * UnknownError → halt new activity, alert, require manual review
       - All errors logged with full context for debugging
       - Error rate monitoring (>3 errors/hour → alert)

4.3.3  Build health monitoring
       - System health checks every 5 minutes:
         * Broker connection alive?
         * Market data flowing? (stale data detection)
         * Database accessible?
         * API budget within limits?
         * All positions accounted for? (reconciliation)
       - Health endpoint for external monitoring (Azure Monitor)
       - Automated restart on certain failure types
       - Dead man's switch: if no heartbeat for 15 minutes, alert via SMS

4.3.4  Deploy to Azure VM
       - Azure VM: D2s v5 (~$80/mo) — 2 vCPU, 8GB RAM
       - IB Gateway running in headless mode (IBC — IB Controller)
       - OptiMind as systemd service with auto-restart
       - PostgreSQL on Azure Database for PostgreSQL ($25/mo flexible tier)
       - Log aggregation via Azure Monitor
       - Backup: daily database backup to Azure Blob Storage
       - SSL/VPN for secure access to dashboard

4.3.5  Build comprehensive test suite
       - Unit tests: every module, especially risk manager
       - Integration tests: broker connection, order flow, monitoring
       - Scenario tests:
         * Market crash (VIX spikes to 40) → circuit breaker fires
         * Multiple positions hit stop simultaneously → orderly closeout
         * Broker disconnection during order → no duplicate orders
         * Overnight gap → positions reassessed at open
       - Run tests in CI (GitHub Actions) on every PR
       - Minimum coverage: 80% for risk and execution modules
```

---

### Sprint 4.4: Go Live (Weeks 39-40+)

**Deliverables:**
- Final pre-live checklist completed
- System-wide toggle switched to LIVE
- First live iron condor executed
- 30-day live monitoring period

**Tasks:**
```
4.4.1  Pre-live checklist
       - [ ] Paper trading data: 12+ weeks of continuous operation
       - [ ] Backtests validate paper results within 2% CAGR
       - [ ] All risk limits tested and verified (unit + integration)
       - [ ] Circuit breakers tested with simulated scenarios
       - [ ] Azure deployment stable for 2+ weeks
       - [ ] Health monitoring and alerting verified
       - [ ] Emergency procedures documented:
             * How to halt all trading immediately
             * How to close all positions manually
             * How to switch back to paper mode
             * Who to call at IBKR if something goes wrong
       - [ ] Tax reporting module generates correct sample output
       - [ ] $400K funded in IBKR account
       - [ ] Portfolio Margin approved (requires $110K+ and application)
       - [ ] Options Level 3+ approved

4.4.2  Staged go-live
       - Week 1 live: ONE iron condor, 1 contract, ~$500 risk
         * Verify real fills match expected pricing
         * Verify real commissions match expected
         * Verify position monitoring works with real data
         * Verify exit logic works with real orders
       - Week 2 live: TWO iron condors, still 1 contract each
         * Verify multi-position management
         * Verify portfolio Greeks calculation
       - Week 3 live: increase to normal position sizing (2-3 contracts)
         * Normal risk per trade (2.5% of NLV)
       - Week 4 live: add second strategy (credit spread)
       - Month 2+: gradually enable all strategies per AI allocation

4.4.3  Live monitoring protocol (first 90 days)
       - Daily: review all positions, P&L, risk status
       - Weekly: compare live results to paper trading baseline
       - Monthly: full performance review vs backtest expectations
       - Immediate alerts for:
         * Any risk limit breach
         * Any position loss > 1% of NLV
         * Any execution anomaly (fill price > 5% from expected)
         * System downtime > 5 minutes during market hours

4.4.4  Iterate and optimize
       - Based on live results, adjust parameters:
         * If win rate lower than backtest → widen stops or reduce size
         * If slippage higher than paper → adjust SmartPricing parameters
         * If certain strategies underperform → reduce allocation or disable
       - Monthly strategy review with AI performance comparison
       - Quarterly: full system audit and optimization cycle
```

---

## Phase 4 Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Backtest results don't match paper trading | Medium | Medium | Investigate divergence; may need parameter adjustment or bug fixing |
| Live fills worse than paper fills | High | Medium | SmartPricing helps; budget 5-10% slippage vs paper; wider stops initially |
| Azure VM reliability | Low | High | Auto-restart, health monitoring, failover alert to phone |
| IB Gateway crashes on Azure | Medium | Medium | IBC (IB Controller) manages restarts; documented IBKR Gateway stability issue |
| First live trade goes wrong | Low | Medium | Single contract sizing limits max loss to $500; manual override ready |
| Portfolio Margin not approved | Low | Medium | Reg-T margin still works; PM just improves capital efficiency |

## Phase 4 Monthly Cost (Production)

| Item | Cost |
|---|---|
| Azure VM (D2s v5) | $70-85/mo |
| Azure PostgreSQL (flexible) | $25-40/mo |
| Azure Monitor + Blob backup | $10-15/mo |
| IBKR market data | $5-10/mo |
| IBKR commissions (~50 contracts/mo) | $32-65/mo |
| ORATS IV data | $50-150/mo |
| Claude API | $20-50/mo |
| Tradier Pro (if used) | $10/mo |
| **Total Production** | **$220-425/mo** |

Against $400K generating 8-15% ($32K-$60K annually):
- Operating costs: $2,640-5,100/year
- As percentage of gross returns: 4-16% of gross return range
- **Break-even requires roughly 0.7-1.3% annual return before OptiMind is "free"**
- At target 10% return ($40K): net income after costs ~$35K-37K/year
- At target 8% return ($32K): net income after costs ~$27K-29K/year
- At target 15% return ($60K): net income after costs ~$55K-57K/year

---

## Timeline Summary

| Phase | Weeks | Calendar (at 15-20 hrs/wk) | Milestone |
|---|---|---|---|
| **Phase 1** | 1-8 | Months 1-2 | First paper iron condor lifecycle |
| **Phase 2** | 9-18 | Months 3-4.5 | Multi-strategy paper trading running |
| **Phase 3** | 19-28 | Months 5-7 | AI-enhanced paper trading |
| **Phase 4** | 29-40 | Months 7.5-10 | First live trade |
| **Stabilization** | 41-52 | Months 10-13 | Full production, 90-day live track record |

**Total: ~10-13 months from start to stable production**
**Total development hours: ~600-800 hours**
**Total cost during development: ~$500-2,000 (mostly Phase 3-4 data/hosting)**
