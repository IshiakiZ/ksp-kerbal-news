# Kerbal News

A news channel in flight: a kerbal anchor at his desk talks over what your flight does, in about 1,500 lines in one voice,
with a caption under each and his mouth moving with his voice.

**Version 0.1.0, the first release.** All 1,504 of its lines are voiced (ElevenLabs, the voice Robert).

For Kerbal Space Program 1.12.x. **Needs [Keystone](https://github.com/IshiakiZ/ksp-keystone)**, a separate download
([on SpaceDock](https://spacedock.info/mod/4665/Keystone)): this mod does not load without it.

## Install

Copy the `KerbalNews` folder from the download into your KSP's `GameData`, beside `Keystone`.

## Use

Switch the channel on with **News channel** in the Keystone window (the keystone button on the game's toolbar, or Alt-K;
Option-K on a Mac), under **Kerbal News**: that shows nothing by itself. Then in flight:

* **Alt-N** (Option-N on a Mac) brings his window up in a corner, static first and then the newsroom. Pressed again, he signs
  off and the window folds as he finishes; pressed once more meanwhile, it folds at once.
* **What he talks about:** the rocket on the pad, lift-off, staging, heights passed, space, orbit, a new world, re-entry,
  parachutes, touchdown or splashdown, a crash, a kerbal outside, a docking, a flag, science, low fuel. In the quiet between,
  what the flight is doing (time warp, a coast in orbit, the night side and the sunrise, a rover's drive, a plane's cruise),
  or the studio's own: jokes, facts about the worlds, viewers' letters, a word from the sponsors. Recover the craft while he
  is on and he says so at the space centre.
* **The whole screen:** the button at the window's top right puts him on the whole screen, with your flight shown beside
  him when something is happening; the craft still answers its keys. The button at the top right puts him back.
* **In the Keystone window:** which corner his window is in and how big, how much he talks, his voice's loudness, captions.
  With Keystone 0.11.0 (to come), four finer details in its Advanced view: how long the static lasts as he comes on, how long the box
  takes to grow to the whole screen, how long a quiet spell lasts before he fills it, and the least time between his lines.
  [All of them](docs/Settings.md).

[What each setting does](docs/Settings.md); [the lines](docs/The-lines.md).

## Works on

Mac (OpenGL) and Windows 11 (Direct3D 11 and OpenGL); Linux never run.

**More:** [the long description](docs/The-long-description.md) (everything in full, and what was tried on
each system) and the rest of the [wiki](docs/README.md).

## Licence

[CC BY 4.0](LICENSE): use it, change it and pass it on as you like, as long as you credit Ishiaki and link back here.
The anchor's voice is made with ElevenLabs.
