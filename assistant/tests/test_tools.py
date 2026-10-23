import json
import pytest
from fishpeek.retrieval import Knowledge


@pytest.mark.parametrize("question,expected", [("流星鱼需要什么天气", "流星鱼与奇虾"),
    ("候选分数是概率吗", "环境评分与概率"), ("F10 F11 怎么用", "安装与按键"),
    ("玩家数字昵称", "临时玩家标签"), ("未知天气怎么处理", "未知条件")])
def test_chinese_retrieval(tools, question, expected):
    result = tools.call("search_knowledge", {"query": question})
    assert result["ok"]
    assert expected in [row["title"] for row in result["data"]["matches"]]
    assert all(row["source"].startswith("knowledge/") for row in result["data"]["matches"])


def test_no_query_match(tools):
    assert tools.knowledge.search("zzzzzzzzzzzz") == []


def test_tools_deny_arbitrary_execution_and_paths(tools):
    assert tools.call("execute_shell", {"command": "whoami"})["error"] == "unknown_tool"
    assert tools.call("read_runtime_events", {"path": "../.env"})["error"] == "invalid_arguments"
    assert tools.call("read_runtime_events", {"limit": 1000})["error"] == "invalid_arguments"
    assert tools.call("search_knowledge", {"query": "x", "limit": -1})["error"] == "invalid_arguments"


def test_missing_events_are_not_filled_with_demo(tools):
    assert tools.read_events(10)["events"] == []
    assert tools.read_events(10)["source"] == "runtime"


def test_event_whitelist_and_partial_lines(tools, config):
    event = dict(event_id="fixture-event", observed_at="2026-10-23T00:00:00Z", player_id="player-7", level=6.0,
                 tier=1.0, body_type=1, scene="fixture-cabin")
    config.events_file.write_text(json.dumps(event) + "\n" + json.dumps(event | {"nickname": "private-name"}) + '\n{"partial":', encoding="utf-8")
    result = tools.read_events(10)
    assert len(result["events"]) == 1 and result["rejected_lines"] == 2
    assert "private-name" not in json.dumps(result)


def test_unknown_weather_preserves_numeric_value(tools):
    assert tools.call("inspect_weather", {"flag": 1024})["data"] == {"flag": 1024, "label": "未知", "known": False}
