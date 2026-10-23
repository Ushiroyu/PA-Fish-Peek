import pytest
from pydantic import ValidationError
from fishpeek.rules import QueryContext, catalog, explain_candidate, query_candidates


def ctx(**kwargs):
    return QueryContext(rod_id="AE_006182_Rod", level=6, location=4, season=4, weather=64, **kwargs)


def test_catalog_integrity():
    data = catalog()
    assert len(data["fish"]) == 76 and len(data["rods"]) == 17
    fish = {f["name"] for f in data["fish"]}
    assert len(fish) == 76
    assert all(set(rod["fish"]) <= fish for rod in data["rods"])


def test_meteor_fish_exact_rod_and_location():
    result = query_candidates(ctx())
    assert "流星鱼" in [c["name"] for c in result["candidates"]]
    explanation = explain_candidate("流星鱼", ctx())
    assert explanation["eligible"] and explanation["environment_matches"]["weather"]


@pytest.mark.parametrize("change,reason", [({"location": 2}, "location_mismatch"), ({"level": 5}, "level_mismatch"),
    ({"rod_id": "AE_005259_Bamboo_Rod"}, "not_in_rod_whitelist")])
def test_explicit_rejections(change, reason):
    data = ctx().model_dump() | change
    assert reason in explain_candidate("流星鱼", QueryContext(**data))["reasons"]


def test_weather_is_score_signal_not_hard_filter():
    matching = explain_candidate("流星鱼", ctx())
    sunny = explain_candidate("流星鱼", QueryContext(**(ctx().model_dump() | {"weather": 1})))
    assert sunny["eligible"] and matching["score"] > sunny["score"]


def test_pond_requires_matching_season():
    base = dict(rod_id="AE_006123_Rod", mode="pond", level=4, location=128, weather=64)
    assert explain_candidate("奇虾", QueryContext(**base, season=4))["eligible"]
    assert "pond_season_required" in explain_candidate("奇虾", QueryContext(**base))["reasons"]
    assert "pond_season_mismatch" in explain_candidate("奇虾", QueryContext(**base, season=1))["reasons"]


@pytest.mark.parametrize("arguments", [{"level": 0}, {"level": True}, {"weather": 3}, {"time": -1},
    {"mode": "pond", "location": 4}, {"weather": 1.5}, {"command": "anything"},
    {"accessory": True}, {"location": True}, {"season": True}])
def test_invalid_context(arguments):
    with pytest.raises(ValidationError):
        QueryContext(**arguments)


def test_unknown_rod_is_error():
    with pytest.raises(ValueError, match="Unknown rod"):
        query_candidates(QueryContext(rod_id="does-not-exist"))


def test_no_hidden_location_relaxation():
    result = query_candidates(QueryContext(rod_id="AE_006123_Rod", mode="route", location=4, level=4))
    assert result["count"] == 0
