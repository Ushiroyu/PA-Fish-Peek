import json
from pathlib import Path
from typing import Any
from pydantic import BaseModel, ConfigDict, Field, ValidationError
from .retrieval import Knowledge
from .rules import QueryContext, WEATHER, LOCATIONS, catalog, query_candidates, explain_candidate


class Arguments(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)


class CandidatesArgs(Arguments):
    context: QueryContext


class ExplainArgs(CandidatesArgs):
    fish_name: str = Field(min_length=1, max_length=60)


class SearchArgs(Arguments):
    query: str = Field(min_length=1, max_length=1500)
    limit: int = Field(4, ge=1, le=6)


class ListRodsArgs(Arguments):
    keyword: str = Field("", max_length=100)


class ReadEventsArgs(Arguments):
    limit: int = Field(10, ge=1, le=50)


class WeatherArgs(Arguments):
    flag: int = Field(ge=0, le=1 << 20)


class Event(Arguments):
    event_id: str = Field(max_length=64)
    observed_at: str = Field(max_length=50)
    player_id: str = Field(pattern=r"^player-\d{1,12}$")
    level: float = Field(ge=0, le=10, allow_inf_nan=False)
    tier: float = Field(ge=0, le=100, allow_inf_nan=False)
    body_type: int = Field(ge=0, le=100)
    scene: str = Field(max_length=150)


TOOLS = {
    "query_candidates": (CandidatesArgs, "按显式鱼竿、等级、地点和环境计算候选；分数不是概率。"),
    "explain_candidate": (ExplainArgs, "解释某种鱼在给定条件下被保留或排除的原因。"),
    "search_knowledge": (SearchArgs, "检索钓鱼说明和运行知识，返回来源和引用段落。"),
    "list_rods": (ListRodsArgs, "查询精确鱼竿档案名称、模式与等级，关键词可为空。"),
    "read_runtime_events": (ReadEventsArgs, "读取最近匿名遭遇事件；没有事件时返回空列表。"),
    "inspect_weather": (WeatherArgs, "解释天气位，保留未知值。"),
}


class ToolRegistry:
    def __init__(self, settings):
        self.settings = settings
        self.knowledge = Knowledge(settings.state_dir / "knowledge.sqlite3")

    def schemas(self):
        return [{"type": "function", "function": {"name": name, "description": desc,
                "parameters": model.model_json_schema()}} for name, (model, desc) in TOOLS.items()]

    def call(self, name: str, arguments: dict[str, Any]):
        if name not in TOOLS:
            return {"ok": False, "error": "unknown_tool"}
        try:
            args = TOOLS[name][0].model_validate(arguments)
            result = self._execute(name, args)
            return {"ok": True, "data": result}
        except ValidationError as exc:
            return {"ok": False, "error": "invalid_arguments", "fields": [".".join(map(str, e["loc"])) for e in exc.errors()]}
        except ValueError as exc:
            return {"ok": False, "error": "invalid_arguments", "message": str(exc)}
        except (OSError, UnicodeError, json.JSONDecodeError):
            return {"ok": False, "error": "data_unavailable"}

    def _execute(self, name, args):
        if name == "query_candidates":
            return query_candidates(args.context)
        if name == "explain_candidate":
            return explain_candidate(args.fish_name, args.context)
        if name == "search_knowledge":
            return {"matches": self.knowledge.search(args.query, args.limit)}
        if name == "list_rods":
            rods = [{k: rod[k] for k in ("id", "uid", "accessory", "rod_level", "location", "label")} for rod in catalog()["rods"]
                    if args.keyword.lower() in (rod["id"] + rod["label"]).lower()]
            return {"version": catalog()["version"], "rods": rods}
        if name == "inspect_weather":
            return {"flag": args.flag, "label": WEATHER.get(args.flag, "未知"), "known": args.flag in WEATHER}
        return self.read_events(args.limit)

    def read_events(self, limit):
        path = Path(self.settings.events_file)
        if not path.exists():
            return {"events": [], "source": "runtime", "status": "empty"}
        # 固定配置文件的有界尾读，不提供任意路径或原始日志读取工具。
        with path.open("rb") as stream:
            stream.seek(0, 2)
            size = stream.tell()
            start = max(0, size - 1024 * 1024)
            stream.seek(start)
            data = stream.read(1024 * 1024).decode("utf-8", errors="replace")
        lines = data.splitlines()
        if start:
            lines = lines[1:]
        events, rejected = [], 0
        for line in lines[-2000:]:
            try:
                events.append(Event.model_validate_json(line).model_dump())
            except (ValueError, ValidationError):
                rejected += 1
        return {"events": events[-limit:], "source": "runtime", "status": "ready" if events else "empty", "rejected_lines": rejected}
