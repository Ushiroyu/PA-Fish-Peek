import pytest
from fishpeek.settings import Settings
from fishpeek.tools import ToolRegistry


@pytest.fixture
def config(tmp_path):
    return Settings(_env_file=None, api_key="", model="", state_dir=tmp_path / "state", events_file=tmp_path / "events.jsonl")


@pytest.fixture
def tools(config):
    return ToolRegistry(config)
