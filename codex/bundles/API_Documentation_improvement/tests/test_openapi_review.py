"""Synthetic self-tests for the preparation pack's offline review helper.

These tests do not build or exercise CanDoItAll. They verify the helper only.
"""
from __future__ import annotations

import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

TOOL = Path(__file__).resolve().parents[1] / 'tools' / 'openapi_review.py'
SPEC = importlib.util.spec_from_file_location('openapi_review', TOOL)
assert SPEC is not None and SPEC.loader is not None
review = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(review)


def document() -> dict:
    return {
        'openapi': '3.1.1',
        'info': {'title': 'Synthetic API', 'version': '1', 'description': 'Test document.'},
        'servers': [{'url': 'https://api.example.test/', 'description': 'Synthetic host.'}],
        'paths': {
            '/records/{id}': {
                'parameters': [{
                    'name': 'id', 'in': 'path', 'required': True,
                    'description': 'Identifier of the record to read.',
                    'schema': {'type': 'string'},
                }],
                'get': {
                    'operationId': 'ReadRecord',
                    'summary': 'Read one record.',
                    'description': 'Returns the current record for the supplied identifier.',
                    'responses': {'200': {
                        'description': 'The current record was read.',
                        'headers': {'ETag': {'description': 'Revision of the returned representation.', 'schema': {'type': 'string'}}},
                        'content': {'application/json': {'schema': {'$ref': '#/components/schemas/Record'}}},
                    }},
                },
            },
        },
        'components': {'schemas': {'Record': {
            'type': 'object', 'description': 'A synthetic record used only by helper tests.',
            'required': ['id'],
            'properties': {
                'id': {'type': 'string', 'description': 'Stable identifier of this record.'},
                'description': {'type': ['string', 'null'], 'description': 'Text entered by the record author.'},
            },
        }}},
    }


