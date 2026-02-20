# OptiMind: Risk Management Framework
## Complete Specification

**Last Updated:** 2026-02-19
**Status:** Design — to be implemented in Phase 2, Sprint 2.2

---

## Philosophy

Risk management is the single most important component of this system. The AI layer generates alpha. The risk layer ensures survival. Every trade that loses money is expected. Every trade that blows up the account is a risk management failure.

The Optionetics approach died not because the strategies were bad, but because practitioners underestimated tail risk and over-leveraged. This system's risk framework is designed to survive the events that kill option sellers:

- March 2020 COVID crash (VIX 82, SPX -34% in 23 trading days)
- February 2018 Volmageddon (VIX from 17 to 50 in 1 day)
- August 2015 China devaluation flash crash (SPX gap down 5% at open)
- October 2014 Ebola scare (VIX doubled in 2 weeks)

**Design principle:** If the worst 1-day and 1-week moves in the last 30 years happened tomorrow, no single event should lose more than 10% of the account.

---

## Risk Hierarchy

### Level 1: Per-Trade Risk (Pre-Trade Checks)
Enforced BEFORE any order is submitted.

| Check | Limit | Implementation |
|---|---|---|
| Max risk per trade | 2.5% of NLV | `max_loss <= NLV * 0.025` |
| Position sizing | Based on max risk and spread width | `contracts = floor(max_risk / (spread_width * 100))` |
| Underlying limit | Max 2 positions per underlying | Count existing positions |
| Sector limit | Max 3 positions per sector | Sector mapping lookup |
| Liquidity check | Bid-ask spread < $0.50 on combo | Check live quotes |
| Margin check | Order won't exceed 60% Reg-T / 40% PM | Query IBKR margin |

**Example sizing calculation:**
- Account NLV: $120,000
- Max risk per trade: $120,000 × 2.5% = $3,000
- SPX iron condor, $50 wide wings
- Max risk per contract: $50 × 100 = $5,000 - credit received (~$3.00 × 100 = $300) = $4,700
- Max contracts: floor($3,000 / $4,700) = 0 contracts — TOO RISKY for $50 wings at 2.5%
- Solution: use $25 wings → max risk $2,200/contract → 1 contract
- Or: use SPY ($5 wings) → max risk $350/contract → 8 contracts

**This is a critical insight: SPX $50-wide iron condors require either larger accounts or lower risk-per-trade limits. The system must size correctly.**

### Level 2: Portfolio Risk (Continuous Monitoring)
Enforced continuously during market hours.

| Metric | Warning | Hard Limit | Action at Limit |
|---|---|---|---|
| Total deployed capital | 35% of NLV | 40% of NLV | Reject new entries |
| Portfolio delta | ±7% of NLV | ±10% of NLV | Alert → suggest hedging trade |
| Portfolio vega | N/A | 1.5% of NLV | Alert → suggest reducing exposure |
| Portfolio gamma (< 7 DTE) | Alert any negative gamma | N/A | Suggest closing near-expiry positions |
| Margin utilization | 50% | 60% Reg-T / 40% PM | Reject new entries |

### Level 3: Temporal Limits (Circuit Breakers)
Enforced on a rolling basis.

| Period | Limit | Action When Hit | Reset |
|---|---|---|---|
| Daily | -3% of NLV | Halt new entries | Next trading day |
| Daily (emergency) | -5% of NLV | Close ALL positions | Manual restart required |
| Weekly | -5% of NLV | Halt new entries | Monday of next week |
| Monthly | -10% of NLV | Halt all activity | First of next month |

**Daily -5% emergency close procedure:**
1. System detects -5% NLV loss
2. ALL open positions queued for immediate close
3. Close orders submitted at market price (not limit — speed over price)
4. Notification sent: email + SMS
5. System enters LOCKDOWN state
6. Resume requires CLI command: `optimind unlock --confirm-review`
7. User must acknowledge they've reviewed positions and understand the loss

### Level 4: Existential Risk Protection
These protect against scenarios that destroy accounts.

| Scenario | Protection |
|---|---|
| Flash crash (5%+ gap down) | Wing protection limits loss to spread width; never sell naked |
| Extended crash (20%+ over weeks) | Circuit breakers progressively reduce exposure; -10% monthly halts all |
| Broker API failure during crash | Positions have defined max loss (all spreads); no naked exposure |
| Multiple positions correlated | Sector limits (max 3); SPX treated as 50% correlated to all |
| Overnight gap beyond wings | Defined-risk spreads only; max loss = spread width - credit |
| VIX spike invalidates Greeks | CRISIS regime halts new entries; existing positions managed conservatively |

---

## Position Adjustment Rules

