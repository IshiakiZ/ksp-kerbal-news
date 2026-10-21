#!/usr/bin/env python3
"""standins.py: stand-in voices for Kerbal News' lines, from the Mac's own speech ("say", voice Ralph), so the channel can be
built and tried before the lines are made with ElevenLabs (make_lines.py). Each line of src/KerbalNews/PluginData/lines.txt
without a sound yet gets PluginData/Lines/<name>.wav (its words without the delivery in brackets). Not for releasing: the
folder's .wav files are kept out of git."""
import os, re, subprocess, tempfile
HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "..", "src", "KerbalNews", "PluginData")
made = 0
for raw in open(os.path.join(DATA, "lines.txt")):
    raw = raw.rstrip("\n")
    if not raw.strip() or raw.startswith("#"): continue
    parts = raw.split("\t")
    if len(parts) < 3: continue
    name, words = parts[1].strip(), re.sub(r"\s+", " ", re.sub(r"\[[^\]]*\]", "", parts[2])).strip()
    out = os.path.join(DATA, "Lines", name + ".wav")
    if os.path.exists(out) or os.path.exists(os.path.join(DATA, "Lines", name + ".ogg")): continue
    with tempfile.TemporaryDirectory() as tmp:
        aiff = os.path.join(tmp, "line.aiff")
        subprocess.run(["say", "-v", "Ralph", "-o", aiff, words], check=True)
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", aiff, "-ar", "22050", "-ac", "1", out], check=True)
    made += 1
print(made, "stand-in lines made")
