"""Aggregate this commit's 53-chapter checklist and executed evidence.

A claim earns its capped credit only if all named evidence was executed and
passed. The gate includes human/live/device gaps as zero; it does not turn
passing tests into proof that the game is enjoyable or all features complete.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
from acceptance_catalog import CHAPTERS

parser = argparse.ArgumentParser()
parser.add_argument('--results', type=Path, required=True)
parser.add_argument('--output', type=Path, default=Path('artifacts/product-acceptance.json'))
parser.add_argument('--enforce', action='store_true', help='Require every observed test to pass and comprehensive engineering score >85')
args = parser.parse_args()
files = [args.results] if args.results.is_file() else sorted(args.results.rglob('*.xml'))
files += sorted(Path('artifacts/services').glob('*.xml'))
cases = {}
for file in files:
    try:
        root=ET.parse(file).getroot()
        for tag in ('test-case','testcase'):
            for case in root.iter(tag):
                name=case.get('fullname',case.get('name',''))
                result=case.get('result')
                if result is None: result='Failed' if case.find('failure') is not None or case.find('error') is not None else 'Skipped' if case.find('skipped') is not None else 'Passed'
                if name: cases[name]={'test':name,'result':result,'source':str(file)}
    except ET.ParseError: continue
assert cases, 'No executed acceptance evidence found'
chapters=[]
for chapter in CHAPTERS:
    source_ok=all(Path(p).is_file() for p in chapter['source'])
    criteria=[]
    for criterion in chapter['criteria']:
        checks=[ [case for name,case in cases.items() if test in name] for test in criterion['tests'] ]
        verified=bool(checks) and all(matches and all(case['result']=='Passed' for case in matches) for matches in checks)
        credit=criterion['completion_cap'] if source_ok and verified else 0
        criteria.append({**criterion, 'earned_credit':credit,
                         'verification':'executed' if verified else 'pending',
                         'evidence':[case for matches in checks for case in matches]})
    score=sum(c['earned_credit'] for c in criteria)/len(criteria)
    chapters.append({**chapter,'criteria':criteria,'source_files_exist':source_ok,'score':score})
percentage=sum(c['score'] for c in chapters)/53*100
pending=[{'id':c['id'],'requirement':c['requirement'],'credit':c['earned_credit'],'known_limit':c['known_limit']}
         for chapter in chapters for c in chapter['criteria'] if c['earned_credit']<1]
counts=dict(Counter(c['result'] for c in cases.values()))
report={'baseline':'0.4.2','commit':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),
        'source_tree':subprocess.check_output(['git','rev-parse','HEAD^{tree}'],text=True).strip(),
        'checklist_sha256':hashlib.sha256(Path('tools/acceptance_catalog.py').read_bytes()).hexdigest(),
        'working_tree_dirty':bool(subprocess.check_output(['git','status','--porcelain'],text=True).strip()),
        'assessment':'53 equally weighted chapters; named checks equally weighted inside each chapter; unverified checks score zero',
        'chapters_audited':53,'checks_audited':sum(len(c['criteria']) for c in chapters),
        'comprehensive_engineering_percent':round(percentage,4), 'threshold':'strictly greater than 85%',
        'threshold_met':percentage>85, 'all_specification_features_implemented':not pending,
        'all_specification_features_verified':not pending and not counts.get('Failed',0),
        'interpretation':'Includes the listed device/live/player gaps as zero. This measures implemented and demonstrated specification behavior, not fun, retention, market readiness or real-world transfer.',
        'test_results':counts, 'chapters':chapters, 'remaining_checks':pending}
args.output.parent.mkdir(parents=True,exist_ok=True)
args.output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:report[k] for k in ['chapters_audited','checks_audited','comprehensive_engineering_percent','threshold_met','test_results']},ensure_ascii=False))
if args.enforce and (percentage<=85 or any(v for k,v in counts.items() if k!='Passed')):
    for c in pending:
        if not c['known_limit']: print('Missing execution evidence:',c['id'],c['requirement'])
    raise SystemExit('Specification gate has not passed; continue implementation or verification.')
