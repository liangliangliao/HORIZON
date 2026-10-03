"""Combine the 53-chapter product audit with observed NUnit evidence.

A passing test of a related rule is partial evidence, never completion of a
chapter or proof of usability. Run against the results of the same commit.
"""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument("--results", type=Path, required=True)
parser.add_argument("--output", type=Path, default=Path("artifacts/product-acceptance.json"))
args = parser.parse_args()
matrix = Path("docs/SPEC_ACCEPTANCE_V042.md").read_text(encoding="utf-8")
rows = re.findall(r"^\| (\d+) \| (.*?) \| (.*?) \| (.*?) \|$", matrix, re.M)
assert [int(row[0]) for row in rows] == list(range(1, 54)), "All 53 chapters must be audited exactly once"

# These fixtures verify implemented subsets. Missing features remain missing
# even when a nearby rule has passing tests.
groups = {
    3: ["WideChoicesGroupedEchoesAndPreparation"], 4: ["EveryModeCompletes", "CampaignCompletionTests"],
    5: ["CausalFlowTests", "ObservationDesignTests"], 6: ["PlayGuideTests", "MasterSpecificationTests"],
    7: ["GameSessionTests", "WideChoicesGroupedEchoes"], 8: ["CausalFlowTests", "CausalPresentationTests"],
    9: ["CampaignCompletionTests", "BusinessRuleTests"], 10: ["MasterSpecificationTests", "ObservationDesignTests"],
    11: ["PreparationConsumesFocus", "LegacyLivesKeep", "MasterInvariantTests"],
    12: ["PreparationConsumesFocus", "UnmatchedReminder", "PaidTriggers"], 13: ["PreparationConsumesFocus", "PaidTriggers", "LegacyLivesKeep"],
    14: [], 15: [], 16: ["KnowledgeCannotSkip"],
    18: ["ImaginedPreparationBranches", "FirstLifeTeachesTime"], 19: ["ImaginationCannotReachVictory", "ImaginedPreparationBranches"],
    20: ["MasterSpecificationTests"], 21: ["ActualChoiceDivergence", "NoActualSetback", "RealSetbackAndNextAction", "PracticePreservesOriginalLife"],
    22: ["MasterInvariantTests", "RealSetbackAndNextAction"], 23: ["MasterInvariantTests", "MasterSpecificationTests"],
    24: ["MasterSpecificationTests"], 25: ["MasterInvariantTests", "MasterSpecificationTests"],
    26: [], 27: ["ReplayingCommandsReproducesResourcesAndDecisionCausality"], 28: ["ReservoirPayoutHasSixRealParentsAndDoesNotRepeatOnReload"],
    29: ["OneActionWithManyPreparationNodes", "CausalFlowTests"], 30: ["OnlineAITests", "PlayableFlowTests"],
    31: ["PlayableFlowTests", "WideChoicesGroupedEchoes"], 32: ["MasterSpecificationTests", "MasterInvariantTests"],
    33: ["MasterFeaturesCanBePlayed"], 34: ["MasterSpecificationTests", "MasterInvariantTests"],
    35: ["EveryModeCompletes", "GameSessionTests"], 36: ["CausalPresentationTests", "PlayableFlowTests"],
    37: ["MasterInvariantTests"], 38: ["MasterInvariantTests"], 39: ["ActualChoiceDivergence", "MasterSpecificationTests"],
    40: ["OnlineAITests", "OnlineProviders", "AzureResource"], 41: ["WideChoicesGroupedEchoes", "FeedbackInteractionTests"],
    42: ["MasterFeaturesCanBePlayed", "CausalPresentationTests"], 43: [], 44: [],
    45: ["FirstLifeTeachesTime", "WideChoicesGroupedEchoes", "PracticePreservesOriginalLife"],
    47: [], 48: ["PlayableFlowTests"], 49: ["BusinessRuleTests", "MasterInvariantTests"],
    51: ["MasterInvariantTests", "OnlineAITests"],
}
files = [args.results] if args.results.is_file() else sorted(args.results.rglob("*.xml"))
cases = {}
for file in files:
    try:
        for case in ET.parse(file).getroot().iter("test-case"):
            name = case.get("fullname", case.get("name", ""))
            if name:
                cases[name] = {"test": name, "result": case.get("result"), "source": str(file)}
    except ET.ParseError:
        continue
assert cases, "No NUnit execution evidence found"
chapters = []
for number, requirement, implemented, pending in rows:
    number = int(number)
    evidence = [case for name, case in cases.items() if any(key in name for key in groups.get(number, []))]
    chapters.append({"chapter": number, "requirement": requirement, "implemented_scope": implemented,
                     "remaining_scope": pending, "verification": "related_subset_executed" if evidence else "not_automated",
                     "evidence": evidence})
report = {
    "baseline": "0.4.2", "commit": subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip(),
    "working_tree_dirty": bool(subprocess.check_output(["git", "status", "--porcelain"], text=True).strip()),
    "all_specification_features_implemented": False, "all_specification_features_verified": False,
    "interpretation": "Execution evidence covers the listed implemented subset. It does not accept the remaining scope, visual quality, fun, or real-world outcomes.",
    "test_results": dict(Counter(case["result"] for case in cases.values())),
    "chapters_audited": len(chapters), "chapters": chapters,
    "not_verified_by_this_report": ["Android physical-device touch, performance, audio and haptics", "first-time-player comprehension and enjoyment",
        "live DeepSeek/Azure with user credentials", "online Parallel Lives/Future Messages", "independent ten-Boss gameplay", "MP4 export"],
}
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"chapters_audited": 53, "test_results": report["test_results"], "all_features_implemented": False,
                  "all_features_verified": False, "report": str(args.output)}, ensure_ascii=False))
