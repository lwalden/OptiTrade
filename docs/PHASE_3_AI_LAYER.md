# Phase 3: AI Intelligence Layer
## Weeks 19-28 | ~15-20 hrs/week | Total: ~150-200 hours

**Phase Goal:** Integrate Claude API for market regime detection and trade reasoning, build MCP server for portfolio queries, add calendar spreads and straddles, implement adaptive strategy weighting. The AI layer is what makes this system worth building custom.

**Entry Criteria:** Phase 2 complete — multi-strategy paper trading running 4+ weeks with all risk limits enforced, zero violations.

**Exit Criteria:**
- [ ] Claude API produces structured market regime assessments
- [ ] AI-informed strategy selection demonstrably differs from static allocation
- [ ] MCP server enables natural-language portfolio queries via Claude Desktop/Code
- [ ] Calendar spread strategy operational
- [ ] Straddle/strangle strategy operational (pre-earnings events)
- [ ] Adaptive strategy weighting adjusts allocation based on regime
- [ ] 8+ weeks of AI-enhanced paper trading data for comparison

---

### Sprint 3.1: Market Context Engine (Weeks 19-20)

**Deliverables:**
- Market context data collector (VIX, term structure, breadth, correlation)
- Structured market state snapshot suitable for LLM consumption
- ORATS integration for institutional IV surface data

**Tasks:**
```
3.1.1  Build market context collector
       - Data points collected every 30 minutes during market hours:
         * VIX spot price + VIX term structure (VIX, VIX3M, VIX6M, VIX1Y)
         * VIX term structure slope (contango/backwardation)
         * SPX price, 10-day realized vol, 30-day realized vol
         * IV rank and IV percentile for all watchlist underlyings
         * Put/call ratio (aggregate from CBOE)
         * Sector performance heat map (via SPDRs: XLF, XLK, XLE, etc.)
       - Store in SQLite time-series table
       - Support lookback queries ("what was VIX doing last week?")

3.1.2  Build regime classification engine (quantitative)
       - Rule-based regime detector as baseline (AI enhances, doesn't replace):
         * LOW_VOL: VIX < 15, term structure in steep contango
         * NORMAL: VIX 15-20, mild contango
         * ELEVATED: VIX 20-30, flat or mild backwardation
         * CRISIS: VIX > 30, deep backwardation
         * TRENDING_UP: SPX above 10/30/50 MA, low realized vol
         * TRENDING_DOWN: SPX below all MAs, rising realized vol
         * RANGE_BOUND: SPX oscillating between defined levels, low trend strength
       - Regime determines strategy mix (see Sprint 3.4)
       - Quantitative regime persisted; AI can override with reasoning

3.1.3  Integrate ORATS IV surface data
       - ORATS API subscription ($50-150/mo)
       - Daily download: IV surface by underlying, strike, DTE
       - Volatility skew analysis (put skew, call skew, smile shape)
       - Term structure by underlying (not just VIX)
       - IV surface anomaly detection:
         * Unusual skew steepening (earnings, events)
         * Term structure inversions (near-term fear)
         * Strike-level mispricings (potential edge)
       - Store surfaces in database for historical comparison

3.1.4  Build structured market snapshot formatter
       - Produce a JSON/structured text snapshot for LLM consumption:
         {
           "timestamp": "2026-05-15T10:30:00-04:00",
           "regime_quantitative": "NORMAL",
           "vix": { "spot": 18.5, "3m": 20.1, "slope": "contango" },
           "spx": { "price": 5420, "rv10": 12.3, "rv30": 14.1 },
           "iv_ranks": { "SPX": 45, "QQQ": 62, "IWM": 38 },
           "sector_leaders": ["XLK +1.2%", "XLF +0.8%"],
           "sector_laggards": ["XLE -1.5%", "XLU -0.3%"],
           "skew": { "SPX_25d_put": 5.2, "SPX_25d_call": -2.1 },
           "open_positions": [...],
           "portfolio_greeks": { "delta": 2300, "theta": -450, "vega": -12000 },
           "daily_pnl": -0.3,
           "weekly_pnl": 1.2
         }
       - This snapshot is the bridge between quantitative data and LLM reasoning
```

