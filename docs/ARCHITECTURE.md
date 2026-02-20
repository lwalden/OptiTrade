# OptiMind Technical Architecture

**Last Updated:** 2026-02-20
**Status:** Canonical architecture summary

---

## System Overview

OptiMind is an async event-driven Python system that:

1. reads market and account state,
2. produces strategy candidates,
3. enforces risk limits,
4. executes approved orders,
5. monitors open positions,
6. records performance and operational telemetry.

Modes:

- `paper` for development and validation.
- `live` for staged production execution.

Mode is controlled only by `OPTIMIND_MODE`.

## Design Principles

1. Safety before alpha.
2. Deterministic core, AI as optional enhancement.
3. One canonical parameter source.
4. Backtest/runtime parity is a first-class requirement.

## Canonical Stack

- Runtime: Python 3.12+
- Backtest language: C# for LEAN
- Dependency manager: uv
- Broker API: `ib_async`
- Storage: SQLite (dev) -> PostgreSQL (prod)
- Scheduling: APScheduler
- CLI: Typer
- LLM runtime model: `claude-sonnet-4-6`

## Module Layout

- `optimind/config`: settings and strategy config.
- `optimind/core`: models, constants, events, persistence.
- `optimind/broker`: broker adapters and connection logic.
- `optimind/data`: chain, Greeks, IV and market context pipelines.
- `optimind/strategies`: strategy implementations.
- `optimind/risk`: risk checks, breakers, concentration logic.
- `optimind/execution`: order execution and guided flow.
- `optimind/monitor`: position and threat monitoring.
- `optimind/ai`: AI client and regime/review helpers.
- `optimind/mcp`: portfolio query interface for Claude tools.
- `optimind/dashboard`: operational and performance dashboard.
- `optimind/tax`: tax lot and reporting modules.

## Key Event Flows

## Trade entry flow

1. Scheduler triggers scan.
2. Strategy layer proposes candidate.
3. Risk manager approves/rejects.
4. Guided mode requests approval (if enabled).
5. Execution engine routes order.
6. Fill creates/updates position state.

## Position monitoring flow

1. Monitor updates PnL and Greeks.
2. Threat detector evaluates adjustment/exit signals.
3. Risk and execution handle actions.
4. Close events update performance and tax records.

## Regime flow

1. Quantitative regime always runs.
2. AI regime call is optional enhancement.
3. Fallback ladder on timeout/errors:
   - fresh AI,
   - cached recent AI,
   - quantitative baseline.

## Data and Integrity Rules

1. Broker data is throttled and paced.
2. Validator checks bounds and staleness.
3. Risk computations consume validated data only.
4. Combo positions require per-leg timestamp coherence.

## Configuration and Parity

- Canonical strategy parameters live in `config/strategies.yaml`.
- LEAN constants are generated artifacts from canonical config.
- Runtime and backtest parity rules are defined in `docs/BACKTEST_LIVE_PARITY.md`.

## Deployment Architecture

## Development

- Local runtime + local IB Gateway paper session.
- SQLite local DB.

## Production

- Azure VM runtime + IB Gateway live session.
- PostgreSQL managed database.
- Health monitoring and alerting.

## Production Safety Requirements

1. Runbooks and emergency controls tested pre-live.
2. Monitoring for broker connectivity, data freshness, and service liveness.
3. Manual operational access path documented.

## Security Baseline

- Secrets only via env vars.
- No credentials in repo.
- Restricted dashboard exposure.
- Explicit safeguards against accidental live execution.

## Governance References

- Canonical roadmap: `docs/PROJECT_STRATEGY.md`
- Validation gates: `docs/VALIDATION_GATES.md`
- Risk framework: `docs/RISK_FRAMEWORK.md`
- Cost model: `docs/COST_MODEL.md`
- Performance model: `docs/PERFORMANCE_MODEL.md`
- Parity controls: `docs/BACKTEST_LIVE_PARITY.md`
