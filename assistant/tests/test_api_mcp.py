import asyncio
import json
import os
import sys
import pytest
from fastapi.testclient import TestClient
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client
from fishpeek.api import create_app


def test_web_query_trace_and_origin(config):
    with TestClient(create_app(config)) as client:
        assert client.get("/").status_code == 200
        assert client.get("/api/health").json()["fish_count"] == 76
        result = client.post("/api/chat", json={"question": "流星鱼什么天气", "mode": "local"}).json()
        assert result["status"] == "ok" and result["sources"]
        assert client.get("/api/traces/" + result["trace_id"]).json()["steps"]
        assert client.post("/api/chat", json={"question": ""}).status_code == 422
        assert client.post("/api/chat", json={"question": "x"}, headers={"origin": "https://untrusted.invalid"}).status_code == 403
        assert client.get("/api/health", headers={"host": "untrusted.invalid"}).status_code == 400


def test_api_does_not_expose_key(config):
    config = type(config)(**(config.model_dump() | {"api_key": "fixture-secret", "model": "fixture-model"}))
    with TestClient(create_app(config)) as client:
        text = client.get("/api/health").text + client.get("/api/tools").text
        assert "fixture-secret" not in text and '"model_configured":true' in text


async def test_mcp_initialize_list_and_call_in_real_subprocess(config):
    env = dict(os.environ, FISHPEEK_STATE_DIR=str(config.state_dir), FISHPEEK_EVENTS_FILE=str(config.events_file), PYTHONUTF8="1")
    server = StdioServerParameters(command=sys.executable, args=["-m", "fishpeek", "mcp"], env=env)
    async with asyncio.timeout(30):
        async with stdio_client(server) as (read, write):
            async with ClientSession(read, write) as session:
                await session.initialize()
                listed = await session.list_tools()
                assert len(listed.tools) == 6
                assert all(tool.annotations.readOnlyHint for tool in listed.tools)
                prompts = await session.list_prompts()
                assert prompts.prompts[0].name == "fishing_analysis"
                result = await session.call_tool("explain_candidate", {"fish_name": "流星鱼", "context": {
                    "rod_id": "AE_006182_Rod", "level": 6, "location": 4, "season": 4, "weather": 64}})
                assert not result.isError
                parsed = json.loads(result.content[0].text)
                assert parsed["ok"] and parsed["data"]["eligible"]
                bad = await session.call_tool("read_runtime_events", {"limit": 1000})
                assert not json.loads(bad.content[0].text)["ok"]
