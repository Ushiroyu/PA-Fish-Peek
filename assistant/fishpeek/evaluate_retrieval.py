"""按逐条相关性标签评测检索，保留分组指标、排名和需复核样例。"""
import argparse
from collections import Counter
from copy import deepcopy
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import statistics
import tempfile
import time
from .retrieval import Knowledge
from .settings import PACKAGE

KS = (1, 3, 4, 6)
CATEGORIES = {"direct", "paraphrase", "abbreviation", "multi_evidence", "missing_context", "no_answer"}


def ranking_metrics(ranking, relevant, k):
    """二元相关性；无答案样例不参与相关文档召回率的分母。"""
    if not relevant:
        return None
    if k < 1 or len(set(ranking)) != len(ranking):
        raise ValueError("Invalid cutoff or duplicate ranking identifiers")
    expected = set(relevant)
    top = ranking[:k]
    hits = [i for i, item in enumerate(top, 1) if item in expected]
    return {"precision": len(hits) / k, "recall": len(hits) / len(expected),
            "hit_rate": float(bool(hits)), "mrr": 1 / hits[0] if hits else 0.0}


def validate_dataset(dataset, sections, manifest):
    known = {s["section_id"] for s in sections}
    if len(known) != len(sections):
        raise ValueError("Duplicate section headings in the same file")
    if dataset.get("corpus_revision") != manifest.get("revision"):
        raise ValueError("Corpus revision does not match annotations")
    actual_files = {Path(s["source"].split(':L')[0]).name for s in sections}
    if actual_files != set(manifest.get("files", {})):
        raise ValueError("Corpus source manifest is incomplete")
    ids, queries = set(), set()
    for case in dataset["cases"]:
        if not case.get("id") or case["id"] in ids or case["query"].strip().casefold() in queries:
            raise ValueError("Empty identifier, duplicate identifier or duplicate query")
        ids.add(case["id"])
        queries.add(case["query"].strip().casefold())
        if case.get("category") not in CATEGORIES or not case.get("reason", "").strip() or not case["query"].strip():
            raise ValueError("Missing category, query or annotation rationale")
        labels = case["relevant"]
        if len(set(labels)) != len(labels) or not set(labels) <= known:
            raise ValueError("Duplicate or unknown relevance label")
        if (case["category"] == "no_answer") != (not labels):
            raise ValueError("Only no_answer cases may have no relevant sections")
    if {c["category"] for c in dataset["cases"]} != CATEGORIES:
        raise ValueError("Missing scenario category")


def aggregate(results):
    positive = [row for row in results if row["relevant"]]
    negative = [row for row in results if not row["relevant"]]
    return {"count": len(results), "answerable": len(positive), "no_answer": len(negative),
            "at_k": {str(k): {name: round(statistics.mean(r["metrics"][str(k)][name] for r in positive), 6)
                              for name in ("precision", "recall", "hit_rate", "mrr")} if positive else None for k in KS},
            "no_answer_empty_result_rate": round(sum(not r["ranking"] for r in negative) / len(negative), 6) if negative else None}


def compare_reference(report, reference, tolerance=0.000001):
    if (reference["dataset_version"], reference["corpus_revision"], reference["strategy"]) != (
            report["dataset_version"], report["corpus_revision"], report["strategy"]):
        raise ValueError("Reference versions or strategy do not match")
    if reference["scenario_counts"] != report["scenario_counts"]:
        raise ValueError("Reference case coverage changed; review annotations and baseline")
    if reference["case_inventory"] != case_inventory(report) or reference["corpus"]["sections"] != report["corpus"]["sections"]:
        raise ValueError("Annotations or corpus changed; review versions and refresh the reference explicitly")
    regressions = []
    for k in map(str, KS):
        for name in ("precision", "recall", "hit_rate", "mrr"):
            old, new = reference["summary"]["at_k"][k][name], report["summary"]["at_k"][k][name]
            if new + tolerance < old:
                regressions.append({"metric": f"{name}@{k}", "reference": old, "actual": new})
    # 空检索率只作行为观察；检索到边界说明有时也有助于回答，不将其视为拒答正确率。
    return regressions


def case_inventory(report):
    return [{key: row[key] for key in ("id", "category", "query", "relevant", "reason")} for row in report["results"]]


def freeze_reference(report):
    return deepcopy({**{key: report[key] for key in ("dataset_version", "corpus_revision", "strategy", "scenario_counts", "summary", "corpus")},
                     "case_inventory": case_inventory(report)})


