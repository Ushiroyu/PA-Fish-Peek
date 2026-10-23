"""核验客户端 JSONL 调用记录，生成可公开的演示摘要与静态页面。"""
import html
import json
import re
from datetime import datetime, timedelta, timezone
from .rules import QueryContext, query_candidates, explain_candidate
from .tools import TOOLS
from pydantic import ValidationError

QUESTION = (
    "只完成只读钓鱼查询演示。仅使用 fishpeek MCP 工具，不调用 shell、文件、网络或其他工具，不修改任何文件。"
    "请自行选择需要的 fishpeek 工具核对以下问题：使用 AE_006182_Rod，河边模式 route，6级，"
    "岸边 location=4，秋季 season=4，时间未知 time=0，天气从流星雨 weather=64 改成晴朗 weather=1，"
    "流星鱼是否仍在候选中，环境排序分数如何变化？先检索规则依据，实际查询两种天气的候选并解释流星鱼。"
    "使用中文回答，注明工具返回的真实 catalog 来源及 knowledge 路径和行号。score 不是概率，"
    "不臆测实时游戏状态。不预设查询结果，完全依据工具输出。"
)
CONTEXT = dict(rod_id="AE_006182_Rod", mode="route", level=6, location=4, season=4, time=0)


class EvidenceError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise EvidenceError(message)


def validate_events(events):
    """只接受真实客户端完成事件；比较工具结果、固定条件和回答引用。"""
    require(not any(e.get("type") in ("error", "turn.failed") for e in events), "client_turn_failed")
    completed = [e for e in events if e.get("type") == "turn.completed"]
    require(len(completed) == 1, "expected_one_completed_turn")
    usage = completed[0].get("usage", {})
    require(isinstance(usage.get("input_tokens"), int) and usage["input_tokens"] > 0, "missing_model_usage")
    items = [e["item"] for e in events if e.get("type") == "item.completed"]
    require(all(i["type"] in ("agent_message", "mcp_tool_call", "reasoning") for i in items), "unexpected_tool_execution")
    calls, sources = [], set()

    def collect_sources(value):
        if isinstance(value, dict):
            if isinstance(value.get("source"), str):
                sources.add(value["source"])
            for entry in value.values():
                collect_sources(entry)
        elif isinstance(value, list):
            for entry in value:
                collect_sources(entry)

    for item in items:
        if item["type"] != "mcp_tool_call":
            continue
        require(item.get("server") == "fishpeek" and not item.get("error"), "mcp_call_failed")
        require(item.get("tool") in ("search_knowledge", "query_candidates", "explain_candidate"), "unexpected_mcp_tool")
        result = item.get("result") or {}
        texts = [c["text"] for c in result.get("content", []) if c.get("type") == "text"]
        require(bool(texts), "missing_tool_output")
        protocol_error = None
        if item.get("status") == "failed":
            # SDK 的字段校验失败是非 JSON 文本；只有实际违反已声明 Schema 才允许恢复。
            try:
                TOOLS[item["tool"]][0].model_validate(item["arguments"])
            except ValidationError:
                require(texts[0].startswith(f"Error executing tool {item['tool']}:") and "validation error" in texts[0], "mcp_call_failed")
            else:
                raise EvidenceError("mcp_call_failed")
            protocol_error = texts[0]
            payload = {"ok": False, "error": "invalid_arguments", "layer": "mcp_schema"}
        else:
            require(item.get("status") == "completed", "mcp_call_failed")
            payload = json.loads(texts[0])
        require(payload.get("ok") is True or payload.get("error") == "invalid_arguments", "domain_tool_failed")
        collect_sources(payload)
        calls.append({"tool": item["tool"], "arguments": item["arguments"], "output": payload,
                      "protocol_status": item["status"], "protocol_error": protocol_error})
    failed = [c for c in calls if not c["output"]["ok"]]
    for index, call in enumerate(calls):
        if not call["output"]["ok"]:
            require(any(c["tool"] == call["tool"] and c["output"]["ok"] for c in calls[index + 1:]), "unrecovered_argument_error")
    require(any(c["tool"] == "search_knowledge" and c["output"]["ok"] and c["output"]["data"].get("matches") for c in calls), "missing_retrieval")
    comparisons = []
    for weather in (64, 1):
        context = QueryContext(**CONTEXT, weather=weather)
        expected = {"query_candidates": query_candidates(context),
                    "explain_candidate": explain_candidate("流星鱼", context)}
        for tool in expected:
            matching = [c for c in calls if c["tool"] == tool and c["output"]["ok"] and c["arguments"].get("context", {}).get("weather") == weather]
            require(bool(matching), f"missing_{tool}_{weather}")
            for call in matching:
                require(QueryContext(**call["arguments"]["context"]) == context, "query_context_changed")
                if tool == "explain_candidate":
                    require(call["arguments"].get("fish_name") == "流星鱼", "unexpected_fish")
                require(call["output"]["data"] == expected[tool], "tool_output_differs_from_catalog")
        fish = expected["explain_candidate"]
        comparisons.append({"weather": weather, "eligible": fish["eligible"], "score": fish["score"],
                            "candidate_count": expected["query_candidates"]["count"], "source": fish["source"]})
    answers = [i["text"] for i in items if i["type"] == "agent_message" and i.get("text")]
    require(bool(answers), "missing_answer")
    answer = answers[-1]
    cited = set(re.findall(r"knowledge/[\w.-]+:L\d+|catalog:[\w.-]+:[\u3400-\u9fff\w]+", answer))
    require(any(s.startswith("knowledge/") for s in cited) and any(s.startswith("catalog:") for s in cited), "missing_answer_sources")
    require(cited <= sources, "unknown_answer_source")
    require(all(str(c["score"]) in answer for c in comparisons), "answer_missing_scores")
    require("概率" in answer, "missing_score_semantics")
    return {"checks": {"completed_model_turn": True, "only_fishpeek_tools": True,
                       "two_weather_queries_and_explanations": True, "catalog_consistency": True,
                       "answer_sources_exist": True, "answer_contains_scores_and_probability_note": True,
                       "argument_errors_recovered": True},
            "tool_call_count": len(calls), "recovered_argument_errors": len(failed), "comparison": comparisons, "sources": sorted(sources),
            "usage": {k: v for k, v in usage.items() if isinstance(v, int)}, "calls": calls, "answer": answer}