---

### Sprint 3.2: Claude API Integration (Weeks 21-23)

**Deliverables:**
- Claude API client with structured prompting for trade analysis
- Market regime assessment via LLM
- Trade rationale generation for every entry/exit/adjustment
- Cost tracking for API usage

**Tasks:**
```
3.2.1  Build Claude API client module
       - Anthropic Python SDK integration
       - Model: claude-sonnet-4-6 (cost-effective for frequent calls)
       - Structured output parsing (JSON mode)
       - Token usage tracking and cost logging
       - **Strict timeouts on ALL Claude API calls** (using `asyncio.wait_for`):
           * Regime assessment calls: 5-second timeout
           * Trade rationale calls: 5-second timeout
           * Adjustment reasoning calls: 5-second timeout
           * Portfolio review calls: 10-second timeout (weekly, more data, less time-critical)
           * On timeout: log warning with elapsed time; return None; caller uses quantitative fallback
       - **Retry logic:**
           * Retry ONLY on HTTP 529 (overloaded): exponential backoff 1s → 2s → 4s, max 2 retries
           * Do NOT retry on timeout — a slow response arriving late is useless for time-sensitive
             10:15 AM regime assessment; accept the fallback and move on
           * Do NOT retry on 4xx errors (bad request, invalid key) — these require human action
       - **Defensive exception handling:**
           * ALL Anthropic SDK exceptions (APIError, AuthenticationError, APIConnectionError,
             RateLimitError, APIStatusError) must be caught in `ai/client.py`
           * None of these may propagate to the main trading loop
           * On any exception: log full traceback, return None, caller uses quantitative fallback
       - Budget cap: $50/mo initial, alert at $30

3.2.2  Build regime analysis prompt system
       - System prompt establishing Claude as options market analyst:
         * Trained in Optionetics methodology
         * Understanding of delta-neutral strategies
         * Access to structured market data (not raw)
         * Required to produce structured JSON output
       - User prompt: market snapshot + question:
         "Given this market context, what regime are we in?
          What strategies should we favor? Any specific risks?"
       - Expected output structure:
         {
           "regime": "NORMAL_BULLISH",
           "confidence": 0.75,
           "reasoning": "VIX at 18.5 with contango suggests...",
           "strategy_bias": {
             "iron_condor": 0.6,
             "butterfly": 0.2,
             "credit_spread_bull": 0.2,
             "straddle": 0.0,
             "calendar": 0.0
           },
           "risks": ["Earnings season begins next week — avoid single-stock strategies"],
           "opportunities": ["QQQ IV rank at 62% — premium selling favorable"]
         }
       - Run twice daily: 10:15 AM and 2:00 PM ET

3.2.3  Build trade rationale generator
       - For every trade entry, generate human-readable rationale:
         Input: trade setup + market context
         Output: "Selling SPX 5200/5150 bull put spread at 45 DTE because:
                  IV rank at 52% favors premium selling, VIX term structure
                  in contango supports mean-reversion, 30-delta short put
                  provides 70% probability of profit..."
       - Store rationale in trades table
       - This becomes the trade journal — invaluable for learning and review

3.2.4  Build adjustment reasoning
       - When adjustment logic triggers (Sprint 2.3), query Claude:
         Input: position data + threat level + market context + available adjustments
         Output: ranked adjustment recommendations with reasoning
         "Recommend rolling the bull put spread because: underlying has fallen
          but VIX hasn't spiked proportionally, suggesting contained selloff.
          Rolling down $50 and out 2 weeks collects additional $0.45 credit
          while moving short strike below the 200-day MA at 5180..."
       - This is where the system genuinely outperforms platform bots

3.2.5  Build portfolio review prompt
       - Weekly comprehensive review:
         Input: all positions + weekly performance + market context
         Output: portfolio assessment with actionable insights
         {
           "overall_assessment": "Portfolio is well-balanced but slightly long delta...",
           "position_reviews": [...],
           "suggested_actions": [...],
           "risk_concerns": [...],
           "upcoming_events": ["FOMC meeting Wednesday — consider reducing vega"]
         }
       - This replaces the manual weekend review process
```

