#!/usr/bin/env python3
"""Render a self-contained Celerity coverage report from a Cobertura XML file.

Reads the coverage produced by `dotnet test --collect:"XPlat Code Coverage"`
(coverlet, Cobertura format) and emits, into the output directory:

  index.html    a styled overview matching the Celerity site, with per-file line
                and branch coverage and links to the uncovered source lines
  badge.svg     a shields-style line-coverage badge (no third-party branding)
  summary.md    a short Markdown summary for the PR comment

It also enforces the coverage floor: if line or branch coverage is below the
configured minimum it writes the summary, prints the numbers, and exits non-zero.

This replaces ReportGenerator so the report carries no "sponsors only" upsell and
matches the project's own dashboard styling. See docs/testing.md.

Usage:
    coverage_report.py --input "<glob-or-path>" --outdir coveragereport \
        [--min-line 95] [--min-branch 90] [--title "Celerity coverage"]
"""
import argparse
import glob
import html
import os
import re
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone


def find_input(pattern: str) -> str:
    matches = sorted(glob.glob(pattern, recursive=True), key=os.path.getmtime)
    if not matches:
        sys.exit(f"coverage_report: no coverage file matched '{pattern}'")
    return matches[-1]


_COND = re.compile(r"\((\d+)/(\d+)\)")


def parse(path: str):
    """Returns (overall, files) where files is a list of per-file dicts."""
    root = ET.parse(path).getroot()

    # Aggregate per source file. Multiple <class> elements (a type and its nested
    # enumerators) share one filename; merge their lines, deduping by line number.
    files: dict[str, dict] = {}
    for cls in root.iter("class"):
        fname = cls.get("filename", "")
        f = files.setdefault(fname, {"lines": {}, "branches": {}})
        for line in cls.iter("line"):
            num = int(line.get("number"))
            hits = int(line.get("hits", "0"))
            # max hits wins if a line appears under more than one class element
            f["lines"][num] = max(f["lines"].get(num, 0), hits)
            if line.get("branch") == "True":
                cc = line.get("condition-coverage", "")
                m = _COND.search(cc)
                if m:
                    covered, total = int(m.group(1)), int(m.group(2))
                    prev = f["branches"].get(num, (0, 0))
                    # keep the richer reading (most branches seen for this line)
                    if total >= prev[1]:
                        f["branches"][num] = (covered, total)

    file_rows = []
    for fname, data in files.items():
        lines = data["lines"]
        lvalid = len(lines)
        lcov = sum(1 for h in lines.values() if h > 0)
        uncovered = sorted(n for n, h in lines.items() if h == 0)
        bcov = sum(c for c, _ in data["branches"].values())
        btot = sum(t for _, t in data["branches"].values())
        file_rows.append({
            "filename": fname,
            "display": display_path(fname),
            "blob": blob_url(fname),
            "lines_covered": lcov,
            "lines_valid": lvalid,
            "line_rate": (lcov / lvalid) if lvalid else 1.0,
            "branches_covered": bcov,
            "branches_valid": btot,
            "branch_rate": (bcov / btot) if btot else None,
            "uncovered": uncovered,
        })

    file_rows.sort(key=lambda r: (r["line_rate"], r["branch_rate"] if r["branch_rate"] is not None else 1.0, r["display"]))

    overall = {
        "lines_covered": int(root.get("lines-covered", 0)),
        "lines_valid": int(root.get("lines-valid", 0)),
        "branches_covered": int(root.get("branches-covered", 0)),
        "branches_valid": int(root.get("branches-valid", 0)),
    }
    overall["line_rate"] = (overall["lines_covered"] / overall["lines_valid"]) if overall["lines_valid"] else 1.0
    overall["branch_rate"] = (overall["branches_covered"] / overall["branches_valid"]) if overall["branches_valid"] else 1.0
    return overall, file_rows


def display_path(fname: str) -> str:
    """Repo-relative path from a SourceLink raw URL, or the raw path otherwise."""
    m = re.match(r"https?://raw\.githubusercontent\.com/[^/]+/[^/]+/[^/]+/(.+)$", fname)
    if m:
        return m.group(1)
    return fname.replace("\\", "/")


def blob_url(fname: str) -> str | None:
    """Browsable github.com/blob URL from a SourceLink raw URL, else None."""
    m = re.match(r"https?://raw\.githubusercontent\.com/([^/]+)/([^/]+)/([^/]+)/(.+)$", fname)
    if m:
        owner, repo, sha, path = m.groups()
        return f"https://github.com/{owner}/{repo}/blob/{sha}/{path}"
    return None


