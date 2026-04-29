Use this skill when reconciling records, line items, or facts across documents, spreadsheets, CSV files, or other structured artifacts.

1. Inspect each source with the right tool for its format. Convert PDFs and office documents to readable text or markdown, inspect spreadsheets through structured spreadsheet tooling, and avoid reading binary files as plain text.
2. Identify the comparison key from the user request, process contract, or source columns. If no key is explicit, use the most stable visible identifier and state that assumption in the output.
3. Separate comparison facts from document metadata. Ignore headers, footers, addresses, dates, totals, and payment or routing fields unless the request explicitly asks to compare them.
4. Normalize quantities, dates, units, currencies, casing, and whitespace before deciding whether records match.
5. Compare records by identifier first, then compare requested attributes such as quantity, availability, status, amount, owner, or due date.
6. Report missing, extra, mismatched, and ambiguous records separately. Do not hide exceptions inside a broad success statement.
7. When the request asks for ranked records, sort by the requested metric and include only the fields needed to justify the ranking.
8. Follow the output schema requested by the process or user. If no schema is specified, use a concise structured summary with match status, exceptions, ranked records when relevant, and notes.
9. Do not copy raw source rows, private addresses, payment details, invoice numbers, or full exports into the final response unless the user explicitly asks for those fields.
10. Stop only after every requested source has been inspected and the final answer distinguishes proven matches from assumptions or unreadable inputs.
