# Gravity Maze: Study Guide

This guide explains how Gravity Maze works for someone who is new to the code. It does not go line by line. It covers what each part does, how the parts talk to each other, and what would break if a part were removed.

How to read it:
1. Read **Part 1** to get the big picture.
2. Read **Part 2**, which covers your part: how the game uses the Arduino and calibration.
3. Use Parts 3 to 7 as a reference when you look at a specific folder or file.
4. Practise with **Part 8**, the likely questions and answers.

---

## Part 1: The big picture

### What the game is
You tilt a real board (the Arduino with the MPU6050 sensor). The game reads the tilt and rolls a metal marble through a maze on screen. You race the clock, avoid walls and holes, and try to reach the goal. Every attempt is saved, so the game can show records, stars and a leaderboard.

### The one idea to remember: separate layers
The project is split into layers, and each layer has one job. A layer only talks to the layers next to it.

```
 ┌──────────────┐   tilt numbers   ┌──────────────┐  ball position  ┌──────────────┐
 │    INPUT     │ ───────────────► │    ENGINE    │ ──────────────► │  RENDERING   │
 │ Arduino +    │   (-1.0 to 1.0)  │ physics,     │   (where the    │ draws the    │
 │ keyboard     │                  │ walls, timer │    ball is)     │ maze + ball  │
 └──────────────┘                  └──────┬───────┘                 └──────────────┘
                                          │ results (time, stars, hits)
                                          ▼
                                   ┌──────────────┐  after every save  ┌──────────────┐
                                   │     DATA     │ ─────────────────► │    MySQL     │
                                   │ records.xml  │    (a copy)        │  (Workbench) │
                                   └──────────────┘                    └──────────────┘
```

- **Input** only produces two numbers: how much left/right and how much up/down.
- **Engine** only does the game rules. It does not know whether the numbers came from the Arduino or the keyboard.
- **Rendering** only draws. It never changes where the ball is.
- **Data** only saves and loads records.
- **UI** (the "shell") is the conductor. It runs the menus and screens and connects all of the above.

**Why this matters:** you can swap one layer without touching the others. For example, the engine was built and tested with the keyboard long before the Arduino was ready. When the Arduino arrived, no engine code had to change.

### The heartbeat: the game loop
The game runs a timer that "ticks" every **16 milliseconds**, about **60 times per second**. On every tick:

1. Read the tilt (from the Arduino or keyboard).
2. If playing: tell the engine to move the ball one small step.
3. Redraw the screen.

That's the whole game, repeated 60 times a second. The timer is in `Form1.vb` and the work for each tick is in `GameShell.Tick()`.

---

## Part 2: How the game uses the Arduino ⭐

This part covers what happens to the numbers the sketch sends after they reach the PC.

### 2.1 What the game expects from the sketch
The game only needs three things from the sketch:

| Requirement | Why |
|---|---|
| `Serial.begin(115200)` | The game opens the COM port at **115200 baud**. A different speed shows up as garbage text, so the board is never recognised. |
| One line per reading: `X: 0.12 Y: -4.90 Z: 8.49` | This is the text the game reads. It finds the numbers after `X:` and `Y:`. `Z` is ignored. |
| `delay(20)` | About **50 readings per second**, which is plenty for smooth movement. |

Any other line, such as `MPU6050 connected!`, is simply ignored, so it doesn't break anything.

> The game also accepts a second format, `0.25,-0.40`, meaning tilt already turned into -1 to 1. This is in case the sketch is ever changed to do the maths itself. The current sketch uses the `X: Y: Z:` format.

### 2.2 The file that talks to the Arduino: `Input/ArduinoController.vb`
This one file is the bridge between the Arduino and the game. It does five jobs:

**1. Finding the board automatically (plug and play)**
- It runs on its own **background thread**, a helper that works alongside the game, so the game never freezes while waiting for the board.
- It goes through every COM port on the PC. It opens each one and waits up to **3.5 seconds** for a line it can read. The extra time is needed because an Uno restarts when its port is opened.
- The first port that sends a valid `X: Y:` line is the board.
- If no board is found, it waits **2 seconds** and tries again, forever. That's why you can plug the board in before or after starting the game.