### When to Adjust
| Condition | Action |
|---|---|
| Short strike delta > 0.50 (was 0.30) | Consider rolling |
| Underlying within 50% of short strike | YELLOW alert, prepare to roll |
| Underlying breaches short strike | RED alert, must act immediately |
| Unrealized loss > 100% of credit | Consider closing |
| DTE < 21 and position profitable | Tighten exit to 25% |
| DTE < 14 regardless of P&L | Close position |
| DTE < 7 (emergency) | Close immediately at market |

### How to Adjust
1. **Roll the threatened side** (preferred)
   - Close threatened spread
   - Open new spread at same delta target but further OTM
   - Roll only if net credit maintained or roll cost < 50% of original credit
   - Maximum 2 rolls per position

2. **Transform the position**
   - Iron condor → single credit spread (close profitable side)
   - Useful when one side is clearly safe and other is threatened

3. **Close the position**
   - Accept the loss and move on
   - Required when: max rolls exhausted, VIX is spiking, or loss > 1.5x max acceptable

4. **Do nothing** (sometimes correct)
   - If theta working in your favor and expiration near
   - If volatility spike is expected to reverse quickly
   - Only valid at GREEN threat level

### Adjustment Budget
Each position has an adjustment budget: the total additional risk acceptable from adjustments.
- Initial adjustment budget = 50% of original credit received
- After adjustment 1: budget reduced by roll cost
- After adjustment 2: if budget remaining, one more roll allowed
- If budget exhausted: close position, no more adjustments

---

## Stress Test Scenarios

The risk framework must survive these historical scenarios:

### Scenario 1: COVID Crash (Feb 19 - Mar 23, 2020)
- SPX: 3,386 → 2,237 (-34%)
- VIX: 14.38 → 82.69
- Timeline: 23 trading days

**Expected system behavior:**
- Day 1-3 (SPX -6%): Daily -3% circuit breaker fires → no new entries
- Day 4-5 (SPX -12%): Daily -5% emergency → close all positions
- Remaining 18 days: LOCKDOWN mode, no trades
- Maximum account loss: 5% (-5% emergency stop) + slippage on closing = ~6-7%
- Without risk management: iron condor sellers lost 30-50% of accounts

### Scenario 2: Volmageddon (Feb 5, 2018)
- VIX: 17 → 50 in one day
- SPX: -4.1% intraday

**Expected system behavior:**
- Intraday: -3% circuit breaker fires within first 2 hours
- If positions already losing 5%+: emergency close triggered
- VIX spike detected → CRISIS regime → no new entries for days
- Maximum account loss: ~5% + slippage

### Scenario 3: Overnight Gap (Aug 24, 2015 China)
- SPX futures: -5% pre-market
- Market opened at levels beyond many iron condor wings

**Expected system behavior:**
- At market open: positions already breached
- All spreads are defined-risk → max loss = spread width - credit
- With proper sizing (2.5% risk per trade, 40% deployed): ~4-5% account loss
- System closes all positions at open, enters LOCKDOWN
- The wings did their job — without them (naked puts), account would be margin-called

---

## Risk Module Testing Requirements

The risk manager is safety-critical. Testing must be thorough.

### Unit Tests (minimum coverage: 95%)
- Every risk check tested with pass and fail cases
- Edge cases: exactly at limit, just above/below
- Multiple positions approaching limits simultaneously
- Circuit breaker activation and reset logic
- Position sizing calculation for various spread widths and account sizes

### Integration Tests
- Full order flow with risk checks
- Simultaneous risk limit violations (which takes precedence?)
- Risk check performance (must complete in < 100ms)
- Database logging of all risk events

### Scenario Tests
- Simulated crash scenario with multiple positions
- Rapid market move triggering cascading circuit breakers
- Risk check with stale data (market data delayed)
- Broker disconnection during position close

### Property-Based Tests (Hypothesis library)
- For ANY combination of positions and market conditions:
  - Total risk never exceeds hard limits
  - Circuit breakers fire at correct thresholds
  - Position sizing never allows over-sized trades
  - All defined-risk spreads have calculable max loss

---

## Monitoring & Alerting

| Alert | Channel | Priority |
|---|---|---|
| New trade recommendation | Email | Normal |
| Adjustment needed | Email + SMS | High |
| Circuit breaker warning (-2.5% daily) | Email | High |
| Circuit breaker fired (-3% daily) | Email + SMS | Critical |
| Emergency close (-5% daily) | Email + SMS + phone | Emergency |
| Broker disconnection (>5 min) | Email + SMS | High |
| Risk limit approaching (90% of any limit) | Email | Normal |
| Margin call | Email + SMS | Critical |
| System error (execution failure) | Email + SMS | Critical |