---

### Sprint 3.3: MCP Server for Portfolio Interaction (Weeks 24-25)

**Deliverables:**
- Custom MCP server exposing portfolio data and trade functions
- Integration with Claude Desktop and Claude Code
- Natural-language portfolio queries and trade discussion

**Tasks:**
```
3.3.1  Build MCP server foundation
       - Python MCP server using mcp library (pip install mcp)
       - Server exposes tools and resources:
         Tools (actions Claude can take):
           * get_portfolio_status() → current positions, Greeks, P&L
           * get_market_context() → latest market snapshot
           * get_trade_candidates() → current scanner results
           * get_position_detail(position_id) → full position data
           * get_trade_history(days=30) → recent trades with P&L
           * approve_trade(trade_id) → approve pending trade (guided mode)
           * get_risk_status() → current risk utilization vs limits
         Resources (data Claude can read):
           * portfolio://positions → live position data
           * portfolio://performance → P&L history
           * portfolio://risk → risk dashboard data

3.3.2  Build Claude Desktop integration
       - MCP server config for Claude Desktop:
         {
           "mcpServers": {
             "optimind": {
               "command": "python",
               "args": ["-m", "optimind.mcp.server"],
               "env": { "OPTIMIND_MODE": "paper" }
             }
           }
         }
       - Test natural-language queries:
         "How are my positions doing today?"
         "What's the IV rank on QQQ right now?"
         "Should I close the SPX iron condor that's at 40% profit?"
         "What trades are you recommending today?"

3.3.3  Build Claude Code integration
       - MCP server accessible from Claude Code sessions
       - Enables development workflow:
         "Check the portfolio status, then review the adjustment
          logic for the iron condor that's approaching the short put"
       - This bridges portfolio data into the development environment

3.3.4  Add trade discussion capabilities
       - Interactive trade analysis via MCP:
         User: "I'm thinking about adding a butterfly on AAPL before earnings"
         Claude (via MCP): queries get_market_context() + get_risk_status()
         "AAPL IV rank is at 78% — that's expensive for a long butterfly.
          However, you're at 25% capital deployment, so you have room.
          Risk concern: you already have 2 tech-sector positions (QQQ iron
          condor, MSFT credit spread). Adding AAPL would hit the 3-position
          sector limit. Consider waiting until the QQQ position closes."
       - This is the conversational interface to the trading system
```

---

### Sprint 3.4: Calendar Spreads, Straddles & Adaptive Weighting (Weeks 26-28)

**Deliverables:**
- Calendar spread strategy operational
- Straddle/strangle strategy operational
- Adaptive strategy weighting based on AI regime assessment
- Full strategy suite running in paper mode