**2. Zeroing itself when it connects**
- As soon as it connects, it takes the average of the next **15 readings** and treats that position as "flat".
- That's why the footer shows `HOLD LEVEL...` for a moment when the board is plugged in.

**3. Turning sensor numbers into tilt**
The sensor reports acceleration in m/s². When the board is flat, gravity (9.8 m/s²) points straight down on Z, so X and Y are near 0. When you tilt the board, part of gravity shows up on X or Y. The game turns that into a simple number from -1 to 1:

```
tilt = (sensor value ÷ 9.8) ÷ sin(full-tilt angle)
```

*Example:* with the default full-tilt angle of **20°**, tilting the board about **10°** gives a tilt of roughly **0.5**, half speed. Tilting **20° or more** gives **1.0**, full speed.

Then, in order:
1. **Swap / invert** are applied if they are turned on in CALIBRATE.
2. **The "flat" offset is subtracted**, so your zeroed position counts as 0.
3. **Dead zone:** tiny tilts (under 0.08 by default) count as 0, so the ball doesn't drift from hand shake. Above the dead zone the value ramps up smoothly, with no sudden jump.
4. **Clamp:** the result is kept between -1 and 1.
5. **Smoothing:** each new reading only moves the value 35% of the way to the new number. This filters out sensor jitter so the ball feels smooth.

**4. Handling unplugging**
- If no line arrives for **1.5 seconds** (unplugged, cable loose, or the Serial Monitor took the port), it drops the connection, sets the tilt to 0 and starts searching again.
- The game screen notices and **pauses the level by itself**. A banner says the controller was disconnected. It never resumes on its own; the player chooses RESUME.

**5. Never crashing**
- Every serial error is caught. A bad cable or a busy port only means "board not connected", never a crash.

### 2.3 Keyboard and Arduino together: `Input/InputManager.vb`
- This small file decides **who is in control**. If any arrow or WASD key is held, the **keyboard** wins. Otherwise the **board** drives, if it's connected.
- The engine only ever asks InputManager for `TiltX` and `TiltY`, so it never knows which device is in use.
- `Input/ITiltInput.vb` is the "contract": anything that can give a `TiltX` and a `TiltY` can control the game. Both `KeyboardController.vb` and `ArduinoController.vb` follow it.

### 2.4 The board also drives the menus: `UI/Screens/MenuList.vb`
The same tilt numbers move the menus:
- Tilt **toward / away**: move the highlight up and down.
- Hold a **right** tilt for about 1 second: select (a fill bar shows progress).
- Hold a **left** tilt for about 1 second: go back.

The keyboard works the same way, because arrow keys produce the same tilt numbers. Enter and Esc are instant shortcuts.

### 2.5 Calibration ⭐ (the hardware highlight)
Every sensor and every mounting is slightly different. Calibration lets the player fix this **inside the game**, without editing code.

**The CALIBRATE screen** (title menu → CALIBRATE) has these options:

| Option | What it changes in `ArduinoController` | Values |
|---|---|---|
| **RECENTER** | Takes a new "flat" (the 15-reading average) | With the board, it waits until the board is held level for about 1 second |
| **SENSITIVITY** | `FullTiltDegrees`: how far to tilt for full speed | 10°, 15°, **20°** (default), 25°, 30° |
| **DEAD ZONE** | `Deadzone`: how much small tilt is ignored | 0.04, **0.08** (default), 0.12, 0.16 |
| **INVERT X** | Flips left/right | on/off |
| **INVERT Y** | Flips up/down | on/off |
| **SWAP AXES** | Swaps X and Y (for a sensor mounted rotated 90°) | on/off |
| **TEST TILT** | A practice ball in a circle, to try the settings | leave with Enter/Esc, or hold full left for 2 s |

**How the settings flow through the code:**

