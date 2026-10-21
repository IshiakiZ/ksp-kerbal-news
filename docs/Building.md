# Building it yourself

`./build.sh` builds the mod and installs it into the game; `./build.sh check` only compiles; `./build.sh dist`
builds into `GameData/` here without touching the game. It needs the .NET SDK, a copy of the game (`KSP_DIR=/path`
if it is not where Steam puts it), and `Keystone.dll` from [Keystone](https://github.com/IshiakiZ/ksp-keystone):
the copy installed in the game, a built copy of that repository beside this one, or `KEYSTONE=/path/to/Keystone.dll`.

This repository is made from a workspace that holds several mods side by side; changes arrive here from there.

Its `PluginData` comes with it as it is: `lines.txt` (every line: its event, its name and its words), `Lines/` (one `.ogg` a
line) and `Suit/` (the suit and the microphone, as meshes).

## The lines

* The words are written in batches, `tools/news/lines_a.py` to `lines_k.py`, each a dict of event to lines.
  `python3 tools/news/add_lines.py lines_k` adds a batch to `lines.txt`, each event's lines numbered after the ones it has;
  words already there are skipped.
* `python3 tools/news/standins.py` makes stand-in voices from the Mac's own speech (`say`, the voice Ralph) for lines without a
  sound yet, so that the channel can be tried before the real ones are made. They are `.wav` files and kept out of git.
* `python3 tools/news/make_lines.py` speaks every line that has no `.ogg` yet through ElevenLabs' text to speech, in the voice
  Robert (`tNIuvXGG5RnGdTbvfnPR`, model `eleven_v4`), and writes mono Ogg Vorbis. It needs your own API key, in the
  environment (`ELEVENLABS_API_KEY`) or in `~/.ksp-ai-bridge/elevenlabs.key`, and never prints it. `--dry` says what it would
  make and roughly what it would cost (about one credit a character) and sends nothing; run it again to carry on where it
  stopped. The words in square brackets are the delivery (`[warmly]`, `[laughs]`): spoken as directions, left out of the
  caption.

## The suit and the microphone

Made in Blender from the game's own kerbal (2026-10-19), headless:

* the development build writes the anchor's skinned parts to a folder (`KerbalNews.News.Export`);
* `blender --background --python tools/news/blender/make_suit.py -- EXPORT_FOLDER OUT.mesh PREVIEW.png` cuts the suit from the
  spacesuit's own surface, so that every piece rides on the body's own bones and weights;
* `blender --background --python tools/news/blender/make_mic.py -- OUT.mesh PREVIEW.png` makes the desk microphone;
* `tools/news/blender/look.py` pictures the parts as he sits, and `anchor_io.py` reads and writes the game's mesh form.
