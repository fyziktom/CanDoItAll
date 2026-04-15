Use this skill when comparing the Mouser office-order files.

1. Use `workspace_convert_document` for the PDF and `workspace_inspect_spreadsheet` for the spreadsheet.
2. Read the generated markdown for the PDF, but do not use `workspace_read_file` on the raw `.xls` file because it is binary.
3. Ignore dates, order numbers, payment terms, addresses, invoice totals, and other header/footer metadata that are not actual line items.
4. Compare the ordered part numbers and quantities across both sources.
5. Confirm whether every ordered item shipped in full or is available in stock.
6. Calculate the top three items by extended price and cite only the Mouser catalog part numbers without quantity prefixes.
7. `matchingItemLists` must be a boolean, not a list of line items.
8. `topThreeMostExpensive` must contain exactly three objects that use only `mouserNo`, `extendedPriceUsd`, and `reason`.
9. In `mouserNo`, use the full Mouser catalog number including prefixes such as `709-` or `485-`; do not substitute customer/manufacturer part numbers such as `PV-SMI-2000A`, `4754`, or `4960`.
10. Do not copy raw invoice/export JSON fields such as `invoiceNumber`, `totalAmount`, `shipToAddress`, `orderedQty`, `unitPrice`, or a full line-item dump into the final answer.
11. Output exactly one JSON object with only `matchingItemLists`, `allAvailableInStock`, `topThreeMostExpensive`, and `notes`. Do not add prose, markdown fences, tables, or headings.
