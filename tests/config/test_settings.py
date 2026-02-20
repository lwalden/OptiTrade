"""Tests for config/settings.py."""

import pytest
from pydantic import ValidationError

from optimind.config.settings import Settings


def test_default_mode_is_paper() -> None:
    s = Settings()
    assert s.mode == "paper"


def test_paper_port_selected_in_paper_mode() -> None:
    s = Settings(mode="paper")
    assert s.ib_port == s.ib_paper_port


def test_live_port_selected_in_live_mode() -> None:
    s = Settings(mode="live")
    assert s.ib_port == s.ib_live_port


def test_paper_and_live_ports_differ() -> None:
    s = Settings()
    assert s.ib_paper_port != s.ib_live_port


def test_mode_is_case_insensitive(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("OPTIMIND_MODE", "PAPER")
    s = Settings()
    assert s.mode == "paper"


def test_invalid_mode_raises() -> None:
    with pytest.raises(ValidationError):
        Settings(mode="sim")  # type: ignore[arg-type]


def test_env_prefix(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("OPTIMIND_IB_CLIENT_ID", "42")
    s = Settings()
    assert s.ib_client_id == 42


def test_guided_mode_default_true() -> None:
    s = Settings()
    assert s.guided_mode is True


def test_ai_regime_default_enabled() -> None:
    s = Settings()
    assert s.ai_regime_enabled is True