```
CALIBRATE screen  ─►  CalibrationSettings  ─►  saved in records.xml <Settings>
(GameShell.Players.vb)  (Data/ScoreManager.vb)     and copied to MySQL "settings" table
        │
        └─► ApplyCalibration() copies them onto ArduinoController right away
            (and again every time the game starts)
```

- **Saved:** sensitivity, dead zone, invert X/Y and swap. They describe the hardware, so they belong to the PC, not to a player. Every player uses the same settings.
- **Not saved:** the "flat" position. It's re-taken every session, on connect, with RECENTER, or by pressing **C**, because how the board sits on the table changes every time.
- **Smoothing** (0.35) is saved too, but only editable in `records.xml`, not on the screen.

> **Why not just put the numbers in the code?** Before the calibration screen existed, you had to edit `Form1.vb` and rebuild for every change. Now anyone can tune the board while playing, and the values are remembered.

### 2.6 One Arduino reading, from start to finish
```
Sketch prints "X: 1.70 Y: 0.05 Z: 9.66"
  → ArduinoController reads the line (background thread)
  → converts to tilt ≈ 0.5 sideways (about a 10° tilt), 0.0 up/down,
    using the calibration settings (sign flips, dead zone, smoothing)
  → InputManager: no key held, so use the board
  → GameShell.Tick reads TiltX = 0.5
  → GameEngine.Update(0.5, 0) pushes the ball sideways at half strength
  → MazeRenderer draws the ball in its new spot
```
All of this happens in less than 1/60th of a second.

---

## Part 3: The game engine (how the marble moves)

File: `Engine/GameEngine.vb`. The engine is the **rules of the game**. It has no idea what the screen looks like or where the input comes from.

### 3.1 Physics in 3 lines
Every tick, `Update(tiltX, tiltY)` does:
1. **Acceleration:** `speed = speed + tilt × 0.010`. More tilt means faster speed-up.
2. **Friction:** `speed = speed × 0.90`. The ball slows down by itself, like rolling on wood.
3. **Speed limit:** speed can't go above 0.12 tiles per tick.

Then it moves the ball, **first along X, then along Y**, and checks for walls before each move. If the ball would enter a wall, it stops at the wall instead. That's collision detection. A hard hit creates a "wall impact" (sound, sparks, ¼ heart).

### 3.2 Special floor tiles change the physics
| Tile | Name | Effect |
|---|---|---|
| `I` | Ice | Weak grip (30% steering), very low friction (0.985), higher top speed |
| `F` | Fast | 3× acceleration and double top speed |
| `H` | Hole | Ball falls: lose 1 heart and respawn at the start |
| `L` | Heart | Restores 1 heart (up to 3) |
| `B` | Overdrive barrier | Solid, unless hit at boost speed (from an `F` strip), then it shatters |

### 3.3 Everything else the engine tracks
- **Timer:** elapsed seconds (a stopwatch, no countdown).
- **Hearts:** 3 per attempt, stored as quarters. A wall hit costs ¼, a hole costs 1. After a wall hit you get **1 second** of protection.
- **Goal:** when the ball's centre is close enough to `G`, the level is complete.
- **State:** `Playing`, `LevelComplete`, `OutOfHearts` (and `TimeUp`, unused). See `Engine/GameState.vb`.
- **Counters for the records:** wall hits, falls, hearts lost, barriers broken, attempts.

The engine **announces** things with events such as `WallImpacted`, `BallFell`, `LevelCompleted` and `HeartLost`. The UI listens and plays sounds and effects. The engine itself never plays a sound.

### 3.4 Level 4 (the dungeon)
Level 4, "Forgotten Keep", adds traps (spikes, saw blades, crumbling floor, lava), keys, gates and a collapsing escape. That logic is in `Engine/DungeonRun.vb` and `Engine/Collapse.vb`, and its layout data is in `Mazes/Level4.dungeon.json`. It still uses the same tilt input and the same engine physics.

---

## Part 4: The folders and files

All the game's code is in the `GravityMaze/` folder.

