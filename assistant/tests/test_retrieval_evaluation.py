from copy import deepcopy
import json
import pytest
from fishpeek.evaluate_retrieval import (
    aggregate, compare_reference, evaluate, freeze_reference, ranking_metrics, validate_dataset,
)
from fishpeek.retrieval import Knowledge, tokens
from fishpeek.settings import PACKAGE


def test_metrics_use_relevant_denominator_and_first_relevant_rank():
    result = ranking_metrics(["noise", "b", "a"], ["a", "b"], 3)
    assert result == {"precision": 2 / 3, "recall": 1.0, "hit_rate": 1.0, "mrr": .5}
    assert ranking_metrics(["noise", "b"], ["a", "b"], 1) == {
        "precision": 0, "recall": 0, "hit_rate": 0, "mrr": 0}
    assert ranking_metrics([], ["a"], 4)["recall"] == 0
    assert ranking_metrics(["a"], [], 4) is None
    with pytest.raises(ValueError, match="duplicate"):
        ranking_metrics(["a", "a"], ["a"], 4)


def test_hex_normalization_preserves_existing_identifier_and_supports_missing_prefix(tmp_path):
    assert "hex:c0000005" in tokens("0xC0000005")
    assert "hex:c0000005" in tokens("c0000005")
    assert "0xc0000005" in tokens("0xc0000005")
    assert "hex:c0000005" not in tokens("0xc0000005", False)
    assert "hex:c0000005" not in tokens("prefixc0000005suffix")
    knowledge = Knowledge(tmp_path / "index.sqlite3")
    assert "稳定性诊断" in [m["title"] for m in knowledge.search("c0000005在哪个模块出过？")]


@pytest.fixture
def labelled_corpus(tmp_path):
    dataset = json.loads((PACKAGE / "data/retrieval_cases.json").read_text(encoding="utf-8"))
    manifest = json.loads((PACKAGE / "knowledge/sources.json").read_text(encoding="utf-8"))
    return dataset, Knowledge(tmp_path / "index.sqlite3").sections(), manifest


@pytest.mark.parametrize("mutation", ["unknown_label", "duplicate_id", "duplicate_query", "duplicate_label", "unlabelled_positive", "missing_rationale", "wrong_revision", "missing_source", "duplicate_section"])
def test_annotation_integrity_rejects_invalid_dataset(labelled_corpus, mutation):
    dataset, sections, manifest = deepcopy(labelled_corpus)
    case = dataset["cases"][0]
    if mutation == "unknown_label":
        case["relevant"] = ["missing#section"]
    elif mutation == "duplicate_id":
        dataset["cases"][1]["id"] = case["id"]
    elif mutation == "duplicate_query":
        dataset["cases"][1]["query"] = case["query"]
    elif mutation == "duplicate_label":
        case["relevant"] *= 2
    elif mutation == "unlabelled_positive":
        case["relevant"] = []
    elif mutation == "missing_rationale":
        case["reason"] = ""
    elif mutation == "wrong_revision":
        dataset["corpus_revision"] = "different"
    elif mutation == "missing_source":
        manifest["files"].pop("fishing.md")
    elif mutation == "duplicate_section":
        sections.append(sections[0])
    with pytest.raises(ValueError):
        validate_dataset(dataset, sections, manifest)


def test_full_suite_reports_failures_without_counting_no_answer_as_recall(tmp_path):
    report = evaluate(tmp_path / "evaluation.json")
    assert report["provider_used"] is False
    assert report["summary"]["count"] == 60
    assert report["summary"]["answerable"] == 50 and report["summary"]["no_answer"] == 10
    assert len(report["corpus"]["sections"]) == 21
    assert set(report["scenario_counts"].values()) == {10}
    assert report["groups"]["no_answer"]["at_k"]["4"] is None
    assert report["summary"]["no_answer_empty_result_rate"] == 0
    assert "P05" in report["review_cases"]
    text = (tmp_path / "evaluation.md").read_text(encoding="utf-8")
    assert "P05" in text and "不代表模型拒答准确率" in text
    reference = json.loads((PACKAGE / "data/retrieval_reference.json").read_text(encoding="utf-8"))
    assert not compare_reference(report, reference)


def test_regression_gate_detects_score_drop_or_annotation_change(tmp_path):
    report = evaluate()
    reference = freeze_reference(report)
    report["summary"]["at_k"]["4"]["recall"] -= .1
    assert any(row["metric"] == "recall@4" for row in compare_reference(report, reference))
    report["results"][0]["query"] += "改动"
    with pytest.raises(ValueError, match="Annotations or corpus changed"):
        compare_reference(report, reference)


def test_no_answer_observation_uses_its_own_denominator():
    def row(relevant, ranking):
        return {"relevant": relevant, "ranking": ranking,
                "metrics": {str(k): ranking_metrics(ranking, relevant, k) for k in (1, 3, 4, 6)}}
    summary = aggregate([row(["a"], ["a"]), row([], []), row([], ["noise"])])
    assert summary["at_k"]["4"]["recall"] == 1
    assert summary["no_answer_empty_result_rate"] == .5
