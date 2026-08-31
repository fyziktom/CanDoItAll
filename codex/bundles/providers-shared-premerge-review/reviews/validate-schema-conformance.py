"""Validate captured generated OpenAPI request schemas with a Draft 2020-12 validator."""
import json
import sys
import xml.etree.ElementTree as ET

from jsonschema import Draft202012Validator

root = ET.parse(sys.argv[1]).getroot()
cases = 0
operations = []
for node in root.iter():
    if node.tag.endswith("StdOut") and node.text:
        for line in node.text.splitlines():
            if not line.startswith('{"SchemaConformance":'):
                continue
            data = json.loads(line)
            schema = data["Schema"]
            Draft202012Validator.check_schema(schema)
            validator = Draft202012Validator(schema)
            for case in data["Cases"]:
                errors = list(validator.iter_errors(case["Payload"]))
                actual = not errors
                assert actual == case["Accepted"], (
                    data["Operation"], case["Payload"], [error.message for error in errors]
                )
                cases += 1
            operations.append(data["Operation"])
assert sorted(operations) == ["ChatCompletions", "ImageGenerations", "Responses"], operations
print(json.dumps({"validator": "jsonschema 4.25.1 / Draft 2020-12", "operations": operations, "cases": cases, "result": "pass"}))