def pct(x: float) -> str:
    # Show "100%" only when genuinely complete; otherwise keep enough precision
    # that a near-perfect value never rounds up to a misleading 100.0%.
    if x >= 1.0:
        return "100%"
    v = x * 100
    s = f"{v:.2f}".rstrip("0").rstrip(".")
    if s in ("100", "100."):
        s = f"{v:.4f}".rstrip("0").rstrip(".")
    return s + "%"


def grade(rate: float) -> str:
    if rate >= 0.90:
        return "good"
    if rate >= 0.75:
        return "ok"
    if rate >= 0.50:
        return "warn"
    return "bad"


def color(rate: float) -> str:
    return {"good": "#0d6e6e", "ok": "#5a8f00", "warn": "#b8860b", "bad": "#b3261e"}[grade(rate)]


def compress_ranges(nums: list[int]) -> list[tuple[int, int]]:
    """Collapses a sorted int list into (start, end) inclusive ranges."""
    out: list[tuple[int, int]] = []
    for n in nums:
        if out and n == out[-1][1] + 1:
            out[-1] = (out[-1][0], n)
        else:
            out.append((n, n))
    return out


def uncovered_html(row: dict) -> str:
    if not row["uncovered"]:
        return '<span class="none">—</span>'
    blob = row["blob"]
    parts = []
    for start, end in compress_ranges(row["uncovered"]):
        label = f"{start}" if start == end else f"{start}–{end}"
        if blob:
            parts.append(f'<a href="{blob}#L{start}">{label}</a>')
        else:
            parts.append(f"<span>{label}</span>")
    return ", ".join(parts)


def bar(rate, label) -> str:
    if rate is None:
        return '<div class="cell-rate"><span class="rate none">n/a</span></div>'
    c = color(rate)
    width = round(rate * 100, 1)
    return (
        f'<div class="cell-rate">'
        f'<div class="bar"><span style="width:{width}%;background:{c}"></span></div>'
        f'<span class="rate" style="color:{c}">{label}</span>'
        f"</div>"
    )


def render_html(overall, files, title) -> str:
    generated = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    rows = []
    for r in files:
        line_lbl = f'{pct(r["line_rate"])} <span class="frac">{r["lines_covered"]}/{r["lines_valid"]}</span>'
        if r["branch_rate"] is None:
            branch_lbl = None
        else:
            branch_lbl = f'{pct(r["branch_rate"])} <span class="frac">{r["branches_covered"]}/{r["branches_valid"]}</span>'
        name = html.escape(r["display"])
        name_cell = f'<a href="{r["blob"]}">{name}</a>' if r["blob"] else name
        rows.append(
            "<tr>"
            f'<td class="file">{name_cell}</td>'
            f'<td>{bar(r["line_rate"], line_lbl)}</td>'
            f'<td>{bar(r["branch_rate"], branch_lbl)}</td>'
            f'<td class="uncovered">{uncovered_html(r)}</td>'
            "</tr>"
        )

    lc = color(overall["line_rate"])
    bc = color(overall["branch_rate"])
    return f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{html.escape(title)}</title>
