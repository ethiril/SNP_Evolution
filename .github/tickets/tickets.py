#!/usr/bin/env python3
"""The ticket registry: issues, labels, and milestones as files.

Tickets are authored here and applied to GitHub, so a breakdown can be written in
one pass and reviewed as a diff instead of typed into a browser. Apply is
idempotent and additive: it creates and updates, and never deletes an issue, a
label, or a milestone.

  tickets.py pull --milestone M0     write files from what GitHub already has
  tickets.py plan                    read-only: what apply would change
  tickets.py apply                   create and update, writing new numbers back

A closed issue is settled work, so both commands pass over it unless --all says
otherwise. Requires the gh CLI, authenticated. Standard library only.
"""

import argparse
import difflib
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
LABELS = ROOT / "labels.json"
MILESTONES = ROOT / "milestones.json"

# Frontmatter keys, in the order they are written back. A key absent from a file
# means "leave this alone on GitHub", which is what lets a ticket file own only
# the fields it cares about. An empty list is different: it sets no labels.
FIELDS = ("number", "title", "milestone", "labels", "parent", "state")
SCALARS = ("number", "title", "milestone", "parent", "state")


def fail(message):
    print(f"tickets: {message}", file=sys.stderr)
    sys.exit(1)


# --- gh -----------------------------------------------------------------------


def gh(path, method="GET", payload=None, paginate=False):
    command = ["gh", "api", path, "--method", method]

    if paginate:
        # Without --slurp a second page arrives as concatenated arrays, which is not
        # a JSON document; with it, one array per page, flattened below.
        command += ["--paginate", "--slurp"]
    if payload is not None:
        command += ["--input", "-"]

    result = subprocess.run(
        command,
        input=json.dumps(payload) if payload is not None else None,
        capture_output=True,
        text=True,
    )

    if result.returncode != 0:
        fail(f"{method} {path} failed: {result.stderr.strip()}")

    if not result.stdout.strip():
        return None

    parsed = json.loads(result.stdout)

    return [record for page in parsed for record in page] if paginate else parsed


def repository():
    result = subprocess.run(
        ["gh", "repo", "view", "--json", "nameWithOwner", "--jq", ".nameWithOwner"],
        capture_output=True,
        text=True,
    )

    if result.returncode != 0:
        fail(f"cannot resolve the repository: {result.stderr.strip()}")

    return result.stdout.strip()


# --- ticket files -------------------------------------------------------------


def read(path):
    text = path.read_text()

    if not text.startswith("---\n"):
        fail(f"{path.name} does not open with a frontmatter block")

    head, separator, body = text[4:].partition("\n---\n")

    if not separator:
        fail(f"{path.name} has an unterminated frontmatter block")

    data = {}
    key = None

    for number, line in enumerate(head.splitlines(), start=2):
        if not line.strip():
            continue

        if line.startswith("  - "):
            if not isinstance(data.get(key), list):
                fail(f"{path.name}:{number} list item under no list key")

            data[key].append(line[4:].strip())
            continue

        match = re.fullmatch(r"([a-z_]+):\s*(.*)", line)

        if match is None:
            fail(f"{path.name}:{number} is not 'key: value' or '  - item'")

        key, value = match.group(1), match.group(2).strip().strip('"')

        if key not in FIELDS:
            fail(f"{path.name}:{number} unknown key '{key}'; known keys are {', '.join(FIELDS)}")

        data[key] = value if value else []

    for scalar in SCALARS:
        if isinstance(data.get(scalar), list):
            fail(f"{path.name} declares '{scalar}' with no value")

    if "title" not in data:
        fail(f"{path.name} declares no title")

    for numeric in ("number", "parent"):
        if data.get(numeric, "").isdigit():
            data[numeric] = int(data[numeric])

    return data, body.strip()


def write(path, data, body):
    lines = ["---"]

    for field in FIELDS:
        if field not in data:
            continue

        value = data[field]

        if isinstance(value, list):
            lines.append(f"{field}:")
            lines += [f"  - {item}" for item in value]
        else:
            lines.append(f"{field}: {value}")

    lines.append("---")
    path.write_text("\n".join(lines) + "\n\n" + body.strip() + "\n")


