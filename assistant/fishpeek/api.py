import asyncio
from typing import Literal
from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import FileResponse, JSONResponse
from starlette.middleware.trustedhost import TrustedHostMiddleware
from pydantic import BaseModel, ConfigDict, Field
from .agent import Agent
from .rules import QueryContext, catalog
from .settings import PACKAGE, Settings
from .tools import ToolRegistry


class ChatRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")
    question: str = Field(min_length=1, max_length=1500)
    context: QueryContext | None = None
    mode: Literal["auto", "local", "model"] = "auto"


class ToolRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")
    arguments: dict = Field(default_factory=dict)


def create_app(settings: Settings | None = None, provider=None):
    from .settings import settings as load_settings
    settings = settings or load_settings()
    app = FastAPI(title="PA Fish Peek 工作台", version="0.1.0")
    app.add_middleware(TrustedHostMiddleware, allowed_hosts=["localhost", "127.0.0.1", "[::1]", "testserver"])
    tools = ToolRegistry(settings)
    agent = Agent(settings, tools, provider)
    semaphore = asyncio.Semaphore(4)
    app.state.agent, app.state.tools = agent, tools

    @app.middleware("http")
    async def local_origin(request: Request, call_next):
        origin = request.headers.get("origin")
        if request.method not in ("GET", "HEAD") and origin and origin.rstrip("/") != str(request.base_url).rstrip("/"):
            return JSONResponse({"error": "origin_not_allowed"}, status_code=403)
        response = await call_next(request)
        response.headers["X-Content-Type-Options"] = "nosniff"
        response.headers["Cache-Control"] = "no-store"
        if request.url.path == "/":
            response.headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'"
        return response

    @app.get("/")
    def index():
        return FileResponse(PACKAGE / "static/index.html")

    @app.get("/api/health")
    def health():
        return {"status": "ok", "model_configured": settings.configured, "catalog_version": catalog()["version"],
                "fish_count": len(catalog()["fish"]), "rod_count": len(catalog()["rods"])}

    @app.get("/api/tools")
    def list_tools():
        return tools.schemas()

    @app.post("/api/tools/{name}")
    def invoke(name: str, body: ToolRequest):
        return tools.call(name, body.arguments)

    @app.post("/api/chat")
    async def chat(body: ChatRequest):
        try:
            async with asyncio.timeout(5):
                await semaphore.acquire()
        except TimeoutError:
            raise HTTPException(429, "busy") from None
        try:
            return await agent.run(body.question, body.context.model_dump() if body.context else None, body.mode)
        finally:
            semaphore.release()

    @app.get("/api/traces/{ident}")
    def trace(ident: str):
        data = agent.trace(ident)
        if not data:
            raise HTTPException(404, "trace_not_found")
        return data

    return app
