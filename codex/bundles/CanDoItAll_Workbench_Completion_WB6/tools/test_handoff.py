"""Tests of the handoff validators only; no application tests are run here."""
from __future__ import annotations
import copy
import io
import json
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest import mock
import inspect_artifact as artifact
import validate_handoff as handoff

ROOT = Path(__file__).resolve().parents[1]
SVG = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 80 40"><rect id="a" width="10" height="10"/><text>A &amp; B</text></svg>'


def workbook_bytes(cell: str = '<c r="A1"><v>2</v></c>', extras: dict[str, str] | None = None) -> bytes:
    # These minimal packages exercise the parser, not spreadsheet application compatibility.
    parts = {
        '[Content_Types].xml': '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"/>',
        '_rels/.rels': '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"/>',
        'xl/workbook.xml': '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Inputs" sheetId="1" r:id="rId1"/></sheets></workbook>',
        'xl/_rels/workbook.xml.rels': '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>',
        'xl/worksheets/sheet1.xml': '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:B2"/><sheetData><row r="1">' + cell + '</row></sheetData></worksheet>'
    }
    parts.update(extras or {})
    output = io.BytesIO()
    with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED) as z:
        for name, value in parts.items():
            z.writestr(name, value)
    return output.getvalue()


class ArtifactTests(unittest.TestCase):
    def inspect_file(self, suffix: str, data: bytes) -> dict:
        with tempfile.TemporaryDirectory() as directory:
            file = Path(directory) / ('fixture' + suffix)
            file.write_bytes(data)
            result = artifact.inspect(file)
            self.assertEqual(file.read_bytes(), data)
            self.assertFalse(result['producer_proven'])
            self.assertEqual(len(result['sha256']), 64)
            return result

    def test_svg_exact_text(self):
        result = self.inspect_file('.svg', SVG.encode())
        self.assertIn('A & B', result['text'])

    def test_svg_invalid_xml(self):
        with self.assertRaises(ValueError):
            artifact.inspect_svg(b'<svg>')

    def test_svg_requires_namespace(self):
        with self.assertRaises(ValueError):
            artifact.inspect_svg(b'<svg/>')

    def test_svg_duplicate_ids(self):
        with self.assertRaises(ValueError):
            artifact.inspect_svg(SVG.replace('</svg>', '<circle id="a"/></svg>').encode())

    def test_svg_local_reference_is_valid(self):
        result = artifact.inspect_svg(SVG.replace('</svg>', '<use href="#a"/></svg>').encode())
        self.assertEqual(result['elements']['use'], 1)

    def test_svg_missing_reference(self):
        with self.assertRaises(ValueError):
            artifact.inspect_svg(SVG.replace('</svg>', '<use href="#missing"/></svg>').encode())

    def test_svg_unsafe_elements_and_attributes(self):
        for addition in ('<script/>', '<foreignObject/>', '<image href="https://invalid.example/image"/>', '<g onclick="alert(1)"/>', '<g style="fill:url(https://invalid.example/)"/>', '<style>@import "remote";</style>'):
            with self.subTest(addition=addition), self.assertRaises(ValueError):
                artifact.inspect_svg(SVG.replace('</svg>', addition + '</svg>').encode())

    def test_xml_dtd_denied(self):
        for encoding in ('utf-8', 'utf-16'):
            with self.subTest(encoding=encoding), self.assertRaises(ValueError):
                artifact.parse_xml(('<!DOCTYPE svg [<!ENTITY a "x">]>' + SVG).encode(encoding))

    def test_xlsx_cells_and_formula_cache_distinct(self):
        result = self.inspect_file('.xlsx', workbook_bytes('<c r="A1"><v>2</v></c><c r="B1"><f>A1*3</f><v>6</v></c>'))
        self.assertEqual(result['formula_count'], 1)
        self.assertFalse(result['formulas_evaluated_by_this_tool'])
        self.assertEqual(result['sheets'][0]['cells'][1]['formula'], 'A1*3')

    def test_xlsx_missing_formula_cache_is_not_invented(self):
        result = self.inspect_file('.xlsx', workbook_bytes('<c r="A1"><f>2+3</f></c>'))
        self.assertEqual(result['formulas_without_cached_values'], 1)
        self.assertIsNone(result['sheets'][0]['cells'][0]['value'])

    def test_xlsx_errors_are_refused(self):
        with self.assertRaises(ValueError):
            self.inspect_file('.xlsx', workbook_bytes('<c r="A1" t="e"><v>#REF!</v></c>'))

    def test_xlsx_macros_are_refused(self):
        with self.assertRaises(ValueError):
            self.inspect_file('.xlsx', workbook_bytes(extras={'xl/vbaProject.bin': 'not executable'}))

    def test_xlsx_external_relationship_is_refused(self):
        with self.assertRaises(ValueError):
            self.inspect_file('.xlsx', workbook_bytes(extras={'xl/worksheets/_rels/sheet1.xml.rels': '<Relationships><Relationship TargetMode="External" Target="https://invalid.example"/></Relationships>'}))

    def test_xlsx_invalid_package(self):
        with self.assertRaises(ValueError):
            self.inspect_file('.xlsx', b'not a workbook')

    def test_xlsx_duplicate_cell_is_refused(self):
        with self.assertRaises(ValueError):
            self.inspect_file('.xlsx', workbook_bytes('<c r="A1"><v>2</v></c><c r="A1"><v>3</v></c>'))

    def test_xlsx_bounded_expansion(self):
        with mock.patch.object(artifact, 'MAX_EXPANDED', 10), self.assertRaises(ValueError):
            self.inspect_file('.xlsx', workbook_bytes())

    def test_raster_actual_decode(self):
        from PIL import Image
        buffer = io.BytesIO()
        Image.new('RGB', (8, 5)).save(buffer, format='PNG')
        result = self.inspect_file('.png', buffer.getvalue())
        self.assertEqual((result['width'], result['height']), (8, 5))

    def test_raster_header_is_not_image(self):
        with self.assertRaises(Exception):
            self.inspect_file('.png', b'\x89PNG\r\n\x1a\n')


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.bundle = json.loads((ROOT / 'bundle.json').read_text())
        self.template = json.loads((ROOT / 'templates/evidence.json').read_text())

    def errors(self, data=None, **kw):
        return handoff.validate_evidence(data or self.template, self.bundle, **kw)

    def valid_claim_shape(self):
        data = copy.deepcopy(self.template)
        data['is_template'] = False
        for g in data['groups']:
            g.update(status='PASS', attempts=['unit-fixture-only'], evidence=['unit-fixture-only'])
        data['readiness'] = dict.fromkeys(data['readiness'], True)
        data['census'] = {'unfinished_active': 0, 'unclassified_active': 0}
        data['source_pair'] = {'main_commit': 'a'*40, 'components_commit': 'b'*40, 'filetools_commit': 'c'*40, 'production_fingerprint':'d'*64, 'published_hash':'e'*64}
        data['candidate'] = {'start_script':'start', 'stop_script':'stop', 'check_script':'check', 'runbook':'runbook', 'restart_count':2, 'restore_test_passed':True}
        data['genuine_provider'] = {'is_scripted':False, 'real_language_vision_attempts':1, 'real_image_attempts':1, 'authorization_reference':'unit fixture'}
        data['artifact_oracles'] = ['unit fixture']; data['signed_commits'] = ['unit fixture']; data['resource_disposition'] = ['unit fixture']
        return data

    def test_template_is_valid_without_readiness(self):
        self.assertFalse(self.errors())

    def test_template_cannot_claim_demo(self):
        self.assertTrue(self.errors(require_demo=True))

    def test_template_cannot_claim_workbench(self):
        self.assertTrue(self.errors(require_complete=True))

    def test_template_results_are_forbidden(self):
        self.template['groups'][0]['status'] = 'PASS'
        self.assertTrue(self.errors())

    def test_group_set_must_be_exact(self):
        self.template['groups'].pop()
        self.assertTrue(self.errors())

    def test_duplicate_group_fails(self):
        self.template['groups'].append(copy.deepcopy(self.template['groups'][0]))
        self.assertTrue(self.errors())

    def test_shape_can_pass_but_authenticity_is_not_inferred(self):
        self.assertFalse(self.errors(self.valid_claim_shape(), require_demo=True))

    def test_scripted_model_does_not_pass_live(self):
        data = self.valid_claim_shape(); data['genuine_provider']['is_scripted'] = True
        self.assertTrue(self.errors(data, require_demo=True))

    def test_zero_image_does_not_pass(self):
        data = self.valid_claim_shape(); data['genuine_provider']['real_image_attempts'] = 0
        self.assertTrue(self.errors(data, require_demo=True))

    def test_blocker_prevents_demo(self):
        data = self.valid_claim_shape(); data['blocking_findings'] = ['real defect']
        self.assertTrue(self.errors(data, require_demo=True))

    def test_original_broad_qualification_requires_exceptions(self):
        data = self.valid_claim_shape()
        next(g for g in data['groups'] if g['id'] == 'G26')['status'] = 'QUALIFIED'
        self.assertTrue(self.errors(data, require_demo=True))
        data['broad_result'] = {'original_status':'FAIL', 'exceptions':['retained exact unit fixture exception']}
        self.assertFalse(self.errors(data, require_demo=True))

    def test_missing_publish_hash_fails(self):
        data = self.valid_claim_shape(); data['source_pair']['published_hash'] = None
        self.assertTrue(self.errors(data, require_demo=True))

    def test_missing_recovery_fails(self):
        data = self.valid_claim_shape(); data['candidate']['restore_test_passed'] = False
        self.assertTrue(self.errors(data, require_demo=True))

    def test_unclassified_surface_fails(self):
        data = self.valid_claim_shape(); data['census']['unclassified_active'] = 1
        self.assertTrue(self.errors(data, require_complete=True))

    def test_unreferenced_pass_is_refused(self):
        data = self.valid_claim_shape(); data['groups'][0]['evidence'] = []
        self.assertTrue(self.errors(data, require_demo=True))


if __name__ == '__main__':
    unittest.main()
