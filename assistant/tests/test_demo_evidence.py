"""使用公开的真实调用记录测试核验器；这些测试不会请求模型。"""
from copy import deepcopy
import json
from pathlib import Path
import pytest
from fishpeek.demo import EvidenceError, validate_events, render_html

RECORD = json.loads((Path(__file__).resolve().parents[2] / "docs/examples/weather-demo.json").read_text(encoding="utf-8"))


def recorded_events():
    events = [{"type": "item.completed", "item": {
        "type": "mcp_tool_call", "server": "fishpeek", "tool": call["tool"], "arguments": deepcopy(call["arguments"]),
        "status": call.get("protocol_status", "completed"), "error": None,
        "result": {"content": [{"type": "text", "text": call.get("protocol_error") or json.dumps(call["output"], ensure_ascii=False)}]}}}
        for call in RECORD["calls"]]
    return events + [{"type": "item.completed", "item": {"type": "agent_message", "text": RECORD["answer"]}},
                     {"type": "turn.completed", "usage": RECORD["usage"]}]


def test_recorded_model_tool_chain_and_recovery():
    record = validate_events(recorded_events())
    assert record["tool_call_count"] == 6
    assert record["recovered_argument_errors"] == 1
    assert [c["score"] for c in record["comparison"]] == [3.5, 1.8]
    assert all(record["checks"].values())


@pytest.mark.parametrize("mutation,error", [
    ("incomplete", "expected_one_completed_turn"),
    ("no_usage", "missing_model_usage"),
    ("extra_tool", "unexpected_tool_execution"),
    ("failed_turn", "client_turn_failed"),
    ("unknown_source", "unknown_answer_source"),
    ("changed_context", "query_context_changed"),
    ("changed_score", "tool_output_differs_from_catalog"),
    ("unrecovered", "unrecovered_argument_error"),
])
def test_rejects_incomplete_or_inconsistent_evidence(mutation, error):
    events = recorded_events()
    if mutation == "incomplete":
        events.pop()
    elif mutation == "no_usage":
        events[-1]["usage"] = {}
    elif mutation == "extra_tool":
        events.insert(0, {"type": "item.completed", "item": {"type": "command_execution"}})
    elif mutation == "failed_turn":
        events.append({"type": "turn.failed"})
    elif mutation == "unknown_source":
        events[-2]["item"]["text"] += " knowledge/unknown.md:L99"
    elif mutation == "changed_context":
        next(e["item"] for e in events if e.get("item", {}).get("tool") == "query_candidates")["arguments"]["context"]["level"] = 5
    elif mutation == "changed_score":
        item = next(e["item"] for e in events if e.get("item", {}).get("tool") == "explain_candidate")
        payload = json.loads(item["result"]["content"][0]["text"])
        payload["data"]["score"] = 999
        item["result"]["content"][0]["text"] = json.dumps(payload)
    elif mutation == "unrecovered":
        events = [e for e in events if e.get("item", {}).get("tool") != "search_knowledge" or e["item"]["status"] == "failed"]
    with pytest.raises(EvidenceError, match=error):
        validate_events(events)


def test_replay_page_escapes_model_and_tool_content():
    record = deepcopy(RECORD)
    record["answer"] = '<script>alert("answer")</script>'
    record["calls"][0]["arguments"]["query"] = '<img src=x onerror=alert(1)>'
    page = render_html(record)
    assert "<script>" not in page and "<img src=x" not in page
    assert "&lt;script&gt;" in page and "&lt;img src=x" in page
