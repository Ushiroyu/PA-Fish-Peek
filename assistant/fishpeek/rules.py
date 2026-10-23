import json
from functools import lru_cache
from typing import Literal
from pydantic import BaseModel, ConfigDict, Field, model_validator, field_validator
from .settings import PACKAGE

LOCATIONS = {1: "河湾", 2: "码头", 4: "岸边", 8: "河中央", 16: "沉船", 32: "巨石", 64: "冰洞", 128: "池塘"}
WEATHER = {1: "晴朗", 2: "雾", 4: "小雪", 8: "大雪", 16: "小雨", 32: "大雨", 64: "流星雨"}
WEIGHT_FLAGS = [32, 2, 8, 1, 4, 16, 64, 128]


@lru_cache
def catalog():
    return json.loads((PACKAGE / "data/catalog.json").read_text(encoding="utf-8"))


class QueryContext(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)
    rod_id: str | None = Field(None, max_length=100)
    mode: Literal["route", "ice", "pond"] = "route"
    accessory: Literal[1, 2] = 2
    level: int = Field(6, ge=1, le=6)
    location: Literal[0, 1, 2, 4, 8, 16, 32, 64, 128] = 0
    season: Literal[0, 1, 2, 4, 8] = 0
    time: int = Field(0, ge=0, le=1 << 20)
    weather: int = Field(0, ge=0, le=1 << 20)

    @field_validator("accessory", "location", "season", mode="before")
    @classmethod
    def reject_boolean_flags(cls, value):
        if isinstance(value, bool):
            raise ValueError("Use integer flags rather than booleans")
        return value

    @model_validator(mode="after")
    def coherent(self):
        mode = "ice" if self.location == 64 else "pond" if self.location == 128 else "route" if self.location else None
        if mode and mode != self.mode:
            raise ValueError("location and mode conflict")
        for flag in (self.time, self.weather):
            if flag and flag & (flag - 1):
                raise ValueError("time and weather must be a single flag or 0 (unknown)")
        return self


def rod_for(identifier):
    for rod in catalog()["rods"]:
        if identifier in (rod["id"], rod["uid"], rod["guid"]):
            return rod
    raise ValueError("Unknown rod identifier")


def evaluate(fish, context: QueryContext):
    rod = rod_for(context.rod_id) if context.rod_id else None
    allowed = rod["fish"] if rod else catalog()["categories"][context.mode][str(context.accessory)]
    reasons = []
    if fish["name"] not in allowed:
        reasons.append("not_in_rod_whitelist")
    if fish["level"] != context.level:
        reasons.append("level_mismatch")
    mode_mask = {"route": 63, "ice": 64, "pond": 128}[context.mode]
    location = context.location or mode_mask
    if not fish["location"] & location:
        reasons.append("location_mismatch")
    if rod and not rod["location"] & location:
        reasons.append("rod_location_mismatch")
    if context.mode == "pond":
        if not context.season:
            reasons.append("pond_season_required")
        elif not fish["season"] & context.season:
            reasons.append("pond_season_mismatch")
    signals = {key: bool(getattr(context, key) and fish[key] & getattr(context, key)) for key in ("season", "time", "weather")}
    if context.location:
        base = fish["weights"][WEIGHT_FLAGS.index(context.location)]
    elif context.mode == "route":
        base = max(fish["weights"][:6])
    else:
        base = fish["weights"][6 if context.mode == "ice" else 7]
    rod_weight = max(rod["fish"].get(fish["name"], 0), .01) if rod else 1
    score = max(base, .01) * fish["factors"][sum(signals.values())] * rod_weight
    return dict(name=fish["name"], eligible=not reasons, reasons=reasons, environment_matches=signals,
                score=round(score, 6), source=f"catalog:{catalog()['version']}:{fish['name']}")


def query_candidates(context: QueryContext):
    results = [evaluate(fish, context) for fish in catalog()["fish"]]
    eligible = sorted((r for r in results if r["eligible"]), key=lambda r: (-r["score"], r["name"]))
    return dict(version=catalog()["version"], context=context.model_dump(), candidates=eligible,
                count=len(eligible), semantics="条件筛选与环境排序分数，不是出鱼概率；使用显式条件，不自动切换竿型或放宽地点。")


def explain_candidate(fish_name: str, context: QueryContext):
    fish = next((f for f in catalog()["fish"] if f["name"] == fish_name), None)
    if fish is None:
        raise ValueError("Unknown fish name")
    return dict(context=context.model_dump(), fish=fish, **evaluate(fish, context))
