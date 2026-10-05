"""Permit a diagnostic rerun only when its game source was already verified."""
import json
import os
from pathlib import Path
import re
import subprocess
import sys


def api(path):
    return json.loads(subprocess.check_output(["gh", "api", path], text=True))


def main():
    run_id = sys.argv[1]
    assert re.fullmatch(r"[1-9][0-9]*", run_id), "Expected an Actions run ID"
    repo = os.environ["GITHUB_REPOSITORY"]
    run = api(f"repos/{repo}/actions/runs/{run_id}")
    assert run["name"] == "Android APK", "Expected the repository's Android workflow"
    previous = run["head_sha"]
    assert re.fullmatch(r"[0-9a-f]{40}", previous), "Invalid source SHA"
    current = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    changed = subprocess.check_output(["git", "diff", "--name-only", previous, current], text=True).splitlines()
    allowed = {".github/workflows/android-apk.yml", "tools/run_android_smoke.py",
               "tools/verify_native_recheck_source.py"}
    assert set(changed) <= allowed, "The compiled game or its test source changed: " + repr(changed)
    jobs = api(f"repos/{repo}/actions/runs/{run_id}/jobs?per_page=100")["jobs"]
    for name in ("test", "rules", "ai-gateway", "parallel-lives"):
        assert any(j["name"] == name and j["conclusion"] == "success" for j in jobs), name + " did not pass"
    build = next(j for j in jobs if j["name"] == "build")
    for name in ("Build Android APK", "Verify APK output"):
        assert any(s["name"] == name and s["conclusion"] == "success" for s in build["steps"]), name + " did not pass"
    artifacts = api(f"repos/{repo}/actions/runs/{run_id}/artifacts?per_page=100")["artifacts"]
    assert any(a["name"] == "HORIZON-Internal-Native-Check-APK" and not a["expired"] for a in artifacts), "No retained native check APK"
    report = {"source_run": int(run_id), "compiled_commit": previous, "checked_commit": current,
              "game_and_tests_unchanged": True, "host_changes": changed,
              "previous_rules_services_unity_and_apk_checks": "passed", "purpose": "diagnosis only"}
    output = Path("artifacts/android-smoke/device/source-verification.json")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
