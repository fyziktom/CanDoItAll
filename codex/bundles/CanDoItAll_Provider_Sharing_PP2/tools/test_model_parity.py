#!/usr/bin/env python3
"""Negative controls for the handoff snapshot checker, not product tests."""
import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("parity_check", Path(__file__).with_name("check_model_parity.py"))
parity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(parity)

SOURCE = "11111111-1111-1111-1111-111111111111"
PUBLICATION = "22222222-2222-2222-2222-222222222222"
ROUTE = "sp1." + "2" * 32 + "." + "A" * 43
ALT = "sp1." + "2" * 32 + "." + "B" * 43

def sample():
    models = [{"route_id":ROUTE,"display_name":"Native model A","is_suggested":True},
              {"route_id":ALT,"display_name":"Native model B","is_suggested":False}]
    source = {"source_instance_id":SOURCE,"publication_id":PUBLICATION,"revision":"r1",
              "default_route_id":ROUTE,"models":models}
    clients = []
    for index in (3,4):
        item = {**copy.deepcopy(source),"local_provider_id":f"{index}"*8+"-"+f"{index}"*4+"-"+f"{index}"*4+"-"+f"{index}"*4+"-"+f"{index}"*12,
                "default_display_name":"Native model A",
                "labels":[{"surface":"agent-default","route_id":ROUTE,"display_name":"Native model A"}]}
        clients.append({"client_id":f"client-{index}","expected_publications":[{"source_instance_id":SOURCE,"publication_id":PUBLICATION}],"imports":[item]})
    return {"schema_version":1,"status":"OBSERVED","application_commit":"a"*40,
            "image_digest":"sha256:"+"b"*64,"phase":"test-only checker fixture",
            "source_publications":[source],"clients":clients}

class ParityChecks(unittest.TestCase):
    def reject(self, change):
        value = sample()
        change(value)
        self.assertTrue(parity.validate(value)[0])
    def test_valid_subset_labels_do_not_require_all_models_in_dropdown(self):
        errors, counts = parity.validate(sample())
        self.assertEqual([], errors)
        self.assertEqual(2, counts["clients"])
    def test_names_compared_exactly(self):
        self.reject(lambda s:s["clients"][0]["imports"][0]["models"][0].update(display_name="native model a"))
    def test_missing_model(self):
        self.reject(lambda s:s["clients"][0]["imports"][0]["models"].pop())
    def test_wrong_default(self):
        self.reject(lambda s:s["clients"][0]["imports"][0].update(default_route_id=ALT))
    def test_raw_route_in_visible_label(self):
        self.reject(lambda s:s["clients"][0]["imports"][0]["labels"][0].update(display_name=ROUTE))
    def test_stale_revision(self):
        self.reject(lambda s:s["clients"][0]["imports"][0].update(revision="old"))
    def test_wrong_source(self):
        self.reject(lambda s:s["clients"][0]["imports"][0].update(source_instance_id="99999999-9999-9999-9999-999999999999"))
    def test_duplicate_client(self):
        self.reject(lambda s:s["clients"].__setitem__(1,copy.deepcopy(s["clients"][0])))
    def test_one_client_insufficient(self):
        self.reject(lambda s:s["clients"].pop())
    def test_missing_import(self):
        self.reject(lambda s:s["clients"][0].update(imports=[]))
    def test_not_run_is_not_observed(self):
        self.reject(lambda s:s.update(status="NOT_RUN"))
    def test_extra_sensitive_fields_rejected(self):
        self.reject(lambda s:s.update(secret="test-only"))
    def test_malformed_origin(self):
        self.reject(lambda s:s.update(application_commit="uncommitted"))
    def test_duplicate_source(self):
        self.reject(lambda s:s["source_publications"].append(copy.deepcopy(s["source_publications"][0])))
    def test_wrong_suggestion(self):
        self.reject(lambda s:s["clients"][0]["imports"][0]["models"][0].update(is_suggested=False))
    def test_duplicate_route(self):
        self.reject(lambda s:s["clients"][0]["imports"][0]["models"].append(copy.deepcopy(s["clients"][0]["imports"][0]["models"][0])))
    def test_wrong_publication_route(self):
        self.reject(lambda s:s["clients"][0]["imports"][0]["models"][0].update(route_id="sp1."+"7"*32+"."+"A"*43))
    def test_duplicate_json_rejected(self):
        with self.assertRaises(parity.InvalidSnapshot):
            parity.unique_object([("field",1),("field",2)])
    def test_missing_ui_labels(self):
        self.reject(lambda s:s["clients"][0]["imports"][0].update(labels=[]))

if __name__ == "__main__":
    unittest.main()