### `GravityMaze/` (the game)
| Folder / file | What it is | If you deleted it... |
|---|---|---|
| `Form1.vb` | **The starting point.** Creates the window, the Arduino reader, the keyboard reader, sound, saving and MySQL, and starts the 60-per-second timer. Also handles F11 (window/fullscreen) and C (recenter). | Nothing starts. |
| `GravityMaze.vbproj` / `.slnx` | Project files that tell Visual Studio how to build and which packages to download. | Visual Studio can't open or build the project. |
| `ApplicationEvents.vb`, `My Project/` | Standard VB.NET startup files Visual Studio creates. | Build errors. |
| **`Input/`** | Reading the Arduino and keyboard (Part 2). | No controls. |
| **`Engine/`** | Game rules and physics (Part 3). | No gameplay. |
| **`Levels/`** | Loads maze files and holds level info (name, par time, stars). | No levels can load. |
| **`Mazes/`** | The maze layouts as text files (see 4.1). | "Could not start" error. |
| **`Rendering/`** | Draws the maze, marble, shadows, 3D tilt and effects. | Nothing visible. |
| **`UI/`** | Screens, menus, HUD, fonts and the drawing canvas. | No menus or screens. |
| **`Data/`** | Saving records (XML) and the MySQL copy (Part 5). | Nothing is saved. |
| **`Audio/`** | Plays the sound effects. | No sound (or build errors). |
| `Sounds/`, `Fonts/`, `Sprites/` | WAV sounds, fonts and pictures (hearts, dungeon art). | The game still runs. It uses plain shapes or no sound where a file is missing. |
| `bin/`, `obj/` | Created by Visual Studio when you build. | Safe to delete; it's rebuilt on the next F5. |

### 4.1 Maze files (`Mazes/Level1.txt` to `Level4.txt`)
Each maze is a grid of characters, one character per tile:

```
11111111111111
1S000001100001      1 = wall      0 = floor
10000001100001      S = start     G = goal
111111011011L1      L = heart     I = ice
...                 F = fast      H = hole
111111111111G1      B = barrier
```

To change a level's layout, edit its text file. No code changes are needed. An unknown character makes the maze refuse to load, with a message naming the row and column. Every edge tile must be `1`, except `S` and `G`.

### 4.2 The most important files to study
| File | One-line summary |
|---|---|
| `Form1.vb` | Sets everything up and runs the timer. |
| `Input/ArduinoController.vb` | Finds the board, reads lines, turns them into tilt, and applies calibration. |
| `Input/InputManager.vb` | Keyboard or board: decides who is in control. |
| `Input/KeyboardController.vb` | Turns held arrow/WASD keys into tilt of -1, 0 or +1. |
| `Engine/GameEngine.vb` | Physics, walls, holes, hearts, timer and goal. |
| `Levels/MazeDefinition.vb` | Reads a maze text file into a grid and checks that it's valid. |
| `Levels/LevelConfig.vb` | Level name, maze file and par time. Stars: 3 within 1.3× par, 2 within 1.8× par, 1 for any finish. |
| `UI/Screens/GameShell.vb` | **The conductor:** which screen is showing, what each tick does, and saving results. |
| `UI/Screens/GameShell.Draw.vb` | Draws every screen (title, HUD, pause, results). |
| `UI/Screens/GameShell.Players.vb` | Player select, profile screen, and the **CALIBRATE screen**. |
| `UI/Screens/MenuList.vb` | A menu that works with tilt (board) and arrow keys. |
| `Rendering/MazeRenderer.vb` | Draws the maze and the marble. |
| `Data/ScoreManager.vb` | Saves and loads players, runs, ghosts and settings in `records.xml`. |
| `Data/MySqlMirror.vb` | Copies the saved data into MySQL. |
| `Data/gravitymaze.sql` | Creates the MySQL database and tables. |