<style>
  :root {{
    --fg:#1a1d1f; --fg-muted:#5a6166; --fg-faint:#8a9296;
    --bg:#ffffff; --bg-alt:#f7f8f8; --border:#e5e8ea; --accent:#0d6e6e;
    --font:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Oxygen,Ubuntu,Cantarell,"Helvetica Neue",Helvetica,Arial,sans-serif;
    --mono:ui-monospace,SFMono-Regular,"SF Mono",Menlo,Consolas,"Liberation Mono",monospace;
  }}
  * {{ box-sizing:border-box; }}
  html,body {{ margin:0; padding:0; }}
  body {{ font-family:var(--font); color:var(--fg); background:var(--bg); line-height:1.55; font-size:16px; -webkit-font-smoothing:antialiased; }}
  a {{ color:var(--accent); text-decoration:none; }}
  a:hover {{ text-decoration:underline; }}
  .wrap {{ max-width:1100px; margin:0 auto; padding:0 24px; }}
  header.site {{ border-bottom:1px solid var(--border); padding:18px 0; }}
  header.site .wrap {{ display:flex; align-items:center; gap:24px; }}
  header.site .brand {{ font-weight:600; font-size:17px; color:var(--fg); letter-spacing:-0.01em; }}
  header.site .brand .dot {{ color:var(--accent); }}
  header.site nav {{ display:flex; gap:18px; margin-left:auto; font-size:14px; }}
  header.site nav a {{ color:var(--fg-muted); }}
  header.site nav a:hover {{ color:var(--fg); text-decoration:none; }}
  section.hero {{ padding:48px 0 8px; }}
  .hero h1 {{ font-size:30px; line-height:1.15; margin:0 0 6px; letter-spacing:-0.025em; font-weight:600; }}
  .hero p.lead {{ font-size:15px; color:var(--fg-muted); margin:0; }}
  .cards {{ display:flex; gap:16px; flex-wrap:wrap; margin:28px 0 8px; }}
  .card {{ flex:1; min-width:200px; border:1px solid var(--border); border-radius:10px; padding:18px 20px; background:var(--bg-alt); }}
  .card .k {{ font-size:12px; text-transform:uppercase; letter-spacing:0.06em; color:var(--fg-faint); margin-bottom:6px; }}
  .card .v {{ font-size:32px; font-weight:600; letter-spacing:-0.02em; }}
  .card .sub {{ font-size:13px; color:var(--fg-muted); margin-top:2px; }}
  .section-head {{ margin:40px 0 12px; }}
  .section-head h2 {{ margin:0; font-size:13px; text-transform:uppercase; letter-spacing:0.07em; color:var(--fg-faint); font-weight:600; }}
  table {{ width:100%; border-collapse:collapse; font-size:14px; }}
  thead th {{ text-align:left; font-size:12px; text-transform:uppercase; letter-spacing:0.05em; color:var(--fg-faint); font-weight:600; padding:8px 12px; border-bottom:1px solid var(--border); }}
  thead th:nth-child(2), thead th:nth-child(3) {{ width:180px; }}
  tbody td {{ padding:9px 12px; border-bottom:1px solid var(--border); vertical-align:middle; }}
  tbody tr:hover {{ background:var(--bg-alt); }}
  td.file a, td.file {{ font-family:var(--mono); font-size:13px; color:var(--fg); }}
  .cell-rate {{ display:flex; align-items:center; gap:10px; }}
  .bar {{ flex:1; height:7px; border-radius:4px; background:var(--border); overflow:hidden; min-width:70px; }}
  .bar span {{ display:block; height:100%; border-radius:4px; }}
  .rate {{ font-variant-numeric:tabular-nums; font-size:13px; white-space:nowrap; }}
  .rate.none {{ color:var(--fg-faint); }}
  .frac {{ color:var(--fg-faint); font-size:11px; }}
  td.uncovered {{ font-family:var(--mono); font-size:12px; color:var(--fg-muted); }}
  td.uncovered a {{ color:var(--fg-muted); }}
  td.uncovered a:hover {{ color:var(--accent); }}
  td.uncovered .none {{ color:var(--fg-faint); }}
  footer {{ border-top:1px solid var(--border); margin-top:48px; padding:24px 0; font-size:13px; color:var(--fg-faint); }}
  footer .wrap {{ display:flex; justify-content:space-between; gap:16px; flex-wrap:wrap; }}
</style>
</head>
<body>
<header class="site">
  <div class="wrap">
    <a href="../" class="brand">Celerity<span class="dot">.</span></a>
    <nav>
      <a href="../dev/bench/">Benchmarks</a>
      <a href="../">Home</a>
      <a href="https://github.com/marius-bughiu/Celerity">GitHub</a>
    </nav>
  </div>
</header>

<section class="hero">
  <div class="wrap">
    <h1>Code coverage</h1>
    <p class="lead">Line and branch coverage for the <code>Celerity</code> library, measured by coverlet on every push. Generated {generated}.</p>
    <div class="cards">
      <div class="card">
        <div class="k">Line coverage</div>
        <div class="v" style="color:{lc}">{pct(overall["line_rate"])}</div>
        <div class="sub">{overall["lines_covered"]} of {overall["lines_valid"]} lines</div>
      </div>
      <div class="card">
        <div class="k">Branch coverage</div>
        <div class="v" style="color:{bc}">{pct(overall["branch_rate"])}</div>
        <div class="sub">{overall["branches_covered"]} of {overall["branches_valid"]} branches</div>
      </div>
      <div class="card">
        <div class="k">Files</div>
        <div class="v">{len(files)}</div>
        <div class="sub">in the Celerity assembly</div>
      </div>
    </div>
  </div>
</section>

<div class="wrap">
  <div class="section-head"><h2>By file &middot; lowest coverage first</h2></div>
  <table>
    <thead><tr><th>File</th><th>Line</th><th>Branch</th><th>Uncovered lines</th></tr></thead>
    <tbody>
      {"".join(rows)}
    </tbody>
  </table>
</div>

<footer>
  <div class="wrap">
    <span>Generated by <code>scripts/coverage_report.py</code> &middot; no third-party report tooling.</span>
    <span><a href="../">Celerity dashboard</a> &middot; <a href="https://github.com/marius-bughiu/Celerity/blob/main/docs/testing.md">Testing guide</a></span>
  </div>