**Tasks:**
```
3.4.1  Implement calendar spread strategy
       - Calendar spread (horizontal spread):
         * Sell near-term option (30 DTE)
         * Buy same-strike further-term option (60 DTE)
         * Profits from differential time decay
       - Entry criteria:
         * IV rank < 30% (options are cheap — want to buy far month)
         * OR near-term IV significantly higher than far-term (term structure inversion)
         * Range-bound underlying (low trend strength)
       - Strike selection: ATM or slightly OTM in directional bias direction
       - Exit: close when near-term expires or at 25% profit
       - Risk: defined — max loss is debit paid
       - Adjustment: if underlying trends away, roll the short leg to follow

3.4.2  Implement straddle/strangle strategy
       - Pre-earnings straddle (Fontanills volatility play):
         * Buy ATM straddle 2-3 weeks before earnings
         * IV typically rises into earnings, increasing position value
         * EXIT BEFORE EARNINGS (do NOT hold through the event)
         * Profit from IV expansion, not from the move itself
       - Entry criteria:
         * Earnings date 14-21 days away
         * Current IV rank < 40% (IV hasn't already expanded)
         * Historical pattern of IV expansion into earnings (check last 4 quarters)
       - Exit: close 1-2 days before earnings, or at 20% profit, or at -15% stop
       - This is a BUYING strategy — different risk profile than premium selling
       - Warning: win rate ~55-60%, profit factor depends on big winners

3.4.3  Build adaptive strategy weighting engine
       - Uses AI regime assessment (Sprint 3.2.2) to adjust strategy allocation:

         LOW_VOL regime:
           iron_condor: 50%, calendar: 30%, butterfly: 20%
           (premium is low but consistent; calendars benefit from cheap far months)

         NORMAL regime:
           iron_condor: 40%, credit_spread: 30%, butterfly: 20%, calendar: 10%
           (balanced approach; directional bias from AI determines spread direction)

         ELEVATED_VOL regime:
           iron_condor: 60%, credit_spread: 30%, straddle: 0%, calendar: 10%
           (rich premiums for selling; avoid buying expensive straddles)

         CRISIS regime:
           ALL STRATEGIES: 0% new entries
           Focus: manage existing positions, reduce exposure
           Resume at 50% allocation when VIX drops below 25

         TRENDING regime:
           credit_spread: 50%, butterfly: 30%, calendar: 20%
           (directional strategies; iron condors get run over in trends)

       - Weights are guidelines, not absolutes — risk manager still enforces limits
       - Compare AI-weighted vs static allocation over paper trading period

3.4.4  Build strategy performance comparison
       - Track hypothetical static allocation alongside AI-weighted
       - After 8+ weeks: compare returns, drawdowns, win rates
       - This validates whether the AI layer actually adds value
       - If it doesn't add value → simplify to static allocation (honest assessment)

3.4.5  Phase 3 integration: full strategy suite paper trading
       - All 5 strategies running simultaneously:
         1. Iron condors (SPX) — primary income engine
         2. Credit spreads (SPY/QQQ) — directional overlay
         3. Butterflies (individual stocks) — targeted plays
         4. Calendar spreads (range-bound underlyings) — decay exploitation
         5. Straddles (pre-earnings) — volatility expansion plays
       - AI regime assessment driving allocation weights
       - MCP server for portfolio interaction
       - Target: 8 weeks of data before Phase 4
```

---

## Phase 3 Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Claude API costs exceed budget | Medium | Low | claude-sonnet-4-5 is cheap; 2 calls/day + adjustments ≈ $20-30/mo; hard budget cap |
| AI regime detection doesn't outperform static | Medium | Medium | This is why we measure — if AI doesn't help, simplify. No ego about it. |
| LLM hallucination produces bad trade recommendation | Low | Medium | AI recommends, risk manager enforces hard limits from `constants.py`. LLM cannot bypass pre-trade checks. Additionally: if AI returns invalid JSON structure, client returns None and system uses quantitative fallback. |
| Claude API timeout or outage | Medium | Low | 5-second timeout fires; system automatically falls back to quantitative regime. AI failure is non-fatal by design. Emit `AI_FALLBACK_TRIGGERED` event for monitoring. |
| AI non-determinism produces inconsistent regime flipping | Low | Low | System logs both `regime_quantitative` and `regime_ai`. Quantitative baseline provides stability even if AI oscillates. Only `REGIME_CHANGED` events trigger strategy weight adjustments, dampening noise. |
| Calendar spreads/straddles add complexity without proportional returns | Medium | Low | Track per-strategy metrics; disable underperformers |
| MCP server security — portfolio data exposure | Low | High | MCP runs locally only; no network exposure; env var for mode control |
| ORATS data feed issues | Low | Medium | Cache last known data; fall back to IBKR-derived IV if ORATS unavailable |

## Phase 3 Monthly Cost

| Item | Cost |
|---|---|
| IBKR market data | $5-10/mo |
| ORATS IV data | $50-150/mo |
| Claude API (Sonnet) | $20-50/mo |
| Total Phase 3 | **$75-210/mo** |