class AuditTests(unittest.TestCase):
    def audit(self, value: dict) -> dict:
        return review.Auditor(value).run()

    def test_complete_document_passes(self):
        result = self.audit(document())
        self.assertEqual((0, 0), (result['errors'], result['warnings']))
        self.assertEqual(1, result['counts']['operations'])
        self.assertEqual(2, result['counts']['properties'])

    def test_empty_export_is_not_accepted_as_coverage_proof(self):
        doc = document()
        doc['paths'] = {}
        self.assertIn('no_operations', [f['code'] for f in self.audit(doc)['findings']])

    def test_missing_operation_summary_is_reported(self):
        doc = document()
        del doc['paths']['/records/{id}']['get']['summary']
        self.assertIn('missing_summary', [f['code'] for f in self.audit(doc)['findings']])

    def test_blank_property_description_is_reported(self):
        doc = document()
        doc['components']['schemas']['Record']['properties']['id']['description'] = '  '
        result = self.audit(doc)
        self.assertTrue(any(f['pointer'].endswith('/properties/id') for f in result['findings']))

    def test_generic_placeholder_is_flagged(self):
        doc = document()
        doc['paths']['/records/{id}']['get']['responses']['200']['description'] = 'Success'
        self.assertIn('suspected_placeholder', [f['code'] for f in self.audit(doc)['findings']])

    def test_reference_type_description_does_not_replace_property_role(self):
        doc = document()
        doc['components']['schemas']['Record']['properties']['parent'] = {'$ref': '#/components/schemas/Record'}
        result = self.audit(doc)
        self.assertTrue(any(f['code'] == 'missing_description' and f['pointer'].endswith('/properties/parent') for f in result['findings']))

    def test_recursive_schema_reference_terminates(self):
        doc = document()
        doc['components']['schemas']['Record']['properties']['parent'] = {
            '$ref': '#/components/schemas/Record', 'description': 'Parent of this record.'}
        self.assertEqual(0, self.audit(doc)['errors'])

    def test_missing_local_reference_is_reported(self):
        doc = document()
        doc['components']['schemas']['Record']['properties']['parent'] = {
            '$ref': '#/components/schemas/Missing', 'description': 'Parent of this record.'}
        self.assertIn('unresolved_ref', [f['code'] for f in self.audit(doc)['findings']])

    def test_external_reference_is_not_fetched(self):
        doc = document()
        doc['components']['schemas']['Record']['properties']['parent'] = {
            '$ref': 'https://example.test/schema.json', 'description': 'Parent of this record.'}
        self.assertIn('unresolved_ref', [f['code'] for f in self.audit(doc)['findings']])

    def test_escaped_reference_names_resolve(self):
        doc = document()
        doc['components']['schemas']['A/B~C'] = {'type': 'string', 'description': 'Synthetic escaped schema name.'}
        doc['components']['schemas']['Record']['properties']['key'] = {
            '$ref': '#/components/schemas/A~1B~0C', 'description': 'An escaped key.'}
        self.assertEqual(0, self.audit(doc)['errors'])

    def test_duplicate_operation_id_is_reported(self):
        doc = document()
        doc['paths']['/other'] = {'get': copy.deepcopy(doc['paths']['/records/{id}']['get'])}
        self.assertIn('duplicate_operation_id', [f['code'] for f in self.audit(doc)['findings']])

    def test_request_body_requires_description(self):
        doc = document()
        doc['paths']['/records/{id}']['get']['requestBody'] = {'content': {'application/json': {'schema': {'type': 'string'}}}}
        self.assertTrue(any('request_body' in f['message'] for f in self.audit(doc)['findings']))

    def test_parameter_and_response_references_are_followed(self):
        doc = document()
        item = doc['paths']['/records/{id}']
        doc['components']['parameters'] = {'RecordId': item['parameters'][0]}
        item['parameters'] = [{'$ref': '#/components/parameters/RecordId'}]
        doc['components']['responses'] = {'ReadResult': item['get']['responses']['200']}
        item['get']['responses']['200'] = {'$ref': '#/components/responses/ReadResult'}
        self.assertEqual(0, self.audit(doc)['errors'])

    def test_object_reference_only_cycle_is_reported(self):
        doc = document()
        doc['components']['responses'] = {'Loop': {'$ref': '#/components/responses/Loop'}}
        self.assertIn('reference_cycle', [f['code'] for f in self.audit(doc)['findings']])

    def test_dictionary_and_composition_children_are_checked(self):
        doc = document()
        doc['components']['schemas']['Bag'] = {
            'description': 'A map of items.', 'type': 'object',
            'additionalProperties': {'oneOf': [{'$ref': '#/components/schemas/Record'}, {'$ref': '#/components/schemas/Missing'}]},
        }
        self.assertIn('unresolved_ref', [f['code'] for f in self.audit(doc)['findings']])

    def test_webhook_and_callback_operations_are_checked(self):
        doc = document()
        op = copy.deepcopy(doc['paths']['/records/{id}']['get'])
        op['operationId'] = 'ReceiveHook'
        doc['webhooks'] = {'RecordChanged': {'post': op}}
        cb_op = copy.deepcopy(op)
        cb_op['operationId'] = 'ReceiveCallback'
        doc['paths']['/records/{id}']['get']['callbacks'] = {
            'Changed': {'{$request.query.callbackUrl}': {'post': cb_op}}}
        result = self.audit(doc)
        self.assertEqual(3, result['counts']['operations'])
        self.assertEqual(0, result['errors'])

    def test_dynamic_reference_requires_review(self):
        doc = document()
        doc['components']['schemas']['Record']['$dynamicRef'] = '#node'
        self.assertIn('dynamic_ref_review', [f['code'] for f in self.audit(doc)['findings']])