### Outside `GravityMaze/` (supporting files)
| Folder / file | What it is |
|---|---|
| `tools/` | **Automatic tests**, small programs that check the game still works: physics, hearts, camera, transitions, saving and MySQL. `VerificationRunner` also takes the screenshots. The game doesn't need them to run. |
| `docs/` | Design notes for features (marble look, camera zoom, dungeon, and so on), plus the build plans. |
| `screenshots/` | Pictures created by the tests. |
| `Gravity_Maze_PRD.md` | **The requirements**: what the game must do. |
| `Gravity_Maze_AGENTS.md` | Coding rules (for example "the engine must never touch the Arduino"). |
| `SETUP.md` | How to install, run, connect the board and set up MySQL. |
| `Form1.vb` (in the root folder), `extracted/` | Old leftover copies. They are **not** part of the build. |

---

## Part 5: Saving and the database

### 5.1 Two places, one source of truth
| | `records.xml` | MySQL `gravitymaze` database |
|---|---|---|
| **Where** | `%AppData%\GravityMaze\records.xml` (press Win+R, then type `%AppData%\GravityMaze`) | Local MySQL server, viewed in Workbench |
| **Role** | **The main save.** The game reads from it. | **A copy.** Updated after every save. |
| **Needs** | Nothing, it's just a file | MySQL running, plus `mysql.ini` filled in |
| **If missing** | The game starts fresh | The game keeps working; the title shows `MYSQL OFFLINE` |

**Why keep XML?** The PRD lists "XML score storage" as a requirement. XML also means the game works on any PC, even one without MySQL.

### 5.2 What's saved
| Thing | Meaning |
|---|---|
| **Player** | Name, when created, when last played, highest level unlocked, ghost on/off. Names are case-insensitive, so "Jude" = "JUDE". |
| **Run** | **Every attempt**, finished or not: player, level, outcome (`cleared`, `out-of-hearts`, `restart`, `quit`), time, attempts, stars, score, wall hits, falls, hearts lost, barriers broken, date. |
| **Ghost** | The path of the player's best run on each level, replayed as a see-through marble to race against. |
| **Settings** | Last player plus the calibration values (Part 2.5). |

**Score formula:** `stars × 1000 + (3000 − time × 25) − (attempts − 1) × 150`, never below 0.

### 5.3 The MySQL tables (same data as the XML)
| Table | One row per | Key columns |
|---|---|---|
| `players` | player | `playerKey` (name in lowercase), `name`, `unlockedLevel`, `ghost` |
| `runs` | attempt | `id` (1, 2, 3… in order), `player`, `level`, `outcome`, `time`, `stars`, `score`, `wallHits`, `falls`, `heartsLost`, `barriersBroken`, `date` |
| `ghosts` | player + level | `playerKey`, `level`, `time`, `points` (the path) |
| `settings` | (always 1 row) | `LastPlayer`, `FullTiltDegrees`, `Deadzone`, `Smoothing`, `SwapAxes`, `InvertX`, `InvertY` |

**Useful queries** (paste into Workbench, then press ⚡):
```sql
-- my latest 10 runs
SELECT * FROM gravitymaze.runs WHERE playerKey = 'jude' ORDER BY id DESC LIMIT 10;

-- Level 1 leaderboard (fastest clears)
SELECT player, time, stars, score FROM gravitymaze.runs
WHERE outcome = 'cleared' AND level = 1 ORDER BY time LIMIT 10;

-- the saved calibration
SELECT * FROM gravitymaze.settings;
```

### 5.4 How the MySQL copy works (simple version)
1. The game saves `records.xml`.
2. Only **after the save succeeds**, it hands a copy of the data to `MySqlMirror`.
3. `MySqlMirror` sends it to MySQL **in the background**, so the game never freezes waiting for the database.
4. Each time, it replaces the four tables with the full, current data in one step (a "transaction": all of it happens, or none of it). Doing it twice gives the same result, so nothing is ever duplicated.
5. If MySQL is off, it just shows `MYSQL OFFLINE · XML ONLY · <reason>` on the title screen. The next successful save catches everything up, because it always sends everything.
6. The connection details (host, port, database, user, password) are in `%AppData%\GravityMaze\mysql.ini`, **not** in the code, so no password is ever written into the program.

---

## Part 6: What happens when you press F5 (start to finish)

