# How it works

## The newsroom

The anchor sits at his desk in a newsroom of his own, drawn by a camera of its own into a picture that the channel puts on the
screen. Everything in it is on the game's layer 22, which the game uses for its instructors' portraits at the space centre:
nothing in flight uses it, so the flight's cameras do not draw the newsroom, and its lamps light nothing else. The game's sun
is kept off it, and while its camera draws, the air's own ambient light and fog are put by for the studio's (a newsroom at night
would otherwise be dark).

He is the game's own seated kerbal, the one it puts in cockpits, made with his scripts held back and then taken off, his
animation left to hold him seated, his helmet off. His suit and his microphone were made in Blender from the game's own
spacesuit, so that the suit rides on the body's own bones ([Building it yourself](Building.md)). His face shows the moment
(delighted at the big good ones, amused at his own jokes, alarmed at a crash).

## The window and the whole screen

Switched on, the channel shows nothing. Alt-N brings the window up: the crew portraits' own static for 0.7 seconds (silent: its
hiss, every time, was more than the owner wanted), then his picture clearing in. A banner under him gives the channel's name and the craft's. With the pointer over
it, a button at its top right puts him on the whole screen; there, when something is happening (an event of the moment, a line
about the flight, engines burning), he moves aside and the flight is shown beside him, live, from the game's own camera; the
craft still answers its keys and the camera the mouse, and the game's interface is put away until the button puts him back.
Something of the game's own on top (its pause menu, a dialog, the flight's results) is not hidden.

Alt-N again: he signs off, and the window folds as he finishes; pressed once more meanwhile, it folds at once.

## His mouth

The game moves a kerbal's face only by whole expressions, but a kerbal's head has a mouth of bones: the lower jaw and the
upper, three bones along each half of each lip with one in each lip's middle, and a corner on each side. Each frame, after
his expression's animation, the mouth is laid on that pose by two numbers from his voice where the line is playing (read from
the recording, not from what is heard, which the game's volumes turn down):

* **How open,** by how loud: the jaw turns about the line from corner to corner (up to 15 degrees), the lower lip drops a little
  more at its middle and the upper lifts, so the mouth opens round; quick to open, a little slower to close.
* **Its shape,** by how bright the sound is (how often the wave crosses nought: his voice about 1,400 times a second at the
  middle of its loud stretches): drawn wide at the corners for the bright sounds (ee, s), rounded, the corners in and the lips
  forward, for the dark ones (oo, o).

Until 2026-10-21 only his jaw was turned, about the wrong one of its axes, so that it rolled and the mouth opened at one side
(the owner: "add like mouth rigging so the mouth can actually move and not rotate weird").

## The lines

Each line is read from disk when it is said, whole (not streamed: the mouth reads what is playing), and let go when the next is.
What sets each kind off, and how the quiet is filled: [The lines](The-lines.md).

## After a recovery

The game leaves the flight for the space centre the moment a craft is recovered, and the channel goes with the flight. So his
line on a recovery is handed, as the Recover button is pressed, to a small player of its own that outlives the change of scene:
it waits for the space centre's recovery report (the Mission Summary), says the line in his voice over it with its caption
under it ("KNN: ..."), and goes.