class ComparisonTests(unittest.TestCase):
    def test_prose_only_changes_are_accepted(self):
        before = document()
        after = copy.deepcopy(before)
        after['info']['description'] = 'Improved orientation.'
        after['servers'][0]['description'] = 'Improved host description.'
        after['paths']['/records/{id}']['get']['summary'] = 'Read the current record.'
        after['components']['schemas']['Record']['description'] = 'Improved record meaning.'
        after['components']['schemas']['Record']['properties']['description']['description'] = 'Improved property meaning.'
        self.assertTrue(review.compare(before, after)['documentation_only'])

    def test_business_description_property_is_not_removed(self):
        before = document()
        after = copy.deepcopy(before)
        after['components']['schemas']['Record']['properties']['description']['type'] = 'integer'
        self.assertFalse(review.compare(before, after)['documentation_only'])

    def test_business_property_named_examples_is_not_removed(self):
        before = document()
        before['components']['schemas']['Record']['properties']['examples'] = {'type': 'string', 'description': 'Example content.'}
        after = copy.deepcopy(before)
        del after['components']['schemas']['Record']['properties']['examples']
        self.assertFalse(review.compare(before, after)['documentation_only'])

    def test_required_and_nullable_changes_are_structural(self):
        before = document()
        for transform in (
            lambda d: d['components']['schemas']['Record']['required'].append('description'),
            lambda d: d['components']['schemas']['Record']['properties']['description'].update(type='string'),
        ):
            after = copy.deepcopy(before)
            transform(after)
            self.assertFalse(review.compare(before, after)['documentation_only'])

    def test_defaults_and_enum_values_are_structural(self):
        before = document()
        after = copy.deepcopy(before)
        after['components']['schemas']['Record']['properties']['id']['default'] = 'fallback'
        self.assertFalse(review.compare(before, after)['documentation_only'])
        after = copy.deepcopy(before)
        after['components']['schemas']['Record']['properties']['id']['enum'] = ['one', 'two']
        self.assertFalse(review.compare(before, after)['documentation_only'])

    def test_security_and_operation_ids_are_structural(self):
        before = document()
        for patch in ({'security': [{'Bearer': []}]}, {'operationId': 'ReadRenamedRecord'}):
            after = copy.deepcopy(before)
            after['paths']['/records/{id}']['get'].update(patch)
            self.assertFalse(review.compare(before, after)['documentation_only'])

    def test_server_url_is_not_normalized_away(self):
        before = document()
        after = copy.deepcopy(before)
        after['servers'][0]['url'] = 'https://different.example.test/'
        self.assertFalse(review.compare(before, after)['documentation_only'])

    def test_example_values_are_prose_only_in_media_object(self):
        before = document()
        after = copy.deepcopy(before)
        after['paths']['/records/{id}']['get']['responses']['200']['content']['application/json']['example'] = {'id': 'example'}
        self.assertTrue(review.compare(before, after)['documentation_only'])

    def test_contract_default_object_named_description_is_preserved(self):
        before = document()
        before['components']['schemas']['Record']['default'] = {'description': 'default A'}
        after = copy.deepcopy(before)
        after['components']['schemas']['Record']['default']['description'] = 'default B'
        self.assertFalse(review.compare(before, after)['documentation_only'])

    def test_new_response_shape_is_flagged_even_if_correcting_old_metadata(self):
        before = document()
        after = copy.deepcopy(before)
        after['paths']['/records/{id}']['get']['responses']['404'] = {'description': 'The record does not exist.'}
        self.assertFalse(review.compare(before, after)['documentation_only'])

    def test_unknown_extension_content_is_preserved(self):
        before = document()
        before['x-contract'] = {'description': 'contract A'}
        after = copy.deepcopy(before)
        after['x-contract']['description'] = 'contract B'
        self.assertFalse(review.compare(before, after)['documentation_only'])


class InputAndCliTests(unittest.TestCase):
    def load(self, text: str):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'document.json'
            path.write_text(text, encoding='utf-8')
            return review.load_document(path)

    def test_duplicate_members_are_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            self.load('{"openapi":"3.1.1","openapi":"3.1.1"}')

    def test_nan_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Non-standard'):
            self.load('{"openapi":"3.1.1","x-value":NaN}')

    def test_unsupported_version_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Only OpenAPI'):
            self.load('{"swagger":"2.0"}')

    def test_non_object_document_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'JSON object'):
            self.load('[]')

    def test_invalid_pointer_escape_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Invalid JSON Pointer'):
            review.resolve_pointer(document(), '#/components/schemas/A~2B')

    def test_pointer_array_index_resolves(self):
        node, path = review.resolve_pointer({'a': [1, {'b': 2}]}, '#/a/1/b')
        self.assertEqual((2, '#/a/1/b'), (node, path))

    def test_cli_audit_writes_report_and_returns_zero(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source, output = root / 'source.json', root / 'report.json'
            source.write_text(json.dumps(document()), encoding='utf-8')
            process = subprocess.run([sys.executable, str(TOOL), 'audit', str(source), '--output', str(output)], capture_output=True, text=True, timeout=10)
            self.assertEqual(0, process.returncode, process.stderr)
            self.assertEqual(0, json.loads(output.read_text())['errors'])

    def test_cli_compare_returns_one_for_contract_change(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            a, b = document(), document()
            b['paths']['/records/{id}']['get']['operationId'] = 'Changed'
            (root / 'a.json').write_text(json.dumps(a), encoding='utf-8')
            (root / 'b.json').write_text(json.dumps(b), encoding='utf-8')
            process = subprocess.run([sys.executable, str(TOOL), 'compare', str(root / 'a.json'), str(root / 'b.json')], capture_output=True, text=True, timeout=10)
            self.assertEqual(1, process.returncode, process.stderr)
            self.assertFalse(json.loads(process.stdout)['documentation_only'])

    def test_cli_bad_json_returns_two(self):
        with tempfile.TemporaryDirectory() as tmp:
            source = Path(tmp) / 'bad.json'
            source.write_text('{broken', encoding='utf-8')
            process = subprocess.run([sys.executable, str(TOOL), 'audit', str(source)], capture_output=True, text=True, timeout=10)
            self.assertEqual(2, process.returncode)
            self.assertIn('Review failed:', process.stderr)


if __name__ == '__main__':
    unittest.main()
