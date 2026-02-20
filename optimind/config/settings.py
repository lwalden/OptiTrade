"""
System-wide settings loaded from environment variables and .env files.

All tuneable parameters live here. Hard limits are in core/constants.py.
"""

from __future__ import annotations

from typing import Annotated, Literal

from pydantic import BeforeValidator, Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        env_prefix="OPTIMIND_",
        case_sensitive=False,
    )

    # ── Mode ──────────────────────────────────────────────────────────────────
    # Annotated with BeforeValidator so lowercasing happens before Literal check
    mode: Annotated[
        Literal["paper", "live"],
        BeforeValidator(lambda v: v.lower() if isinstance(v, str) else v),
    ] = "paper"

    # ── IB Gateway ───────────────────────────────────────────────────────────
    ib_host: str = "127.0.0.1"
    ib_paper_port: int = 4002
    ib_live_port: int = 4001
    ib_client_id: int = 1
    ib_timeout: float = 10.0   # seconds to wait for connection

    @property
    def ib_port(self) -> int:
        """Return the correct port based on current mode."""
        return self.ib_paper_port if self.mode == "paper" else self.ib_live_port

    # ── Claude API ───────────────────────────────────────────────────────────
    anthropic_api_key: str = Field(default="", repr=False)
    claude_model: str = "claude-sonnet-4-6"

    # ── Database ─────────────────────────────────────────────────────────────
    database_url: str = "sqlite+aiosqlite:///./data/optimind.db"

    # ── Logging ──────────────────────────────────────────────────────────────
    log_level: Literal["DEBUG", "INFO", "WARNING", "ERROR"] = "INFO"
    log_json: bool = False   # True in production for structured JSON logs

    # ── Capital ──────────────────────────────────────────────────────────────
    account_nlv: float = 100_000.0   # Net liquidation value (updated from IBKR)

    # ── Feature flags ────────────────────────────────────────────────────────
    ai_regime_enabled: bool = True
    guided_mode: bool = True   # Require human approval before execution


# Module-level singleton — import this everywhere
settings = Settings()
