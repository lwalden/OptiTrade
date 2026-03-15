---
description: Architecture fitness rules — structural constraints for this project
---

# Architecture Fitness Rules
# AIAgentMinder-managed. Customize the rules below to match your project's architecture.
# Delete this file to opt out of architecture fitness enforcement.

## How to Use This File

These rules are enforced by Claude during code review, PR creation, and when writing new code.
Each rule is specific enough to check mechanically.

---

## Structural Constraints

### Layer Boundaries

**Broker layer is the only place allowed to import `ib_async` or `httpx`.**
All external broker I/O must go through `optimind/broker/`. No module outside `broker/` may directly
instantiate `ib_async.IB`, call `ib.reqMktData()`, or make HTTP calls to Tradier.

```
Bad:  from ib_async import IB  # inside optimind/risk/ or optimind/strategies/
Good: from optimind.broker.ibkr.adapter import IBKRAdapter
```

**Risk checks must complete before any order reaches the broker layer.**
`optimind/execution/` must call `optimind/risk/` and receive `RiskCheckResult.approved == True`
before constructing or submitting any order. The execution layer must never bypass the risk layer.

**`optimind/core/` has no upward imports.**
`core/models.py` and `core/constants.py` must not import from any other `optimind/` subpackage.
They are the foundation — all other layers import from core, never the reverse.

```
Bad:  from optimind.broker.ibkr.connection import IBKRConnection  # inside core/
Good: from optimind.core.models import Position  # anywhere else importing core
```

**`core/constants.py` risk limits are read-only.**
No module may modify or monkey-patch values in `core/constants.py` at runtime.
Risk limit overrides belong in `config/risk_limits.yaml` and must have a floor enforced against constants.

**AI layer is non-fatal by design.**
`optimind/ai/` calls to the Anthropic API must be wrapped so that any `anthropic` exception
causes graceful degradation to the quantitative baseline. The trading loop must never block on
an AI call — set a 5-second timeout and fall back silently.

**Event bus for cross-module communication (once implemented).**
Once `core/events.py` exists, modules in the trading loop (monitor, strategies, risk, execution) must
communicate via the event bus, not by importing each other directly.

### External API Calls

**IBKR calls are rate-limited to 40 messages/second.**
Any code that calls `ib_async` data request methods (`reqMktData`, `reqHistoricalData`, etc.)
must use the semaphore/throttle mechanism in `broker/ibkr/connection.py`. Never call raw
`ib_async` methods without rate-limit protection.

**Anthropic API calls belong only in `optimind/ai/`.**
No module outside `optimind/ai/` may import `anthropic` or call the Claude API.
If another module needs an AI result, it calls a function in `optimind/ai/` and handles
the `None` / fallback case.

**No live IBKR port (4001) in tests.**
Tests must never connect to `127.0.0.1:4001` (live) or `127.0.0.1:4002` (paper).
All `ib_async.IB` usage in tests must be mocked with `unittest.mock`.

**Credentials never in code.**
API keys, passwords, and tokens must come from environment variables (`.env` file, gitignored).
Pydantic `SecretStr` must be used for all secret fields in `config/settings.py`.
No hardcoded credential strings anywhere in source.

### Test Isolation

**`env_isolation` fixture is autouse in `conftest.py`.**
All tests run with a clean `OPTIMIND_*` environment — the autouse `env_isolation` fixture
in `tests/conftest.py` clears these vars before each test. Never rely on env vars being set
from the shell or `.env` file in unit/integration tests.

**Test files must not import from other test files.**
Shared setup lives in `conftest.py` or a `tests/helpers/` module.
No `from tests.broker.test_connection import some_fixture` across test files.

**Async tests use `pytest-anyio` or `pytest.mark.asyncio`; no `asyncio.run()` in tests.**
All async test functions must be decorated with `@pytest.mark.asyncio` (or rely on
`asyncio_mode = "auto"` from `pyproject.toml`). Never call `asyncio.run()` inside a test.

**Gate evaluator tests use fixture JSON, not live LEAN output.**
`tests/scripts/test_evaluate_phase1_gate.py` must use deterministic fixture JSON files,
never files from `backtests/lean/results/` (which change across runs).

### File Size Limits

**Source files over 300 lines warrant decomposition review.**
If a file in `optimind/` exceeds 300 lines, flag it before adding more code.
A file that large usually has more than one responsibility. Split by concern and note in DECISIONS.md.

**Scripts in `scripts/` cap at 250 lines.**
Scripts are standalone utilities. If a script exceeds 250 lines, extract reusable logic
into `optimind/` and import it.

**Test files cap at 300 lines.**
If a test file grows past 300 lines, split by concern (e.g., `test_connection_connect.py`
and `test_connection_data.py`) or extract shared helpers into `conftest.py`.

### Generated Code

**`backtests/lean/Config/StrategyConstants.cs` is never edited manually.**
This file is auto-generated by `scripts/generate_lean_config.py` from `optimind/config/strategies.yaml`.
All parameter changes go in `strategies.yaml` only, then regenerate. The file header contains the
`parameter_hash` — mismatches caught by `evaluate_phase1_gate.py` indicate a manual edit.

### Mode Safety

**All paper/live branching uses `settings.mode`, not hardcoded strings.**
When code must behave differently in paper vs live mode, gate it on `settings.mode == "live"`.
Never hardcode port numbers (4001/4002) outside of `config/settings.py`.

**Default mode is always paper.**
`Settings.mode` defaults to `"paper"`. Any code path that initializes `Settings` without
an explicit override must end up in paper mode. Never set `mode = "live"` as a default.

---

## Enforcement

When writing or reviewing code:

1. Check each constraint above before creating or modifying a file in scope.
2. If a constraint would be violated: explain the rule, show the compliant alternative, and implement the compliant version.
3. If there's a legitimate exception: document it in a code comment (`# Architecture exception: [reason]`) and note it in DECISIONS.md.