def tickets():
    """Every ticket file, parents before children.

    A parent may be an issue number or another file's stem, so a milestone's epic
    and its children can be created in one run.
    """
    found = {}

    for path in sorted(p for p in ROOT.rglob("*.md") if p.name != "README.md"):
        data, body = read(path)

        if path.stem in found:
            fail(f"two ticket files share the stem '{path.stem}'")

        found[path.stem] = (path, data, body)

    ordered = []
    placed = set()

    def place(stem, chain):
        if stem in placed:
            return
        if stem in chain:
            fail(f"parent cycle through '{stem}'")

        path, data, body = found[stem]
        parent = data.get("parent")

        if isinstance(parent, str) and parent:
            if parent not in found:
                fail(f"{path.name} names parent '{parent}', which is neither a number nor a ticket file")

            place(parent, chain | {stem})

        placed.add(stem)
        ordered.append((path, data, body))

    for stem in found:
        place(stem, frozenset())

    return ordered


# --- configuration ------------------------------------------------------------


def configuration(path, noun):
    if not path.exists():
        fail(f"{path.name} is missing; run 'tickets.py pull' to write it from {noun} on GitHub")

    return json.loads(path.read_text())


def ensure_labels(repo, dry):
    declared = configuration(LABELS, "the labels")
    existing = {label["name"]: label for label in gh(f"repos/{repo}/labels?per_page=100", paginate=True)}
    changes = []

    for name, spec in declared.items():
        wanted = {"color": spec["color"].lstrip("#"), "description": spec.get("description", "")}
        former = spec.get("renamed_from")

        if name not in existing and former and former in existing:
            changes.append(f"label  rename  {former} -> {name}")

            if not dry:
                gh(f"repos/{repo}/labels/{former}".replace(" ", "%20"), "PATCH", {"new_name": name, **wanted})

            continue

        if name not in existing:
            changes.append(f"label  create  {name}")

            if not dry:
                gh(f"repos/{repo}/labels", "POST", {"name": name, **wanted})

            continue

        current = existing[name]

        if current["color"].lower() != wanted["color"].lower() or (current["description"] or "") != wanted["description"]:
            changes.append(f"label  update  {name}")

            if not dry:
                gh(f"repos/{repo}/labels/{name}".replace(" ", "%20"), "PATCH", wanted)

    return changes


def ensure_milestones(repo, dry):
    """Returns the key -> number map tickets resolve their milestone through.

    Milestones are keyed by their prefix rather than their full title, so a
    renamed milestone stays the same milestone.
    """
    declared = configuration(MILESTONES, "the milestones")
    existing = gh(f"repos/{repo}/milestones?state=all&per_page=100", paginate=True)
    by_key = {milestone["title"].split(":", 1)[0].strip(): milestone for milestone in existing}
    resolved = {}
    changes = []

    for key, spec in declared.items():
        wanted = {"title": spec["title"], "description": spec.get("description", "")}
        current = by_key.get(key)

        if current is None:
            changes.append(f"milestone  create  {wanted['title']}")

            if dry:
                continue

            resolved[key] = gh(f"repos/{repo}/milestones", "POST", wanted)["number"]
            continue

        resolved[key] = current["number"]

        if current["title"] != wanted["title"] or (current["description"] or "") != wanted["description"]:
            changes.append(f"milestone  update  {wanted['title']}")

            if not dry:
                gh(f"repos/{repo}/milestones/{current['number']}", "PATCH", wanted)

    return resolved, changes


# --- apply --------------------------------------------------------------------


def issues(repo):
    """Every issue in the repository, keyed by number.

    Reading the registry a ticket at a time is one round trip per file, which the
    list endpoint does in a handful of pages carrying the same fields a diff reads.
    """
    return {
        issue["number"]: issue
        for issue in gh(f"repos/{repo}/issues?state=all&per_page=100", paginate=True)
        if "pull_request" not in issue
    }


