from fishpeek.evaluate import evaluate


def test_golden_suite_and_database_cleanup(tmp_path):
    report = evaluate(tmp_path / "report.json")
    assert report["cases"] == 26 and report["passed"] == 26
    assert report["provider_used"] is False