1. **`Form1` starts** and makes the window fullscreen.
2. It creates the **keyboard reader** and the **Arduino reader**. The Arduino reader starts searching COM ports in the background.
3. It loads the **sounds** and the list of **4 levels** (name, maze file, par time).
4. It opens **`records.xml`** through `ScoreManager`. If `mysql.ini` is filled in, it also connects the **MySQL copy** and sends one full copy right away.
5. It creates **`GameShell`**, which applies the saved **calibration** to the Arduino reader and shows the **title screen**. A demo marble plays by itself behind the menu.
6. The **16 ms timer** starts. From now on, every tick runs `GameShell.Tick()`.
7. You pick **PLAY**, then choose or type a player name. A level intro card shows, then **3-2-1-GO**.
8. **Playing:** each tick reads the tilt, calls `engine.Update`, and redraws.
9. You reach the **goal**. The time and stars are worked out, the run is **saved** to XML and copied to MySQL, and the marble drops into the next level.
10. You **close** the game. If you were mid-level, that attempt is saved as `quit`. The game waits up to 2 s for the MySQL copy to finish, and the Arduino port is closed.

---

## Part 7: Handy keys

| Key | What it does |
|---|---|
| Arrow keys / WASD | Tilt (overrides the board while held) |
| Enter / Esc | Select / back, pause |
| **C** | Recenter the board (hold it flat first) |
| R | Restart the level |
| F11 | Window / fullscreen |
| F8 | 3D tilt view on/off |
| F9 | Camera zoom (full maze, 1.5×, 2×). Hold **Tab** to see the whole maze. |
| M | Sound on/off |
| G | Ghost on/off |

---

## Part 8: Likely questions and short answers

**Q: How does the game read the Arduino?**
Over USB serial at 115200 baud. `ArduinoController` reads each line on a background thread, picks out the X and Y numbers, and turns them into a tilt from -1 to 1.

**Q: How does it find the right COM port?**
It tries every port and keeps the first one that sends a valid `X: Y:` line. If nothing is found, it tries again every 2 seconds, so you can plug the board in any time.

**Q: What does calibration do?**
It adapts the game to the real sensor. RECENTER sets "flat". Sensitivity sets how far to tilt for full speed. The dead zone ignores small shakes. Invert and swap fix the direction if the sensor is mounted differently. The settings are saved, except "flat", which is re-taken each session.

**Q: Why is there a dead zone and smoothing?**
Real sensors are noisy and hands shake. Without them, the ball would drift and jitter even when the board is held still.

**Q: What happens if the Arduino is unplugged mid-game?**
After about 1.5 seconds with no data, the game pauses itself and shows a banner. The keyboard still works. Plugging the board back in reconnects it automatically.

**Q: Can you play without the Arduino?**
Yes. The keyboard produces the same tilt numbers, and holding a key always overrides the board.

**Q: How does the ball move?**
Each tick, the tilt adds to the speed, friction slows it down, and a speed limit caps it. The ball moves along X, then along Y, checking for walls before each move.

**Q: Why are the engine and the Arduino code separate?**
So each part can be changed and tested alone. The engine only sees two numbers. It doesn't know or care if they came from the board or the keyboard.

**Q: Where are records stored? Why both XML and MySQL?**
XML (`records.xml`) is the main save and is required by the PRD. MySQL gets an automatic copy after every save, so the data can be viewed and queried in Workbench.

**Q: What if MySQL is off?**
The game keeps working and saving to XML. The title screen shows `MYSQL OFFLINE`, and the next successful save copies everything over.

**Q: How are stars and score worked out?**
Stars compare your time to the level's par time: 3 stars within 1.3× par, 2 within 1.8× par, 1 for any finish. The score adds stars × 1000 plus a time bonus, minus 150 for each extra attempt.

**Q: How do you add or change a level?**
Edit its text file in `Mazes/`. Each character is a tile (`1` wall, `0` floor, `S` start, `G` goal, and so on). No code changes are needed.

**Q: How do you know the game works after a change?**
The `tools/` folder has automatic tests that check physics, hearts, saving, MySQL, transitions and more. They're all run before every change is saved to GitHub.
