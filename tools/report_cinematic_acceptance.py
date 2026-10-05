"""Bind the two fixed v1.0 requirement checklists to this run's actual evidence.

Static implementation review, executed automation, visual review and mobile FPS
are deliberately separate. The old Master v0.4.2 score is never used here.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET


def report(checklist, results, root):
    groups = checklist['groups']
    expected = {f'R{i:02}' for i in range(1, 23)} | {f'A{i:02}' for i in range(1, 29)}
    ids = [g['id'] for g in groups]
    if len(ids) != 50 or set(ids) != expected:
        raise ValueError('The fixed 22 reward / 28 animation requirement groups must occur exactly once.')
    cases = {}
    for file in results:
        document = ET.parse(file).getroot()
        for case in document.iter('test-case'):
            name = case.get('fullname', case.get('name', ''))
            if name:
                cases.setdefault(name, []).append({'result': case.get('result', 'Unknown'), 'file': str(file)})
    reviewed = {'passed', 'implemented_pending_acceptance'}
    rows = []
    for group in groups:
        sources = group.get('source', [])
        source_exists = bool(sources) and all((root / path.split('#')[0]).is_file() for path in sources)
        checks = []
        for method in group.get('tests', []):
            matches = [record for name, records in cases.items()
                       if name == method or name.startswith(method + '(') for record in records]
            checks.append({'method': method, 'executed': bool(matches),
                           'passed': bool(matches) and all(x['result'] == 'Passed' for x in matches),
                           'evidence': matches})
        implemented = group['status'] in reviewed and source_exists
        verified = implemented and bool(checks) and all(x['passed'] for x in checks)
        paths = group.get('runtime_evidence', [])
        runtime = {p: [str(x.relative_to(root)) for x in root.glob(p) if x.is_file()] for p in paths}
        rows.append({**group, 'source_exists': source_exists, 'implementation_reviewed': implemented,
                     'automation_verified': verified, 'executed_checks': checks,
                     'runtime_files': runtime,
                     'acceptance_passed': verified and group['status'] == 'passed'
                         and all(runtime.values())})
    summaries = {}
    for spec, prefix, denominator, minimum in [('reward', 'R', 22, 19), ('animation', 'A', 28, 24)]:
        selected = [g for g in rows if g['id'].startswith(prefix)]
        summaries[spec] = {'total': denominator, 'minimum': minimum}
        for key in ['implementation_reviewed', 'automation_verified', 'acceptance_passed']:
            count = sum(g[key] for g in selected)
            summaries[spec][key] = count
            summaries[spec][key + '_percent'] = round(count / denominator * 100, 2)
        summaries[spec]['implementation_threshold_met'] = summaries[spec]['automation_verified'] >= minimum
    failing = {name: records for name, records in cases.items() if any(x['result'] != 'Passed' for x in records)}
    return {'specification_version': '1.0', 'summary': summaries, 'groups': rows,
            'observed_tests': len(cases), 'nonpassing_tests': failing,
            'threshold_met': bool(cases) and not failing and all(x['implementation_threshold_met'] for x in summaries.values()),
            'interpretation': 'Fixed natural requirement groups; reviewed implementation requires named passing tests. Visual quality and actual device FPS require separate evidence. APK success is an independent gate.'}


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--checklist', type=Path, default=Path('docs/REWARD_CINEMATIC_ACCEPTANCE_V1.json'))
    parser.add_argument('--results', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path('artifacts/cinematic-acceptance.json'))
    parser.add_argument('--enforce', action='store_true')
    args = parser.parse_args()
    paths = [args.results] if args.results.is_file() else sorted(args.results.rglob('*.xml'))
    checklist = json.loads(args.checklist.read_text())
    result = report(checklist, paths, Path.cwd())
    result['commit'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
    result['checklist_sha256'] = hashlib.sha256(args.checklist.read_bytes()).hexdigest()
    result['review_state'] = checklist.get('review_state', 'unknown')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'summary': result['summary'], 'tests': result['observed_tests'],
                      'threshold_met': result['threshold_met']}, ensure_ascii=False))
    if args.enforce and (not result['threshold_met'] or result['review_state'] != 'complete'):
        raise SystemExit('Both v1.0 specifications need at least 85% reviewed and test-backed implementation.')


if __name__ == '__main__':
    main()
