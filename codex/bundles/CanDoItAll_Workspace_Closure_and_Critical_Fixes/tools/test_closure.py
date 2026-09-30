#!/usr/bin/env python3
"""Test closure validation with synthetic files, never product behavior or live requests."""
from __future__ import annotations
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('closure_validator', ROOT / 'tools/validate_closure.py')
if spec is None or spec.loader is None:
    raise RuntimeError('Cannot load validator.')
v = importlib.util.module_from_spec(spec)
spec.loader.exec_module(v)


class ClosureTests(unittest.TestCase):
    def setUp(self):
        self.plan = json.loads((ROOT / 'closure-plan.json').read_text())
        self.blank = json.loads((ROOT / 'templates/closure-results.json').read_text())
        self.temp = tempfile.TemporaryDirectory(prefix='closure-validator-only-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.checkpoint = dict(app_commit='a'*40, components_commit='b'*40,
                               filetools_commit='c'*40, source_manifest_sha256='d'*64)
        self.proofs = []
        for kind in ('ui', 'owner', 'static', 'authorization', 'budget-journal', 'dependency'):
            path = self.root / (kind + '.md')
            path.write_text('Synthetic validator test only: ' + kind)
            self.proofs.append(dict(kind=kind, path=path.name,
                                   sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        self.good = copy.deepcopy(self.blank)
        self.good.update(workspace_rendering_complete=True, product_fixes_closed=True,
                         ready_for_next_module=True, accepted_checkpoint=self.checkpoint,
                         open_critical_findings=[])
        self.good['dependency_delivery'] = dict(status='VERIFIED', consumer_components_commit='b'*40,
                                               intended_consumer='Synthetic fixture only', evidence=copy.deepcopy(self.proofs))
        self.good['live_authorization'] = dict(authorized=True, campaign_id='synthetic-test',
            operator_reference='Synthetic validator test, not real authorization', max_requests_total=40,
            max_requests_per_execution=10, evidence=copy.deepcopy(self.proofs))
        for index, group in enumerate(self.good['groups']):
            case = self.plan['cases'][index]
            aid = 'attempt-' + group['id']
            attempt = dict(attempt_id=aid, status='PASSED', mode=case['required_mode'],
                execution='live' if case['required_mode'] == 'live-agent' else 'executed',
                checkpoint=copy.deepcopy(self.checkpoint), command='synthetic-validator-test',
                started_at_utc='2026-09-30T12:00:00Z', ended_at_utc='2026-09-30T12:00:01Z',
                evidence=copy.deepcopy(self.proofs))
            if case['required_mode'] == 'live-agent':
                eid = 'execution-' + group['id']
                attempt['live_execution_ids'] = [eid]
                self.good['live_executions'].append(dict(id=eid, reserved_requests=1,
                    provider_journal_requests=1, checkpoint=copy.deepcopy(self.checkpoint),
                    evidence=copy.deepcopy(self.proofs)))
            group.update(selected_attempt_id=aid, attempts=[attempt])

    def test_blank_is_valid_but_not_ready(self):
        self.assertFalse(v.validate(self.plan, self.blank)['ready_for_next_module'])

    def test_blank_cannot_be_declared_complete(self):
        with self.assertRaisesRegex(ValueError, 'not ready'):
            v.validate(self.plan, self.blank, self.root, True)

    def test_valid_synthetic_complete_record(self):
        value = v.validate(self.plan, self.good, self.root, True)
        self.assertEqual(value['groups'], 35)
        self.assertEqual(value['reserved_requests'], 3)

    def test_complete_needs_files(self):
        with self.assertRaisesRegex(ValueError, 'evidence root'):
            v.validate(self.plan, self.good)

    def test_missing_group(self):
        self.good['groups'].pop()
        with self.assertRaisesRegex(ValueError, 'inventory'):
            v.validate(self.plan, self.good, self.root)

    def test_rehearsal_cannot_pass(self):
        self.good['groups'][0]['attempts'][0]['execution'] = 'rehearsal'
        with self.assertRaisesRegex(ValueError, 'rehearsal'):
            v.validate(self.plan, self.good, self.root)

    def test_missing_owner_proof(self):
        attempt = self.good['groups'][0]['attempts'][0]
        attempt['evidence'] = [x for x in attempt['evidence'] if x['kind'] != 'owner']
        with self.assertRaisesRegex(ValueError, 'owner evidence'):
            v.validate(self.plan, self.good, self.root)

    def test_wrong_proof_mode(self):
        self.good['groups'][0]['attempts'][0]['mode'] = 'static'
        with self.assertRaisesRegex(ValueError, 'Wrong proof mode'):
            v.validate(self.plan, self.good, self.root)

    def test_old_source_does_not_close_current(self):
        self.good['groups'][0]['attempts'][0]['checkpoint']['app_commit'] = 'e'*40
        with self.assertRaisesRegex(ValueError, 'Stale final source'):
            v.validate(self.plan, self.good, self.root)

    def test_critical_finding_blocks_fixes_claim(self):
        self.good['open_critical_findings'] = ['WCL-R1']
        with self.assertRaisesRegex(ValueError, 'fixes remain open'):
            v.validate(self.plan, self.good, self.root)

    def test_blocked_group_stays_blocked(self):
        self.good['groups'][0]['attempts'][0].update(status='BLOCKED', reason='Fixture unavailable')
        with self.assertRaisesRegex(ValueError, 'Required group'):
            v.validate(self.plan, self.good, self.root)

    def test_no_new_live_authorization(self):
        self.good['live_authorization'] = copy.deepcopy(self.blank['live_authorization'])
        with self.assertRaisesRegex(ValueError, 'without new authorization'):
            v.validate(self.plan, self.good, self.root)

    def test_per_execution_budget(self):
        self.good['live_executions'][0]['reserved_requests'] = 11
        with self.assertRaisesRegex(ValueError, 'Per-execution budget'):
            v.validate(self.plan, self.good, self.root)

    def test_campaign_budget(self):
        for i in range(5):
            self.good['live_executions'].append(dict(id='extra-'+str(i),reserved_requests=10,
                provider_journal_requests=0, checkpoint=self.checkpoint,evidence=self.proofs))
        with self.assertRaisesRegex(ValueError, 'Campaign budget'):
            v.validate(self.plan, self.good, self.root)

    def test_live_needs_actual_journal(self):
        self.good['live_executions'][0]['provider_journal_requests'] = 0
        with self.assertRaisesRegex(ValueError, 'provider journal proof'):
            v.validate(self.plan, self.good, self.root)

    def test_failed_live_still_needs_budget_reference(self):
        attempt = next(x['attempts'][0] for x in self.good['groups'] if x['id']=='LIVE-01')
        attempt.update(status='FAILED', reason='Provider failed', live_execution_ids=[])
        with self.assertRaisesRegex(ValueError, 'live execution references'):
            v.validate(self.plan, self.good, self.root)

    def test_older_pass_cannot_hide_later_failure(self):
        group = self.good['groups'][0]
        later = copy.deepcopy(group['attempts'][0])
        later.update(attempt_id='later', status='FAILED', reason='Current failure')
        group['attempts'].append(later)
        with self.assertRaisesRegex(ValueError, 'old pass'):
            v.validate(self.plan, self.good, self.root)

    def test_local_only_dependency_is_not_verified_delivery(self):
        self.good['dependency_delivery']['status'] = 'LOCAL_SOURCE_PAIR_VERIFIED'
        with self.assertRaisesRegex(ValueError, 'delivery not verified'):
            v.validate(self.plan, self.good, self.root)

    def test_dependency_source_mismatch(self):
        self.good['dependency_delivery']['consumer_components_commit'] = 'e'*40
        with self.assertRaisesRegex(ValueError, 'differs from tested'):
            v.validate(self.plan, self.good, self.root)

    def test_hash_mismatch(self):
        (self.root/'ui.md').write_text('Changed file')
        with self.assertRaisesRegex(ValueError, 'hash mismatch'):
            v.validate(self.plan, self.good, self.root)

    def test_traversal_rejected(self):
        self.good['live_authorization']['evidence'][0]['path'] = '../outside.md'
        with self.assertRaisesRegex(ValueError, 'Unsafe evidence'):
            v.validate(self.plan, self.good, self.root)

    def test_failed_test_counts_cannot_pass(self):
        self.good['groups'][0]['attempts'][0]['test_counts'] = dict(executed=1, passed=0, failed=1, skipped=0)
        with self.assertRaisesRegex(ValueError, 'Non-green'):
            v.validate(self.plan, self.good, self.root)

    def test_boolean_is_not_request_count(self):
        self.good['live_executions'][0]['reserved_requests'] = True
        with self.assertRaisesRegex(ValueError, 'Invalid integer'):
            v.validate(self.plan, self.good, self.root)


if __name__ == '__main__':
    unittest.main(verbosity=2)
