# Playtesting re:SquashStrike

Thanks for testing! The game keeps a small diary of how you play, so we can tune it. The diary stays on your PC; nothing is sent anywhere. It records numbers only: where your shots went, rally results, Gauntlet floors, coins earned and spent, and which screens you used. No names.

## While you play

- **Press F8** (or **click the right stick** on a pad) the moment something feels wrong: a shot that went somewhere odd, a call you disagree with, a screen that confused you. You'll see "Marked - thanks!". Then jot a few words about it if you can.
- Play however you like: Quick Match, the career, the Gauntlet, the store. The more normal, the better.

## When you're done

Send the files from this folder (paste it into the Explorer address bar):

```
%USERPROFILE%\AppData\LocalLow\arriotong\reSquashStrike\playtest
```

There is one `session-....jsonl` file each time you open the game. Zip the folder and send it, with your notes about the F8 moments.

## For the developer: the report

```
python Tools/Playtest/report.py path/to/the/folders > report.md
```

It sums every session up:

- where shots died, by shot;
- results by opponent;
- every F8 mark with its context;
- Gauntlet floors reached;
- coins earned an hour and what they went on;
- the screens used, and where players quit.