def evaluate(output=None, cases_path=None, reference=None, strategy="hex-normalized"):
    if strategy not in ("baseline", "hex-normalized"):
        raise ValueError("Unknown retrieval strategy")
    dataset = json.loads((cases_path or PACKAGE / "data/retrieval_cases.json").read_text(encoding="utf-8"))
    manifest = json.loads((PACKAGE / "knowledge/sources.json").read_text(encoding="utf-8"))
    with tempfile.TemporaryDirectory(prefix="fishpeek-retrieval-") as directory:
        knowledge = Knowledge(Path(directory) / "knowledge.sqlite3")
        sections = knowledge.sections()
        validate_dataset(dataset, sections, manifest)
        logical_ids = {s["id"]: s["section_id"] for s in sections}
        results = []
        for case in dataset["cases"]:
            started = time.perf_counter()
            matches = knowledge.search(case["query"], max(KS), normalize_identifiers=strategy != "baseline")
            duration = (time.perf_counter() - started) * 1000
            ranking = [logical_ids[m["id"]] for m in matches]
            metrics = {str(k): ranking_metrics(ranking, case["relevant"], k) for k in KS}
            relevant = set(case["relevant"])
            missing = sorted(relevant - set(ranking[:4]))
            flags = []
            if relevant:
                if not relevant & set(ranking[:4]):
                    flags.append("no_relevant_section_at_4")
                elif missing:
                    flags.append("incomplete_evidence_at_4")
                if ranking and ranking[0] not in relevant:
                    flags.append("first_result_not_labelled_relevant")
            elif ranking:
                flags.append("no_answer_context_needs_review")
            results.append({**case, "ranking": ranking, "matches": matches, "metrics": metrics,
                            "missing_at_4": missing, "review_flags": flags, "duration_ms": round(duration, 4)})
    durations = sorted(row["duration_ms"] for row in results)
    report = {"suite": "retrieval-ranking", "strategy": strategy, "provider_used": False,
              "recorded_at": datetime.now(timezone.utc).isoformat(), "dataset_version": dataset["version"],
              "corpus_revision": manifest["revision"], "provenance": dataset["provenance"],
              "corpus": {"files": manifest["files"], "sections": sections},
              "scenario_counts": dict(sorted(Counter(c["category"] for c in results).items())),
              "summary": aggregate(results),
              "groups": {name: aggregate([r for r in results if r["category"] == name]) for name in sorted(CATEGORIES)},
              "latency_ms": {"p50": statistics.median(durations), "p95": durations[math.ceil(len(durations) * .95) - 1],
                             "scope": "本机单次离线检索；不包含索引构建和模型调用"},
              "review_cases": [r["id"] for r in results if r["review_flags"]], "results": results}
    report["reference_regressions"] = compare_reference(report, reference) if reference else []
    if output:
        output = Path(output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        output.with_suffix(".md").write_text(render_markdown(report), encoding="utf-8")
    return report


def render_markdown(report):
    lines = ["# 中文检索评测记录", "", f"数据集：`{report['dataset_version']}`；资料版本：`{report['corpus_revision']}`。",
             "", report["provenance"], "", f"策略：`{report['strategy']}`，字符与双字词 BM25；未调用模型。",
             "", "| 分组 | 有依据问题 | 无答案问题 | Recall@4 | MRR@4 |", "| --- | ---: | ---: | ---: | ---: |"]
    for name, group in [("all", report["summary"]), *report["groups"].items()]:
        metric = group["at_k"]["4"]
        recall, mrr = (f"{metric['recall']:.4f}", f"{metric['mrr']:.4f}") if metric else ("—", "—")
        lines.append(f"| {name} | {group['answerable']} | {group['no_answer']} | {recall} | {mrr} |")
    rate = report["summary"]["no_answer_empty_result_rate"]
    lines += ["", f"无答案样例的空检索率：{rate:.1%}。该值仅观察是否返回文段，不代表模型拒答准确率。",
              "", "## 需复核样例", "", "返回边界说明可能有助于澄清问题；无答案样例返回文段时单独列出，不直接判为模型错误。", ""]
    for row in report["results"]:
        if not row["review_flags"]:
            continue
        lines += [f"### {row['id']} · {row['category']}", "", row["query"], "",
                  f"标注依据：{row['reason']}", f"预期段落：{'；'.join(row['relevant']) or '无直接答案'}",
                  f"实际前四：{'；'.join(row['ranking'][:4]) or '空'}", f"复核标记：{'；'.join(row['review_flags'])}", ""]
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path("artifacts/retrieval-evaluation.json"))
    parser.add_argument("--check-reference", action="store_true")
    parser.add_argument("--strategy", choices=("baseline", "hex-normalized"), default="hex-normalized")
    args = parser.parse_args()
    reference = json.loads((PACKAGE / "data/retrieval_reference.json").read_text(encoding="utf-8")) if args.check_reference else None
    report = evaluate(args.output, reference=reference, strategy=args.strategy)
    print(json.dumps({"cases": report["summary"]["count"], "sections": len(report["corpus"]["sections"]),
                      "at_4": report["summary"]["at_k"]["4"], "no_answer_empty_result_rate": report["summary"]["no_answer_empty_result_rate"],
                      "review_cases": len(report["review_cases"]), "reference_regressions": report["reference_regressions"]}, ensure_ascii=False))
    if report["reference_regressions"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
