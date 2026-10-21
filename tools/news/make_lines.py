#!/usr/bin/env python3
"""make_lines.py [--model eleven_v4] [--only EVENT] [--limit N] [--dry]: Kerbal News' lines spoken by ElevenLabs.

Each line of src/KerbalNews/PluginData/lines.txt that has no .ogg yet is sent to ElevenLabs' text-to-speech "with timestamps" in
the anchor's voice (Robert, the American broadcaster, the owner's pick on 2026-10-18), and written as PluginData/Lines/<name>.ogg,
with the time of every letter beside it as <name>.json (for the mouth to follow the words' sounds). A stand-in .wav of the same
name (standins.py) is taken away once the real line is there.

The API key is read from the environment (ELEVENLABS_API_KEY) or from ~/.ksp-ai-bridge/elevenlabs.key, which the owner sets up
themselves; this script never prints it. --dry says what would be made and roughly what it costs (about one credit a character
with this model), and sends nothing. Lines are made one at a time; run it again to carry on where it stopped.
"""
import argparse, base64, json, os, re, subprocess, sys, tempfile, time, urllib.request, urllib.error

VOICE = "tNIuvXGG5RnGdTbvfnPR"          # "Robert - American standard broadcaster"
HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.normpath(os.path.join(HERE, "..", "..", "src", "KerbalNews", "PluginData"))


def key():
    k = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not k:
        path = os.path.expanduser("~/.ksp-ai-bridge/elevenlabs.key")
        if os.path.exists(path): k = open(path).read().strip()
    if not k: sys.exit("No ElevenLabs API key: set ELEVENLABS_API_KEY or put it in ~/.ksp-ai-bridge/elevenlabs.key")
    return k


def lines(only=None):
    for raw in open(os.path.join(DATA, "lines.txt"), encoding="utf-8"):
        raw = raw.rstrip("\n")
        if not raw.strip() or raw.startswith("#"): continue
        parts = raw.split("\t")
        if len(parts) < 3: continue
        if only and parts[0].strip() != only: continue
        yield parts[0].strip(), parts[1].strip(), parts[2].strip()


def speak(api_key, words, model):
    body = json.dumps({"text": words, "model_id": model}).encode("utf-8")
    request = urllib.request.Request("https://api.elevenlabs.io/v1/text-to-speech/%s/with-timestamps?output_format=mp3_44100_128" % VOICE,
                                     data=body, headers={"xi-api-key": api_key, "Content-Type": "application/json"}, method="POST")
    with urllib.request.urlopen(request, timeout=120) as reply:
        return json.loads(reply.read().decode("utf-8"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="eleven_v4")
    ap.add_argument("--only")
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--dry", action="store_true")
    a = ap.parse_args()
    folder = os.path.join(DATA, "Lines")
    os.makedirs(folder, exist_ok=True)
    todo = [(e, n, w) for e, n, w in lines(a.only) if not os.path.exists(os.path.join(folder, n + ".ogg"))]
    if a.limit: todo = todo[:a.limit]
    chars = sum(len(w) for _, _, w in todo)
    print("%d lines to make, %d characters (about %d credits)" % (len(todo), chars, chars))
    if a.dry or not todo: return
    api_key = key()
    for i, (event, name, words) in enumerate(todo):
        try:
            got = speak(api_key, words, a.model)
        except urllib.error.HTTPError as e:
            sys.exit("ElevenLabs refused line %s: %s %s" % (name, e.code, e.read().decode("utf-8", "replace")[:300]))
        with tempfile.TemporaryDirectory() as tmp:
            mp3 = os.path.join(tmp, "line.mp3")
            open(mp3, "wb").write(base64.b64decode(got["audio_base64"]))
            # (Ogg Vorbis, which the game reads everywhere; mono, as a voice is)
            subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", mp3, "-ac", "1", "-c:a", "libvorbis", "-q:a", "5",
                            os.path.join(folder, name + ".ogg")], check=True)
        json.dump(got.get("normalized_alignment") or got.get("alignment") or {}, open(os.path.join(folder, name + ".json"), "w"))
        stand_in = os.path.join(folder, name + ".wav")
        if os.path.exists(stand_in): os.remove(stand_in)
        print("%d/%d %s" % (i + 1, len(todo), name), flush=True)
        time.sleep(0.2)


if __name__ == "__main__":
    main()
