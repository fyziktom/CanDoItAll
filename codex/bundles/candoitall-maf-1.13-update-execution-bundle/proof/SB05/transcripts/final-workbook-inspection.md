# Final Workbook Inspection

Workbook path:

`C:\Users\lucys\AppData\Local\CanDoItAll\workspace\managed-files\project-media\files\f28c07cd982c4d2dbcf23e60a32eca72\x-ray-machine-pricing-model-8124de720ddd4832a7d00f684928b2ca.xlsx`

Inspection command:

```powershell
& "C:\Users\lucys\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -c "from openpyxl import load_workbook; import json; path=r'C:\Users\lucys\AppData\Local\CanDoItAll\workspace\managed-files\project-media\files\f28c07cd982c4d2dbcf23e60a32eca72\x-ray-machine-pricing-model-8124de720ddd4832a7d00f684928b2ca.xlsx'; wb=load_workbook(path, data_only=False); ws=wb['Pricing']; rows=[[cell.value for cell in row] for row in ws.iter_rows(min_row=1, max_row=ws.max_row, max_col=ws.max_column)]; print(json.dumps({'sheets': wb.sheetnames, 'dimensions': {'rows': ws.max_row, 'cols': ws.max_column}, 'rows': rows}, indent=2, default=str))"
```

Output:

```json
{
  "sheets": [
    "Pricing"
  ],
  "dimensions": {
    "rows": 4,
    "cols": 8
  },
  "rows": [
    [
      "Model",
      "EWX Shenzhen USD",
      "Marketing Low USD",
      "Marketing High USD",
      "Target Price USD",
      "Margin USD",
      "Margin Percent",
      "Source"
    ],
    [
      "ZM-x5600",
      "35000",
      "39900",
      "42000",
      "=(C2+D2)/2",
      "=E2-B2",
      "=IFERROR(F2/E2,\"\")",
      "Quotation for Xrays (PDF p.1)"
    ],
    [
      "ZM-x6600",
      "41500",
      "46000",
      "49000",
      "=(C3+D3)/2",
      "=E3-B3",
      "=IFERROR(F3/E3,\"\")",
      "Quotation for Xrays (PDF p.1)"
    ],
    [
      "ZM-x6600A",
      "66000",
      "73000",
      "78000",
      "=(C4+D4)/2",
      "=E4-B4",
      "=IFERROR(F4/E4,\"\")",
      "Quotation for Xrays (PDF p.1)"
    ]
  ]
}
```

Manual formula check:

| Model | Target Formula | Target | Margin Formula | Margin | Margin Percent |
| --- | --- | ---: | --- | ---: | ---: |
| `ZM-x5600` | `(39900 + 42000) / 2` | `40950` | `40950 - 35000` | `5950` | `14.53%` |
| `ZM-x6600` | `(46000 + 49000) / 2` | `47500` | `47500 - 41500` | `6000` | `12.63%` |
| `ZM-x6600A` | `(73000 + 78000) / 2` | `75500` | `75500 - 66000` | `9500` | `12.58%` |

Result: `Pass`
