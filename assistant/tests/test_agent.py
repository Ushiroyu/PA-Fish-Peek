import asyncio
import json
import httpx
import pytest
from fishpeek.agent import Agent
from fishpeek.provider import ChatProvider, ProviderError


def response(message, **kwargs):
    return httpx.Response(200, json={"choices": [{"message": {"role": "assistant", **message}}], **kwargs})


def call(name, arguments, ident="call-1"):
    return {"id": ident, "type": "function", "function": {"name": name, "arguments": json.dumps(arguments)}}


async def test_actual_http_adapter_tool_loop_and_citations(config, tools):
    config = type(config)(**(config.model_dump() | {"api_key": "fixture-secret", "model": "fixture-model"}))
    requests = []
    def handler(request):
        body = json.loads(request.content)
        requests.append(body)
        assert request.headers["authorization"] == "Bearer fixture-secret"
        if len(requests) == 1:
            return response({"tool_calls": [call("explain_candidate", {"fish_name": "流星鱼", "context": {
                "rod_id": "AE_006182_Rod", "level": 6, "location": 4, "season": 4, "weather": 64}})]})
        assert body["messages"][-1]["role"] == "tool"
        data = json.loads(body["messages"][-1]["content"])["data"]
        assert data["eligible"] and data["environment_matches"]["weather"]
        return response({"content": "流星鱼满足白名单、等级和地点条件；流星雨增加排序分。" + data["citation"]}, usage={"total_tokens": 20})
    agent = Agent(config, tools, ChatProvider(config, httpx.MockTransport(handler)))
    result = await agent.run("流星鱼为什么保留？", mode="model")
    assert result["status"] == "ok" and result["mode"] == "model"
    assert len(requests) == 2 and result["usage"]["total_tokens"] == 20
    trace = agent.trace(result["trace_id"])
    assert any(s.get("name") == "explain_candidate" for s in trace["steps"])
    assert "fixture-secret" not in json.dumps(trace)


async def test_local_mode_is_explicit_and_cited(config, tools):
    result = await Agent(config, tools).run("F10 怎么用", mode="local")
    assert result["mode"] == "local" and result["status"] == "ok"
    assert "[S" in result["answer"] and result["sources"]


async def test_no_credentials_returns_actionable_code(config, tools):
    result = await Agent(config, tools).run("F10", mode="model")
    assert result["status"] == "error" and result["error"] == "model_configuration_required"


async def test_hallucinated_citation_rejected(config, tools):
    class Provider:
        async def complete(self, *_):
            return {"content": "某个不存在的结论 [S999]"}, {}
    result = await Agent(config, tools, Provider()).run("F10", mode="model")
    assert result["error"] == "answer_citation_error"


async def test_unknown_tool_error_can_be_recovered(config, tools):
    class Provider:
        count = 0
        async def complete(self, messages, _):
            self.count += 1
            if self.count == 1:
                return {"tool_calls": [call("execute_shell", {"command": "whoami"})]}, {}
            assert json.loads(messages[-1]["content"])["error"] == "unknown_tool"
            return {"content": "按 F10 开启预测。[S1]"}, {}
    result = await Agent(config, tools, Provider()).run("F10", mode="model")
    assert result["status"] == "ok"


async def test_repeated_tool_call_stops(config, tools):
    class Provider:
        async def complete(self, *_):
            return {"tool_calls": [call("inspect_weather", {"flag": 64})]}, {}
    result = await Agent(config, tools, Provider()).run("天气", mode="model")
    assert result["error"] == "repeated_tool_call"


async def test_deadline(config, tools):
    class Provider:
        async def complete(self, *_):
            await asyncio.sleep(2)
    config.agent_timeout = .05
    result = await Agent(config, tools, Provider()).run("F10", mode="model")
    assert result["error"] == "agent_timeout"


async def test_provider_error_body_never_leaks(config):
    config = type(config)(**(config.model_dump() | {"api_key": "fixture-secret", "model": "fixture-model"}))
    provider = ChatProvider(config, httpx.MockTransport(lambda request: httpx.Response(401, text="sensitive-provider-message")))
    with pytest.raises(ProviderError) as error:
        await provider.complete([], [])
    assert str(error.value) == "provider_http_401"


async def test_retry_transient_status_once(config):
    config = type(config)(**(config.model_dump() | {"api_key": "fixture-secret", "model": "fixture-model"}))
    attempts = []
    def handler(request):
        attempts.append(1)
        return httpx.Response(503) if len(attempts) == 1 else response({"content": "ok"})
    message, _ = await ChatProvider(config, httpx.MockTransport(handler)).complete([], [])
    assert message["content"] == "ok" and len(attempts) == 2


@pytest.mark.parametrize("bad_call", [{"id": [], "type": "function", "function": {}},
    {"id": "a", "type": "function", "function": None},
    {"id": "a", "type": "function", "function": {"name": []}}])
async def test_malformed_model_tool_response_is_contained(config, tools, bad_call):
    class Provider:
        async def complete(self, *_):
            return {"tool_calls": [bad_call]}, {}
    result = await Agent(config, tools, Provider()).run("F10", mode="model")
    assert result["status"] == "error" and result["error"] == "invalid_tool_calls"


async def test_step_budget(config, tools):
    class Provider:
        index = 0
        async def complete(self, *_):
            self.index += 1
            return {"tool_calls": [call("inspect_weather", {"flag": 1 << self.index})]}, {}
    config.max_steps = 2
    result = await Agent(config, tools, Provider()).run("天气", mode="model")
    assert result["error"] == "step_budget_exceeded"


async def test_malformed_arguments_can_be_corrected(config, tools):
    class Provider:
        count = 0
        async def complete(self, messages, _):
            self.count += 1
            if self.count == 1:
                broken = call("inspect_weather", {})
                broken["function"]["arguments"] = "not-json"
                return {"tool_calls": [broken]}, {}
            assert json.loads(messages[-1]["content"])["error"] == "invalid_json_arguments"
            return {"content": "天气说明见引用。[S1]"}, {}
    result = await Agent(config, tools, Provider()).run("天气", mode="model")
    assert result["status"] == "ok"