</footer>
</body>
</html>
"""


def render_badge(rate: float) -> str:
    label = "coverage"
    value = pct(rate)
    c = color(rate)
    # Approximate widths (px) for the default 11px DejaVu Sans shields layout.
    # The value half scales with the text so "100%" and "99.97%" both fit.
    lw = 62
    vw = 14 + len(value) * 7
    total = lw + vw
    lx = lw * 10 // 2
    vx = (lw + vw // 2) * 10
    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="{total}" height="20" role="img" aria-label="{label}: {value}">
  <title>{label}: {value}</title>
  <linearGradient id="s" x2="0" y2="100%"><stop offset="0" stop-color="#bbb" stop-opacity=".1"/><stop offset="1" stop-opacity=".1"/></linearGradient>
  <clipPath id="r"><rect width="{total}" height="20" rx="3" fill="#fff"/></clipPath>
  <g clip-path="url(#r)">
    <rect width="{lw}" height="20" fill="#555"/>
    <rect x="{lw}" width="{vw}" height="20" fill="{c}"/>
    <rect width="{total}" height="20" fill="url(#s)"/>
  </g>
  <g fill="#fff" text-anchor="middle" font-family="Verdana,Geneva,DejaVu Sans,sans-serif" font-size="110" text-rendering="geometricPrecision">
    <text x="{lx}" y="150" fill="#010101" fill-opacity=".3" transform="scale(.1)" textLength="{(lw - 12) * 10}">{label}</text>
    <text x="{lx}" y="140" transform="scale(.1)" textLength="{(lw - 12) * 10}">{label}</text>
    <text x="{vx}" y="150" fill="#010101" fill-opacity=".3" transform="scale(.1)" textLength="{(vw - 12) * 10}">{value}</text>
    <text x="{vx}" y="140" transform="scale(.1)" textLength="{(vw - 12) * 10}">{value}</text>
  </g>
</svg>
"""


def render_summary(overall, files, min_line, min_branch, passed) -> str:
    worst = [f for f in files if f["line_rate"] < 1.0][:5]
    lines = [
        "### Coverage",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f'| Line | {pct(overall["line_rate"])} ({overall["lines_covered"]}/{overall["lines_valid"]}) |',
        f'| Branch | {pct(overall["branch_rate"])} ({overall["branches_covered"]}/{overall["branches_valid"]}) |',
        "",
    ]
    if not passed:
        lines.append("> :x: **Coverage is below the configured floor.**")
        lines.append("")
    if worst:
        lines.append("<details><summary>Files below 100% line coverage</summary>")
        lines.append("")
        lines.append("| File | Line | Branch |")
        lines.append("|---|---:|---:|")
        for f in worst:
            br = pct(f["branch_rate"]) if f["branch_rate"] is not None else "n/a"
            lines.append(f'| `{f["display"]}` | {pct(f["line_rate"])} | {br} |')
        lines.append("")
        lines.append("</details>")
    return "\n".join(lines) + "\n"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--input", required=True, help="Cobertura XML path or glob")
    ap.add_argument("--outdir", required=True)
    ap.add_argument("--min-line", type=float, default=0.0)
    ap.add_argument("--min-branch", type=float, default=0.0)
    ap.add_argument("--title", default="Celerity coverage")
    args = ap.parse_args()

    path = find_input(args.input)
    overall, files = parse(path)
    os.makedirs(args.outdir, exist_ok=True)

    with open(os.path.join(args.outdir, "index.html"), "w", encoding="utf-8") as f:
        f.write(render_html(overall, files, args.title))
    with open(os.path.join(args.outdir, "badge.svg"), "w", encoding="utf-8") as f:
        f.write(render_badge(overall["line_rate"]))

    line_pct = overall["line_rate"] * 100
    branch_pct = overall["branch_rate"] * 100
    passed = line_pct >= args.min_line and branch_pct >= args.min_branch

    summary = render_summary(overall, files, args.min_line, args.min_branch, passed)
    with open(os.path.join(args.outdir, "summary.md"), "w", encoding="utf-8") as f:
        f.write(summary)

    step_summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if step_summary:
        with open(step_summary, "a", encoding="utf-8") as f:
            f.write(summary)

    print(f"Source:          {path}")
    print(f"Line coverage:   {line_pct:.2f}% (floor {args.min_line:.0f}%)")
    print(f"Branch coverage: {branch_pct:.2f}% (floor {args.min_branch:.0f}%)")
    print(f"Report:          {os.path.join(args.outdir, 'index.html')}")
    if not passed:
        print("::error::Coverage dropped below the configured floor.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
