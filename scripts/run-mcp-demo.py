"""通过本机官方客户端登录执行模型 + MCP 演示，或核验已有 JSONL 记录。"""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
from fishpeek.demo import QUESTION, validate_events, render_html

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model", default="gpt-6.1-sol", help="本机账号可用的模型标识")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/mcp-demo")
    parser.add_argument("--from-events", type=Path, help="核验已有原始记录；此模式不会重新请求模型")
    parser.add_argument("--timeout", type=int, default=240)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    started = time.perf_counter()
    if args.from_events:
        stream = args.from_events.read_text(encoding="utf-8-sig")
        origin = "recorded_client_events"
    else:
        executable = shutil.which("codex")
        if not executable:
            parser.error("请先准备官方客户端，并运行 codex login 登录。")
        # 仅本次测试配置 fishpeek，不加载其他项目的 MCP。认证仍由客户端管理。
        quote = lambda value: json.dumps(str(value), ensure_ascii=False)
        config = ('mcp_servers.fishpeek={command=' + quote(sys.executable) +
                  ',args=["-m","fishpeek","mcp"],env={PYTHONUTF8="1",FISHPEEK_STATE_DIR=' +
                  quote(output / "state") + '},required=true}')
        with tempfile.TemporaryDirectory(prefix="fishpeek-demo-") as cwd:
            command = [executable, "--no-daemon", "exec", "--ignore-user-config", "--ephemeral", "--json",
                       "--color", "never", "-m", args.model, "-c", 'model_reasoning_effort="low"',
                       "-c", config, "-s", "read-only", "--skip-git-repo-check", "-C", cwd, "-"]
            try:
                result = subprocess.run(command, input=QUESTION, capture_output=True, encoding="utf-8",
                                        timeout=args.timeout, env={**os.environ, "PYTHONUTF8": "1"})
            except subprocess.TimeoutExpired:
                raise SystemExit("演示超时，本轮不生成通过记录。")
        stream = result.stdout
        (output / "events.jsonl").write_text(stream, encoding="utf-8")
        (output / "client.log").write_text(result.stderr, encoding="utf-8")
        if result.returncode:
            raise SystemExit("官方客户端执行失败，请在本机查看 client.log；本轮不生成通过记录。")
        origin = "live_client_run"
    events = [json.loads(line) for line in stream.splitlines() if line.strip()]
    record = validate_events(events)
    record.update(recorded_at=datetime.now(timezone.utc).isoformat(), requested_model=args.model,
                  record_origin=origin, question=QUESTION)
    if not args.from_events:
        record["duration_seconds"] = round(time.perf_counter() - started, 2)
    for suffix, content in (("json", json.dumps(record, ensure_ascii=False, indent=2) + "\n"),
                            ("html", render_html(record))):
        (output / f"weather-demo.{suffix}").write_text(content, encoding="utf-8")
    print(json.dumps({"validated": True, "origin": origin, "tool_calls": record["tool_call_count"],
                      "checks": len(record["checks"]), "output": str(output)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
