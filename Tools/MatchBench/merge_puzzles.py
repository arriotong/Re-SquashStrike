# ===========================================================
# Tools/MatchBench/merge_puzzles.py
# Playable challenges (2026-10-28): the game's shot-puzzle bank from the
# --puzzle-bank runs - every answer about as often (the AI's boasts win a
# lot: capped like the rest), interleaved so days in a row differ, as JSON
# lines in Assets/SquashGame/Resources/Challenges/puzzles.txt.
#
#   python merge_puzzles.py OUT CAP bank_0.jsonl bank_1.jsonl ...
# ===========================================================
import json, sys, random

out, cap, files = sys.argv[1], int(sys.argv[2]), sys.argv[3:]
by = {}
seen = set()
for f in files:
    for line in open(f, encoding='utf-8'):
        line = line.strip()
        if not line: continue
        p = json.loads(line)
        if p['Seed'] in seen: continue
        seen.add(p['Seed'])
        by.setdefault(p['Options'][p['Best']], []).append(line)
rng = random.Random(20261028)
for k in by: rng.shuffle(by[k]); by[k] = by[k][:cap]
order = sorted(by)
lines = []
i = 0
while any(by[k] for k in order):
    k = order[i % len(order)]; i += 1
    if by[k]: lines.append(by[k].pop())
with open(out, 'w', encoding='utf-8', newline='\n') as w:
    w.write('\n'.join(lines) + '\n')
print(len(lines), 'puzzles:', {k: sum(1 for l in lines if json.loads(l)['Options'][json.loads(l)['Best']] == k) for k in order})