def render_html(record):
    """静态回放真实记录；所有动态内容转义，页面无外部依赖。"""
    esc = lambda value: html.escape(str(value))
    recorded = datetime.fromisoformat(record["recorded_at"]).astimezone(timezone(timedelta(hours=8)))
    recorded_label = recorded.strftime("%Y-%m-%d %H:%M（北京时间）")
    rows = "".join(f"<tr><td>{'流星雨' if c['weather'] == 64 else '晴朗'}</td><td>{c['candidate_count']}</td>"
                   f"<td>{'保留' if c['eligible'] else '排除'}</td><td>{c['score']}</td></tr>" for c in record["comparison"])
    calls = "".join(f"<details><summary>{i}. {esc(c['tool'])}</summary><pre>{esc(json.dumps(c, ensure_ascii=False, indent=2))}</pre></details>"
                    for i, c in enumerate(record["calls"], 1))
    return f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>PA Fish Peek · MCP 查询演示</title><style>
body{{margin:0;background:#f4f6fa;color:#172333;font:16px/1.7 system-ui,sans-serif}}main{{max-width:1050px;margin:auto;padding:36px 24px}}
h1{{font-size:30px;margin:8px 0}}h2{{font-size:21px}}.tag{{color:#226b55;font-weight:700}}.meta{{color:#617084;font-size:14px}}
section{{background:white;border:1px solid #dce3eb;border-radius:14px;padding:20px 26px;margin:20px 0}}
table{{border-collapse:collapse;width:100%}}th,td{{text-align:left;padding:12px;border-bottom:1px solid #e3e8ef}}
pre{{white-space:pre-wrap;overflow-wrap:anywhere;font:14px/1.8 ui-monospace,monospace}}summary{{cursor:pointer;padding:12px 0;color:#295baa}}
.answer{{white-space:pre-wrap;overflow-wrap:anywhere}}.grid{{display:grid;grid-template-columns:1fr 1fr;gap:20px}}.grid section{{margin:0}}
@media(max-width:700px){{.grid{{grid-template-columns:1fr}}main{{padding:16px}}section{{padding:16px}}}}
</style><main><div class="tag">PA FISH PEEK · 真实模型 / MCP 调用记录</div><h1>天气改变，候选与分数如何变化？</h1>
<p class="meta">记录核验于 {esc(recorded_label)} · 请求模型 {esc(record['requested_model'])}<br>{record['tool_call_count']} 次工具调用 · {record['recovered_argument_errors']} 次参数错误已恢复 · {len(record['checks'])} 项记录核验通过</p>
<div class="grid"><section><h2>固定查询条件</h2><p>AE_006182_Rod · 河边模式<br>6 级 · 岸边 4 · 秋季 4 · 时间未知</p><p>仅将天气由流星雨 64 改为晴朗 1。</p></section>
<section><h2>工具实际返回</h2><table><tr><th>天气</th><th>候选数</th><th>流星鱼</th><th>分数</th></tr>{rows}</table><p class="meta">环境排序分数，不是出鱼概率。</p></section></div>
<section><h2>模型最终回答</h2><div class="answer">{esc(record['answer'])}</div></section>
<section><h2>展开查看真实工具输入与输出</h2>{calls}</section>
<p class="meta">此页面静态回放已完成的模型调用，查询条件由演示输入提供；记录范围为模型、MCP 和版本数据快照。</p></main></html>'''
