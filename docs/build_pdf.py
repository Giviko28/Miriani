"""Render a Markdown doc to a styled, print-ready HTML file.

Usage: python build_pdf.py <input.md> <output.html>
Then print the HTML to PDF with headless Chrome (--print-to-pdf).
"""

import sys
from pathlib import Path

import markdown

CSS = """
@page { size: A4; margin: 18mm 16mm; }
* { box-sizing: border-box; }
body {
  font-family: -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
  font-size: 11pt; line-height: 1.5; color: #1f2933; max-width: 100%;
}
h1 { font-size: 22pt; color: #0f172a; border-bottom: 3px solid #2563eb;
     padding-bottom: 6px; margin-top: 0; }
h2 { font-size: 15pt; color: #0f172a; margin-top: 22px;
     border-bottom: 1px solid #e2e8f0; padding-bottom: 4px; }
h3 { font-size: 12.5pt; color: #334155; }
p, li { color: #1f2933; }
a { color: #2563eb; text-decoration: none; }
code { font-family: "Cascadia Code", Consolas, "Courier New", monospace;
       font-size: 9.5pt; background: #f1f5f9; padding: 1px 4px; border-radius: 3px;
       color: #b91c1c; }
pre { background: #0f172a; color: #e2e8f0; padding: 12px 14px; border-radius: 6px;
      overflow-x: auto; font-size: 8.5pt; line-height: 1.4; page-break-inside: avoid; }
pre code { background: transparent; color: inherit; padding: 0; font-size: 8.5pt; }
table { border-collapse: collapse; width: 100%; margin: 12px 0; font-size: 10pt;
        page-break-inside: avoid; }
th, td { border: 1px solid #cbd5e1; padding: 6px 10px; text-align: left;
         vertical-align: top; }
th { background: #f1f5f9; color: #0f172a; }
tr:nth-child(even) td { background: #f8fafc; }
strong { color: #0f172a; }
hr { border: none; border-top: 1px solid #e2e8f0; margin: 20px 0; }
h2, h3 { page-break-after: avoid; }
"""

src = Path(sys.argv[1])
out = Path(sys.argv[2])
html_body = markdown.markdown(
    src.read_text(encoding="utf-8"),
    extensions=["tables", "fenced_code", "toc", "sane_lists"],
)
doc = f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<title>{src.stem}</title><style>{CSS}</style></head>
<body>{html_body}</body></html>"""
out.write_text(doc, encoding="utf-8")
print(f"wrote {out}")
