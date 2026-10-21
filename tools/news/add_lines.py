#!/usr/bin/env python3
"""add_lines.py MODULE...: the lines in tools/news/<MODULE>.py (a dict LINES of event -> words) added to Kerbal News'
PluginData/lines.txt, each event's numbered after the ones it has (welcome_02, welcome_03, ...); words already there are
skipped, so a batch can be added again after it is changed."""
import os, re, sys, importlib
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
PATH = os.path.normpath(os.path.join(HERE, "..", "..", "src", "KerbalNews", "PluginData", "lines.txt"))
text = open(PATH, encoding="utf-8").read()
have, top = set(), {}
for raw in text.splitlines():
    if not raw.strip() or raw.startswith("#"): continue
    parts = raw.split("\t")
    if len(parts) < 3: continue
    have.add(parts[2].strip())
    m = re.match(r"(.+)_(\d+)$", parts[1].strip())
    if m: top[m.group(1)] = max(top.get(m.group(1), 0), int(m.group(2)))
added = 0
with open(PATH, "a", encoding="utf-8") as out:
    for name in sys.argv[1:]:
        for event, words in importlib.import_module(name).LINES.items():
            for w in words:
                if w in have: continue
                top[event] = top.get(event, 0) + 1
                out.write("%s\t%s_%02d\t%s\n" % (event, event, top[event], w))
                have.add(w); added += 1
print(added, "lines added;", sum(1 for l in open(PATH, encoding="utf-8") if l.strip() and not l.startswith("#")), "in all")