def normalise(body):
    return (body or "").replace("\r\n", "\n").strip()


def differences(issue, data, body, milestones):
    """The fields GitHub disagrees with, as a patch payload."""
    patch = {}

    if data["title"] != issue["title"]:
        patch["title"] = data["title"]

    if normalise(body) != normalise(issue["body"]):
        patch["body"] = body

    if "labels" in data:
        if sorted(data["labels"]) != sorted(label["name"] for label in issue["labels"]):
            patch["labels"] = data["labels"]

    if "milestone" in data:
        wanted = milestones.get(data["milestone"])

        if wanted is None:
            fail(f"'{data['milestone']}' is not a milestone key in {MILESTONES.name}")

        if (issue["milestone"] or {}).get("number") != wanted:
            patch["milestone"] = wanted

    if "state" in data and data["state"] != issue["state"]:
        patch["state"] = data["state"]

    return patch


def link(repo, parent, issue, dry):
    """Parents the issue, unless it is already there.

    Parentage is the one field the list endpoint omits, so it costs a read of its
    own; only a ticket that declares a parent pays for it.
    """
    current = gh(f"repos/{repo}/issues/{issue['number']}").get("parent_issue_url") or ""

    if current.endswith(f"/issues/{parent}"):
        return None

    if not dry:
        payload = {"sub_issue_id": issue["id"]}

        # Only one parent is possible, so a reparent has to say so explicitly.
        if current:
            payload["replace_parent"] = True

        gh(f"repos/{repo}/issues/{parent}/sub_issues", "POST", payload)

    return f"#{issue['number']}  parent  -> #{parent}"


def settled(issue, data, everything):
    """Whether GitHub has closed this ticket and the file is not asking to reopen it.

    Most of the registry is finished milestones, and reconciling them costs a request
    each to confirm what nobody is editing.
    """
    return not everything and issue["state"] == "closed" and data.get("state", "closed") == "closed"


def run(repo, dry, verbose, everything):
    changes = ensure_labels(repo, dry)
    milestones, milestone_changes = ensure_milestones(repo, dry)
    changes += milestone_changes
    existing = issues(repo)
    numbers = {}
    skipped = 0

    for path, data, body in tickets():
        parent = data.get("parent")

        if isinstance(parent, str) and parent:
            parent = numbers.get(parent)

            if parent is None and not dry:
                fail(f"{path.name}'s parent was not created")

        if "number" not in data:
            under = f" under #{parent}" if parent else ""
            changes.append(f"NEW    create  {data['title']}{under}")

            if dry:
                continue

            payload = {"title": data["title"], "body": body}

            if data.get("labels"):
                payload["labels"] = data["labels"]
            if "milestone" in data:
                payload["milestone"] = milestones[data["milestone"]]

            issue = gh(f"repos/{repo}/issues", "POST", payload)
            data["number"] = issue["number"]
            numbers[path.stem] = issue["number"]
            write(path, data, body)

            # Create cannot set state, so a ticket recording work already done
            # would otherwise need a second apply to close.
            if data.get("state", "open") != "open":
                issue = gh(f"repos/{repo}/issues/{issue['number']}", "PATCH", {"state": data["state"]})
        else:
            issue = existing.get(data["number"])

            if issue is None:
                fail(f"{path.name} names #{data['number']}, which the repository does not have")

            numbers[path.stem] = data["number"]

            if settled(issue, data, everything):
                skipped += 1
                continue

            patch = differences(issue, data, body, milestones)

            if patch:
                changes.append(f"#{data['number']}  update  {', '.join(sorted(patch))}")

                if verbose and "body" in patch:
                    changes += [
                        "         " + line.rstrip()
                        for line in difflib.unified_diff(
                            normalise(issue["body"]).splitlines(),
                            normalise(body).splitlines(),
                            fromfile="github",
                            tofile=path.name,
                            lineterm="",
                        )
                    ]

                if not dry:
                    gh(f"repos/{repo}/issues/{data['number']}", "PATCH", patch)

        if parent:
            linked = link(repo, parent, issue, dry)

            if linked:
                changes.append(linked)

    return changes, skipped


