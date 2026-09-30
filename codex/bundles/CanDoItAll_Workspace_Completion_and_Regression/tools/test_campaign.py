#!/usr/bin/env python3
"""Exercise evidence validation using temporary synthetic fixtures; never run product tests."""
from __future__ import annotations
import copy
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('campaign_validator', Path(__file__).with_name('validate_campaign.py'))
if spec is None or spec.loader is None:
    raise RuntimeError('Cannot load validator.')
v = importlib.util.module_from_spec(spec)
spec.loader.exec_module(v)


class CampaignTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='workspace-campaign-proof-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.proofs = []
        for kind in ['ui', 'owner', 'provider']:
            path = self.root / (kind + '.json')
            path.write_text('{"synthetic_validator_fixture":true}\n')
            self.proofs.append(dict(kind=kind, path=path.name, sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        self.plan = dict(schema_version=1, campaign_id='fixture',
                         live_budget=dict(maximum_requests_per_execution=10, maximum_requests_total=40),
                         cases=[dict(id='WS-01', required=True, required_mode='production-ui', needs_ui=True, needs_owner=True),
                                dict(id='LIVE-01', required=True, required_mode='live-agent', needs_ui=True, needs_owner=True)])
        a = dict(attempt_id='workspace-1', status='PASSED', mode='production-ui', execution='executed',
                 source_commit='1'*40, checkpoint_sha256='2'*64, command='synthetic validator fixture only',
                 checks=dict(ui_driven=True, owner_readback=True), evidence=copy.deepcopy(self.proofs))
        b = copy.deepcopy(a)
        b.update(attempt_id='live-1', mode='live-agent', execution='live', provider='synthetic provider',
                 model='synthetic model', model_requests=2, executions=[dict(id='run-1', model_requests=2)])
        self.results = dict(schema_version=1, campaign_id='fixture', workspace_ui_complete=True,
                            application_regression_ready=True, accepted_checkpoints=['2'*64],
                            cases=[dict(id='WS-01', attempts=[a]), dict(id='LIVE-01', attempts=[b])])

    def check(self, complete=True):
        return v.validate(self.plan, self.results, self.root, complete)

    def live(self):
        return self.results['cases'][1]['attempts'][-1]

    def test_complete_synthetic_fixture(self):
        self.assertTrue(self.check()['bookkeeping_complete'])

    def test_initial_template_is_structural_only(self):
        for row in self.results['cases']:
            row['attempts'] = []
        self.results.update(workspace_ui_complete=False, application_regression_ready=False)
        self.assertEqual(2, len(self.check(False)['incomplete']))
        with self.assertRaisesRegex(ValueError, 'Campaign incomplete'):
            self.check()

    def test_runner_pass_without_live_execution_is_rejected(self):
        self.live().update(execution='executed', model_requests=0, executions=[])
        with self.assertRaisesRegex(ValueError, 'actual model execution'):
            self.check()

    def test_rehearsal_mislabeled_pass_is_rejected(self):
        self.live().update(execution='rehearsal', model_requests=0, executions=[])
        with self.assertRaisesRegex(ValueError, 'actual model execution'):
            self.check()

    def test_live_zero_requests_is_rejected(self):
        self.live().update(model_requests=0, executions=[dict(id='run-1', model_requests=0)])
        with self.assertRaisesRegex(ValueError, 'actual model execution'):
            self.check()

    def test_owner_oracle_is_required(self):
        self.live()['checks']['owner_readback'] = False
        with self.assertRaisesRegex(ValueError, 'owner evidence'):
            self.check()

    def test_ui_artifact_is_required(self):
        self.live()['evidence'] = [p for p in self.live()['evidence'] if p['kind'] != 'ui']
        with self.assertRaisesRegex(ValueError, 'UI evidence'):
            self.check()

    def test_duplicate_case_is_rejected(self):
        self.results['cases'].append(copy.deepcopy(self.results['cases'][0]))
        with self.assertRaisesRegex(ValueError, 'Duplicate result case'):
            self.check()

    def test_missing_group_is_rejected(self):
        self.results['cases'].pop()
        with self.assertRaisesRegex(ValueError, 'inventory'):
            self.check()

    def test_unaccepted_checkpoint_is_rejected(self):
        self.live()['checkpoint_sha256'] = '3'*64
        with self.assertRaisesRegex(ValueError, 'checkpoint is not accepted'):
            self.check()

    def test_modified_artifact_is_rejected(self):
        (self.root/'owner.json').write_text('changed')
        with self.assertRaisesRegex(ValueError, 'hash mismatch'):
            self.check()

    def test_path_escape_is_rejected(self):
        self.live()['evidence'][0]['path'] = '../outside.json'
        with self.assertRaisesRegex(ValueError, 'Unsafe evidence path'):
            self.check()

    def test_latest_failure_cannot_hide_behind_old_pass(self):
        attempt = copy.deepcopy(self.live())
        attempt.update(attempt_id='live-2', status='FAILED', reason='synthetic late failure')
        self.results['cases'][1]['attempts'].append(attempt)
        with self.assertRaisesRegex(ValueError, 'readiness declared'):
            self.check()

    def test_failed_retry_usage_counts_against_budget(self):
        self.plan['live_budget']['maximum_requests_total'] = 3
        attempt = copy.deepcopy(self.live())
        attempt.update(attempt_id='live-2', status='FAILED', reason='synthetic failure')
        self.results['cases'][1]['attempts'].append(attempt)
        self.results['application_regression_ready'] = False
        with self.assertRaisesRegex(ValueError, 'Total live request budget'):
            self.check(False)

    def test_per_execution_budget_is_enforced(self):
        self.live().update(model_requests=11, executions=[dict(id='run-1', model_requests=11)])
        with self.assertRaisesRegex(ValueError, 'Per-execution'):
            self.check()

    def test_non_live_substitution_is_rejected(self):
        self.live().update(mode='production-ui', execution='executed', model_requests=0, executions=[])
        with self.assertRaisesRegex(ValueError, 'mode does not satisfy'):
            self.check()

    def test_complete_mode_needs_real_hash_verification_root(self):
        with self.assertRaisesRegex(ValueError, 'requires an evidence root'):
            v.validate(self.plan, self.results, require_complete=True)

    def test_counters_must_reconcile(self):
        self.live()['model_requests'] = 3
        with self.assertRaisesRegex(ValueError, 'counters do not reconcile'):
            self.check()

    def test_unknown_status_is_rejected(self):
        self.live()['status'] = 'PROBABLY_FINE'
        with self.assertRaisesRegex(ValueError, 'Unknown status'):
            self.check()


if __name__ == '__main__':
    unittest.main(verbosity=2)
