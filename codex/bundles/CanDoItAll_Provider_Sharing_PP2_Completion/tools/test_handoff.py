"""Tests of the handoff validator only; these are not application tests."""
from copy import deepcopy
import json
from pathlib import Path
import tempfile
import unittest
import validate_handoff as validator

ROOT = Path(__file__).resolve().parents[1]
META = json.loads((ROOT/'bundle.json').read_text())
TEMPLATE = json.loads((ROOT/'templates/evidence.json').read_text())


def complete_fixture():
    evidence = deepcopy(TEMPLATE)
    evidence['gates'] = {key: True for key in evidence['gates']}
    for group in evidence['groups']:
        group.update(status='PASS', attempts=['synthetic-validator-test'], evidence=['test-only-receipt'])
    evidence['final_source'].update(application_commit='a'*40, components_commit='b'*40,
        filetools_commit='c'*40, worktree_digest='d'*64, application_image_digest='sha256:'+'e'*64,
        source_manifest='synthetic-manifest')
    evidence['signed_commits']=[{'sha':'a'*40, 'openpgp':True, 'verify_exit_code':0}]
    return evidence


class HandoffTests(unittest.TestCase):
    def test_actual_package(self):
        self.assertEqual([], validator.package_errors(ROOT, check_manifest=False)[0])

    def test_blank_template_is_valid_unfinished_evidence(self):
        self.assertEqual([], validator.evidence_errors(META, TEMPLATE))

    def test_blank_template_is_not_complete(self):
        self.assertTrue(validator.evidence_errors(META, TEMPLATE, True))

    def test_synthetic_consistent_evidence(self):
        self.assertEqual([], validator.evidence_errors(META, complete_fixture(), True))

    def test_s0_alone_is_not_completion(self):
        data=complete_fixture()
        data['groups'][META['product_outcome_groups'].index('PP2-SHARING')]['status']='NOT_RUN'
        self.assertTrue(validator.evidence_errors(META, data, True))

    def test_missing_group(self):
        data=complete_fixture(); data['groups'].pop()
        self.assertTrue(validator.evidence_errors(META,data,True))

    def test_duplicate_group(self):
        data=complete_fixture(); data['groups'][-1]=deepcopy(data['groups'][0])
        self.assertTrue(validator.evidence_errors(META,data,True))

    def test_pass_without_receipt(self):
        data=complete_fixture(); data['groups'][0]['evidence']=[]
        self.assertTrue(validator.evidence_errors(META,data,True))

    def test_pass_without_attempt(self):
        data=complete_fixture(); data['groups'][0]['attempts']=[]
        self.assertTrue(validator.evidence_errors(META,data,True))

    def test_invalid_status(self):
        data=complete_fixture(); data['groups'][0]['status']='GREEN'
        self.assertTrue(validator.evidence_errors(META,data,True))

    def test_old_image_is_not_current_proof(self):
        data=complete_fixture(); data['final_source']['application_image_digest']=None
        self.assertTrue(validator.evidence_errors(META,data,True))

    def test_release_claim_refused(self):
        data=complete_fixture(); data['application_release_ready']=True
        self.assertTrue(validator.evidence_errors(META,data,True))

    def test_missing_signature(self):
        data=complete_fixture(); data['signed_commits']=[]
        self.assertTrue(validator.evidence_errors(META,data,True))

    def test_failed_signature(self):
        data=complete_fixture(); data['signed_commits'][0]['verify_exit_code']=1
        self.assertTrue(validator.evidence_errors(META,data,True))

    def test_wrong_signing_format(self):
        data=complete_fixture(); data['signed_commits'][0]['openpgp']=False
        self.assertTrue(validator.evidence_errors(META,data,True))

    def test_string_gate_is_rejected(self):
        data=complete_fixture(); data['gates']['pp2_complete']='true'
        self.assertTrue(validator.evidence_errors(META,data,True))

    def test_live_blocker_is_rejected(self):
        data=complete_fixture(); data['open_findings']=[{'blocks_pp2':True,'status':'OPEN'}]
        self.assertTrue(validator.evidence_errors(META,data,True))

    def test_resolved_finding_allowed(self):
        data=complete_fixture(); data['open_findings']=[{'blocks_pp2':True,'status':'RESOLVED'}]
        self.assertEqual([],validator.evidence_errors(META,data,True))

    def test_duplicate_json_keys(self):
        with self.assertRaises(ValueError):
            json.loads('{"a":1,"a":2}', object_pairs_hook=validator.unique_object)

    def test_invalid_evidence_type(self):
        self.assertTrue(validator.evidence_errors(META,[]))


if __name__ == '__main__':
    unittest.main()
