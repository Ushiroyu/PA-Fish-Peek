import argparse
import asyncio
import json


def main():
    parser = argparse.ArgumentParser(description="PA Fish Peek 工作台")
    sub = parser.add_subparsers(dest="command", required=True)
    serve = sub.add_parser("serve")
    serve.add_argument("--port", type=int, default=8768)
    sub.add_parser("mcp")
    ask = sub.add_parser("ask")
    ask.add_argument("question")
    ask.add_argument("--mode", choices=["auto", "local", "model"], default="auto")
    args = parser.parse_args()
    if args.command == "serve":
        import uvicorn
        from .api import create_app
        uvicorn.run(create_app(), host="127.0.0.1", port=args.port)
    elif args.command == "mcp":
        from .mcp_server import main as run_mcp
        run_mcp()
    else:
        from .settings import settings
        from .tools import ToolRegistry
        from .agent import Agent
        result = asyncio.run(Agent(settings(), ToolRegistry(settings())).run(args.question, mode=args.mode))
        print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
