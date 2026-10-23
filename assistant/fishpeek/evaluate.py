import argparse
import json
import tempfile
import time
from pathlib import Path
from .settings import PACKAGE, Settings
from .tools import ToolRegistry


def evaluate(output=None):
    cases = json.loads((PACKAGE / "data/golden.json").read_text(encoding="utf-8"))
    results = []
    with tempfile.TemporaryDirectory() as folder:
        settings = Settings(_env_file=None, state_dir=Path(folder), events_file=Path(folder) / "absent.jsonl")
        tools = ToolRegistry(settings)
        for case in cases:
            start = time.perf_counter()
            result = tools.call(case["tool"], case["arguments"])
            actual = result
            for key in case["path"]:
                actual = actual.get(key) if isinstance(actual, dict) else None
            if case["operator"] == "equals":
                passed = actual == case["expected"]
            elif case["operator"] == "contains":
                passed = actual is not None and case["expected"] in actual
            else:
                passed = isinstance(actual, list) and any(row.get("title") == case["expected"] for row in actual)
            results.append({"id": case["id"], "passed": passed, "duration_ms": round((time.perf_counter() - start) * 1000, 2)})
    summary = {"suite": "domain-and-retrieval", "provider_used": False, "cases": len(results),
               "passed": sum(r["passed"] for r in results), "results": results}
    summary["pass_rate"] = summary["passed"] / summary["cases"]
    if output:
        path = Path(output)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: value for key, value in summary.items() if key != "results"}, ensure_ascii=False))
    return summary


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output")
    args = parser.parse_args()
    if evaluate(args.output)["pass_rate"] != 1:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
