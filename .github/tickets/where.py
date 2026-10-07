#!/usr/bin/env python3
"""Checks that every path named in an open ticket's `Where:` line exists.

    python3 .github/tickets/where.py            open issues, asked of GitHub with gh
    python3 .github/tickets/where.py 46 112     only these issue numbers

A path is a backticked token holding a slash or ending in a file extension the repository uses, so dotted type and
member names are not mistaken for paths. It is looked up from the solution folder
`SNP_Evolution/` first, as the tickets README says, then from the repository root (for `tools/`, `README.md` and the
like). A path written after "new" is one the ticket creates, so only the folder it goes in must exist.
"""
import json
import pathlib
import re
import subprocess
import sys

EXTENSIONS = r"\.(cs|csproj|props|slnx|md|py|sh|metal|json|yml)$"

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOLUTION = ROOT / "SNP_Evolution"
TICKETS = ROOT / ".github" / "tickets"


def open_issues():
    out = subprocess.run(["gh", "issue", "list", "--state", "open", "--limit", "500", "--json", "number"],
                         check=True, capture_output=True, text=True, cwd=ROOT).stdout
    return {issue["number"] for issue in json.loads(out)}


def tickets(numbers):
    for path in sorted(TICKETS.glob("*/*.md")):
        text = path.read_text()
        number = re.search(r"^number:\s*(\d+)\s*$", text, re.M)
        if number and int(number.group(1)) in numbers:
            yield int(number.group(1)), path, text


def paths(where):
    for match in re.finditer(r"(\bnew\s+(?:folder\s+)?)?`([^`]+)`", where):
        token = match.group(2)
        if "/" in token or re.search(EXTENSIONS, token):
            yield token, match.group(1) is not None


def exists(token, new):
    # The app's old single-project folder would otherwise pass from the root.
    bases = (SOLUTION,) if token.startswith("SNP_Evolution/") else (SOLUTION, ROOT)
    for base in bases:
        target = base / token.rstrip("/")
        if (target.parent if new else target).exists():
            return True
    return False


def main():
    numbers = {int(arg) for arg in sys.argv[1:]} or open_issues()
    missing = 0
    checked = 0
    found = 0
    for number, path, text in tickets(numbers):
        found += 1
        where = re.search(r"^Where:(.*?)(?:\n\s*\n|\Z)", text, re.M | re.S)
        if not where:
            continue
        for token, new in paths(where.group(1)):
            checked += 1
            if not exists(token, new):
                missing += 1
                print(f"#{number} {path.relative_to(ROOT)}: {'new ' if new else ''}{token}")
    print(f"{checked} paths in {found} tickets, {missing} missing")
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
