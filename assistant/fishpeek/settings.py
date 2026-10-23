from pathlib import Path
from functools import lru_cache
from pydantic import Field, SecretStr, field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

PACKAGE = Path(__file__).resolve().parent


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_prefix="FISHPEEK_", env_file=".env", extra="ignore")
    base_url: str = "https://api.openai.com/v1"
    api_key: SecretStr = SecretStr("")
    model: str = ""
    state_dir: Path = Path(".state")
    events_file: Path = Path("../BepInEx/fishpeek/encounters.jsonl")
    model_timeout: float = Field(30, ge=1, le=90)
    agent_timeout: float = Field(90, ge=1, le=180)
    max_steps: int = Field(6, ge=1, le=12)
    max_tool_calls: int = Field(12, ge=1, le=24)
    max_tokens: int = Field(1200, ge=128, le=4096)

    @field_validator("base_url")
    @classmethod
    def validate_url(cls, value):
        from urllib.parse import urlsplit
        parsed = urlsplit(value)
        if parsed.scheme not in ("https", "http") or not parsed.hostname or parsed.username or parsed.password or parsed.query or parsed.fragment:
            raise ValueError("Use a plain HTTP(S) base URL without credentials or query parameters")
        if parsed.scheme == "http" and parsed.hostname not in ("localhost", "127.0.0.1", "::1"):
            raise ValueError("Non-local model endpoints require HTTPS")
        return value.rstrip("/")

    @property
    def configured(self):
        return bool(self.model and self.api_key.get_secret_value())


@lru_cache
def settings():
    return Settings(_env_file=PACKAGE.parent / ".env")
