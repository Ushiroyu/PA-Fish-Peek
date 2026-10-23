import asyncio
import httpx


class ProviderError(Exception):
    def __init__(self, code):
        self.code = code
        super().__init__(code)


class ChatProvider:
    def __init__(self, settings, transport=None):
        self.settings, self.transport = settings, transport

    async def complete(self, messages, tools):
        if not self.settings.configured:
            raise ProviderError("model_configuration_required")
        payload = {"model": self.settings.model, "messages": messages, "tools": tools,
                   "tool_choice": "auto", "max_tokens": self.settings.max_tokens}
        headers = {"Authorization": "Bearer " + self.settings.api_key.get_secret_value()}
        async with httpx.AsyncClient(timeout=self.settings.model_timeout, transport=self.transport,
                                     follow_redirects=False, trust_env=False) as client:
            for attempt in range(2):
                try:
                    response = await client.post(self.settings.base_url + "/chat/completions", json=payload, headers=headers)
                    if response.status_code in (429, 502, 503, 504) and attempt == 0:
                        await asyncio.sleep(.5)
                        continue
                    if not response.is_success:
                        raise ProviderError("provider_http_" + str(response.status_code))
                    # 不将供应商错误正文、请求头或密钥写入日志。
                    if len(response.content) > 2 * 1024 * 1024:
                        raise ProviderError("provider_response_too_large")
                    data = response.json()
                    message = data["choices"][0]["message"]
                    if not isinstance(message, dict) or message.get("role") != "assistant":
                        raise ProviderError("provider_invalid_response")
                    usage = data.get("usage")
                    return message, usage if isinstance(usage, dict) else {}
                except httpx.RequestError:
                    if attempt == 0:
                        await asyncio.sleep(.5)
                        continue
                    raise ProviderError("provider_unavailable") from None
                except (ValueError, KeyError, IndexError, TypeError):
                    raise ProviderError("provider_invalid_response") from None