# --- pull ---------------------------------------------------------------------


def slug(title):
    text = re.sub(r"[^a-z0-9]+", "-", title.lower()).strip("-")

    return text[:56].rstrip("-")


def pull(repo, milestone, numbers, overwrite):
    if not LABELS.exists() or overwrite:
        labels = gh(f"repos/{repo}/labels?per_page=100", paginate=True)
        LABELS.write_text(
            json.dumps(
                {
                    label["name"]: {"color": label["color"], "description": label["description"] or ""}
                    for label in sorted(labels, key=lambda label: label["name"])
                },
                indent=2,
            )
            + "\n"
        )
        print(f"wrote {LABELS.name}")

    if not MILESTONES.exists() or overwrite:
        milestones = gh(f"repos/{repo}/milestones?state=all&per_page=100", paginate=True)
        MILESTONES.write_text(
            json.dumps(
                {
                    milestone["title"].split(":", 1)[0].strip(): {
                        "title": milestone["title"],
                        "description": milestone["description"] or "",
                    }
                    for milestone in sorted(milestones, key=lambda milestone: milestone["number"])
                },
                indent=2,
            )
            + "\n"
        )
        print(f"wrote {MILESTONES.name}")

    if numbers:
        issues = [gh(f"repos/{repo}/issues/{number}") for number in numbers]
    else:
        query = "state=all&per_page=100"

        if milestone:
            found = [
                m
                for m in gh(f"repos/{repo}/milestones?state=all&per_page=100", paginate=True)
                if m["title"].split(":", 1)[0].strip() == milestone
            ]

            if not found:
                fail(f"no milestone keyed '{milestone}'")

            query += f"&milestone={found[0]['number']}"

        issues = [issue for issue in gh(f"repos/{repo}/issues?{query}", paginate=True) if "pull_request" not in issue]

    for issue in issues:
        key = (issue["milestone"] or {}).get("title", "unfiled").split(":", 1)[0].strip().lower()
        path = ROOT / key / f"{issue['number']:04d}-{slug(issue['title'])}.md"

        if path.exists() and not overwrite:
            print(f"kept   {path.relative_to(ROOT)}")
            continue

        data = {
            "number": issue["number"],
            "title": issue["title"],
            "labels": sorted(label["name"] for label in issue["labels"]),
        }

        if issue["milestone"]:
            data["milestone"] = issue["milestone"]["title"].split(":", 1)[0].strip()
        if issue.get("parent_issue_url"):
            data["parent"] = int(issue["parent_issue_url"].rsplit("/", 1)[1])
        if issue["state"] != "open":
            data["state"] = issue["state"]

        path.parent.mkdir(parents=True, exist_ok=True)
        write(path, data, issue["body"] or "")
        print(f"wrote  {path.relative_to(ROOT)}")


# --- entry point --------------------------------------------------------------


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)

    for name in ("plan", "apply"):
        command = commands.add_parser(name)
        command.add_argument("-v", "--verbose", action="store_true", help="show body diffs")
        command.add_argument("--all", action="store_true", help="reconcile closed tickets as well")

    fetch = commands.add_parser("pull")
    fetch.add_argument("--milestone", help="milestone key, e.g. M0")
    fetch.add_argument("numbers", nargs="*", type=int)
    fetch.add_argument(
        "--overwrite", action="store_true", help="let GitHub win over ticket files that already exist"
    )

    arguments = parser.parse_args()
    repo = repository()

    if arguments.command == "pull":
        pull(repo, arguments.milestone, arguments.numbers, arguments.overwrite)
        return

    dry = arguments.command == "plan"
    changes, skipped = run(repo, dry, arguments.verbose, arguments.all)

    if changes:
        print("\n".join(changes))
        print(f"\n{len(changes)} change(s){' — run apply to make them' if dry else ' applied'}")
    else:
        print("nothing to do")

    if skipped:
        print(f"left {skipped} closed ticket(s) alone; --all reconciles those too")


if __name__ == "__main__":
    main()
