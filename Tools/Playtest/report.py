"""
re:SquashStrike playtest report (2026-10-30).

Reads the session files the game writes (the save folder's "playtest"
folder: session-*.jsonl, one JSON object a line) - your friends send
them to you - and prints a summary as Markdown:

    python report.py FOLDER_OR_FILES... [> report.md]

What it looks at, and why:
  shots      where your shots died (back corner / middle / side wall
             first ...) by shot and by control mode - is the aim good?
  rallies    won, length, how they ended - by opponent
  matches    won and lost by opponent - is the AI ladder right?
  felt bad   every F8 mark with its context - what felt wrong
  gauntlet   floors reached, where runs ended
  coins      earned an hour, and what they were spent on
  screens    the screens used, and where players quit
"""

import glob
import json
import os
import sys
from collections import Counter, defaultdict


def files(args):
    out = []
    for a in args or ["."]:
        if os.path.isdir(a):
            out += sorted(glob.glob(os.path.join(a, "**", "session-*.jsonl"), recursive=True))
        else:
            out += glob.glob(a)
    return out


def load(path):
    rows = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError:
                pass                                    # a line cut off by a crash
    return rows


def pct(a, b):
    return "-" if b == 0 else f"{100.0 * a / b:.0f}%"


def main(argv):
    paths = files(argv)
    if not paths:
        print("No session-*.jsonl files found.")
        return
    sessions = [(p, load(p)) for p in paths]
    out = []
    w = out.append
    total_s = sum(max((r.get("t") or 0) for r in rows) for _, rows in sessions if rows)
    w("# Playtest report")
    w("")
    w(f"{len(sessions)} sessions, {total_s / 3600:.1f} hours in all.")
    versions = Counter(r.get("version") for _, rows in sessions for r in rows if r.get("ev") == "session")
    w("Versions: " + ", ".join(f"{v} ({n})" for v, n in versions.most_common()))
    w("")

    every = [r for _, rows in sessions for r in rows]
    by = defaultdict(list)
    for r in every:
        by[r.get("ev")].append(r)

    # ---- shots ----
    shots = [s for s in by["shot"] if not s.get("serve")]
    w("## Your shots (serves left out)")
    w("")
    if shots:
        places = ["back_corner", "back_middle", "side", "middle", "front", "returned", "down"]
        w("| shot | n | " + " | ".join(places) + " | side wall first |")
        w("|---|---|" + "---|" * len(places) + "---|")
        for kind in sorted({s.get("shot") for s in shots}, key=lambda k: -sum(1 for s in shots if s.get("shot") == k)):
            ks = [s for s in shots if s.get("shot") == kind]
            c = Counter(s.get("where") for s in ks)
            w(f"| {kind} | {len(ks)} | " + " | ".join(pct(c[p], len(ks)) for p in places) +
              f" | {pct(sum(1 for s in ks if s.get('side_first')), len(ks))} |")
        drives = [s for s in shots if s.get("shot") in ("drive", "kill")]
        if drives:
            q = sum(s.get("quality") or 0 for s in drives) / len(drives)
            w("")
            w(f"Drives and kills: {pct(sum(1 for s in drives if s.get('where') == 'back_corner'), len(drives))} died in a back corner, "
              f"{pct(sum(1 for s in drives if s.get('where') == 'middle'), len(drives))} in the middle; mean contact quality {q:.2f}.")
    else:
        w("No shots recorded.")
    w("")

    # ---- matches and rallies ----
    w("## Matches by opponent")
    w("")
    opp = None
    per = defaultdict(lambda: {"m": 0, "won": 0, "r": 0, "rw": 0, "shots": 0})
    for _, rows in sessions:
        for r in rows:
            if r.get("ev") == "match":
                opp = f"{r.get('mode')}: {r.get('opponent')}"
            elif r.get("ev") == "match_end" and opp:
                per[opp]["m"] += 1
                per[opp]["won"] += 1 if r.get("won") else 0
            elif r.get("ev") == "rally" and opp:
                per[opp]["r"] += 1
                per[opp]["rw"] += 1 if r.get("won") else 0
                per[opp]["shots"] += r.get("shots") or 0
    if per:
        w("| opponent | matches | won | rallies won | shots a rally |")
        w("|---|---|---|---|---|")
        for k, v in sorted(per.items(), key=lambda kv: -kv[1]["r"]):
            w(f"| {k} | {v['m']} | {pct(v['won'], v['m'])} | {pct(v['rw'], v['r'])} ({v['r']}) | {v['shots'] / max(1, v['r']):.1f} |")
        ends = Counter(r.get("reason") or r.get("end") for r in by["rally"] if not r.get("won"))
        w("")
        w("Rallies you lost ended by: " + ", ".join(f"{k} {n}" for k, n in ends.most_common(8)))
    else:
        w("No matches recorded.")
    w("")

    # ---- felt bad ----
    w(f"## Felt bad (F8) - {len(by['felt_bad'])} marks")
    w("")
    for r in by["felt_bad"]:
        if r.get("in_match"):
            w(f"- {r.get('t', 0) / 60:.0f} min: last shot **{r.get('last_shot')}**, {r.get('phase')}, score {r.get('score')}, "
              f"ball ({r.get('ball_x')}, {r.get('ball_z')}), you ({r.get('you_x')}, {r.get('you_z')})")
        else:
            w(f"- {r.get('t', 0) / 60:.0f} min: on {r.get('where')}")
    w("")

    # ---- gauntlet ----
    over = by["gauntlet_over"]
    w("## The Gauntlet")
    w("")
    if over:
        fl = sorted(r.get("floors") or 0 for r in over)
        w(f"{len(over)} runs ended: floors cleared median {fl[len(fl) // 2]}, best {fl[-1]}, worst {fl[0]}.")
        lost = Counter(r.get("floor") for r in by["gauntlet"] if not r.get("won"))
        w("Hearts lost on floors: " + ", ".join(f"{k} ({n})" for k, n in sorted(lost.items())))
    else:
        w("No runs ended.")
    w("")

    # ---- coins ----
    earned = sum(r.get("earned") or 0 for r in by["coins"])
    spent = Counter()
    for r in by["coins"]:
        if r.get("spent"):
            on = (r.get("on") or "?").split(" ")[0]
            spent[on] += r["spent"]
    why = Counter()
    for r in by["coins"]:
        if r.get("earned"):
            why[r.get("why")] += r["earned"]
    w("## Coins")
    w("")
    w(f"Earned {earned:,} ({earned / max(0.01, total_s / 3600):,.0f} an hour); spent {sum(spent.values()):,}.")
    w("Earned from: " + ", ".join(f"{k} {v:,}" for k, v in why.most_common(8)))
    w("Spent on: " + ", ".join(f"{k} {v:,}" for k, v in spent.most_common()))
    w("")

    # ---- screens ----
    w("## Screens")
    w("")
    sc = Counter(r.get("name") for r in by["screen"])
    w("Most opened: " + ", ".join(f"{k} {n}" for k, n in sc.most_common(12)))
    quits = Counter(r.get("screen") for r in by["quit"])
    w("Quit from: " + (", ".join(f"{k} {n}" for k, n in quits.most_common()) or "-"))
    print("\n".join(out))


if __name__ == "__main__":
    main(sys.argv[1:])
