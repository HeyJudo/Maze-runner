# Gravity Maze: setup and tilt-board testing

This guide takes you from a fresh Windows PC to playing Gravity Maze with the MPU6050 tilt board.
It takes about 30 to 60 minutes, and most of that is installing Visual Studio.

---

## 1. Install the tools

| Tool | What to pick |
|---|---|
| **Visual Studio 2026** (Community is free) | In the installer, tick the **.NET desktop development** workload. You need VS 2026 because the project targets `net10.0-windows` and uses a `.slnx` solution. VS 2022 can't build .NET 10. |
| **.NET 10 SDK** | The VS 2026 workload normally installs it. To check, open a terminal and run `dotnet --list-sdks`. You should see a line starting with `10.0`. If you don't, get the SDK from https://dotnet.microsoft.com/download/dotnet/10.0 |
| **Git** (optional) | https://git-scm.com/download/win (the default options are fine). If you don't want to install it, see "No Git?" in section 2. |
| **Arduino IDE 2.x** | https://www.arduino.cc/en/software |
| **Adafruit MPU6050 library** | In the Arduino IDE, open Library Manager, search **Adafruit MPU6050** and install it. When it asks about dependencies, click **Install all** (it needs Adafruit Unified Sensor and BusIO too). |

## 2. Get the game running (without the board first)

**With Git:**

```
git clone https://github.com/HeyJudo/Maze-runner.git
```

**No Git?** Download the code as a ZIP instead:

1. Go to https://github.com/HeyJudo/Maze-runner
2. Click the green **Code** button, then **Download ZIP**.
3. Right-click the ZIP and choose **Extract All…**. Extract it somewhere simple like `C:\Maze-runner`, not inside OneDrive. The extracted folder is called `Maze-runner-main`, so use that name in place of `Maze-runner` below.
4. If Jude sends a newer version, download and extract a fresh ZIP. Copy your `board = New ArduinoController() ...` line across from the old `Form1.vb` first so you don't lose your settings.

Then:

1. In Visual Studio, use **File → Open → Project/Solution** and open `Maze-runner\GravityMaze\GravityMaze.slnx`.
2. NuGet packages restore on their own. The project's only package is `System.IO.Ports`. If you get restore errors, right-click the solution and choose **Restore NuGet Packages**.
3. Build with **Ctrl+Shift+B**, then run with **F5**.

**What you should see:** a **fullscreen** title screen with the menu PLAY / LEVEL SELECT / RECORDS / HOW TO PLAY / CHANGE PLAYER / QUIT.
The bottom-right footer says `SOUND ON [M] · NO BOARD · KEYBOARD`.

- **F11** switches between fullscreen and a normal window. A window makes recording and switching to VS easier.
- **F8** switches the perspective tilt view on or off. It starts on: the board sits at a slight angle and tilts smoothly with Arduino or keyboard input. For board-only control, pause and select **TILT VIEW: ON/OFF**. This preference lasts for the current game session.
- Arrow keys or WASD move through the menus, and Enter selects. The first time you press PLAY it asks for a player name, which you type on the keyboard.
- In a level, **Esc** pauses. To exit, choose MAIN MENU and then QUIT.
- In **PLAY** (campaign), reaching the goal drops the marble through the exit and automatically reveals the next maze. A brief time/stars overlay accompanies the transition; the next timer starts after the marble lands and the READY cue finishes. The final goal leads to victory. Levels started through **LEVEL SELECT** still show their results menu after the goal drop.

Before you add the board, play a level with the keyboard so you know the game itself works.

## 3. Set up the Arduino

**Wiring (MPU6050 → Uno):**

| MPU6050 | Uno |
|---|---|
| VCC | 5V |
| GND | GND |
| SDA | A4 |
| SCL | A5 |

**Sketch requirements.** The game reads your existing sketch as long as it does these three things:

- uses `Serial.begin(115200)` (any other baud rate won't connect)
- prints one line per reading in exactly this format: `X: <ax> Y: <ay> Z: <az>`, with acceleration in m/s². For example: `X: 0.12 Y: -4.90 Z: 8.49`. Extra text such as `m/s^2` or commas after the numbers will break the parsing.
- ends the loop with `delay(20)`, which gives about 50 readings a second

If you need a starting point, this sketch does all three:

```cpp
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>
#include <Wire.h>

Adafruit_MPU6050 mpu;

void setup() {
  Serial.begin(115200);
  while (!mpu.begin()) { Serial.println("MPU6050 not found"); delay(500); }
  Serial.println("MPU6050 connected!");
}

void loop() {
  sensors_event_t a, g, temp;
  mpu.getEvent(&a, &g, &temp);
  Serial.print("X: "); Serial.print(a.acceleration.x);
  Serial.print(" Y: "); Serial.print(a.acceleration.y);
  Serial.print(" Z: "); Serial.println(a.acceleration.z);
  delay(20);
}
```

1. Upload the sketch. If you like, open the Serial Monitor at 115200 to check that lines are coming in.
2. **Close the Serial Monitor.** While it is open it locks the COM port, and the game can't connect.
3. Start the game with F5. You can plug the board in before or after starting, because the game scans every COM port every couple of seconds.

## 4. Test the controls

1. **Connection:** within about 5 seconds the footer should change from `NO BOARD` to **`BOARD COMx`** (for example `BOARD COM3`). It briefly shows `HOLD LEVEL...` while the game zeroes itself.
2. **Re-center:** hold the board flat and still, then press **C**. The game treats that position as level.
3. **Menus with the board:**
   - Tilt toward or away from you to move the highlight up and down.
   - Hold a **right** tilt for about 1 second to select. A fill bar shows your progress.
   - Hold a **left** tilt for about 1 second to go back.
4. **In a level:** tilt in each of the four directions and confirm the ball rolls the same way. Check the diagonals and small, gentle tilts as well. Level 1 is the easiest to test on. You have 3 hearts: each new wall contact costs **1/4 heart**, while a pit costs **one full heart** and respawns the ball. Wall damage grants 1 second of protection from another wall hit; pits still cost health during this protection. Holding against a wall does not drain health continuously. Floating hearts restore one full heart, capped at 3; the HUD shows partially filled hearts.
5. **Keyboard override:** while the board is connected, hold an arrow key. The keyboard should take control. When you let go, the board takes over again.
6. **Unplug test:** unplug the USB cable mid-game. The footer should go back to `NO BOARD` and the keyboard should still work. Plug it back in, and within a few seconds it reconnects and the footer shows `BOARD COMx` again.

## 5. Troubleshooting

All tuning happens in **`GravityMaze/Form1.vb`**, on the line `board = New ArduinoController()`. Add the settings you need with `With { }`, for example:

```vb
board = New ArduinoController() With {.InvertX = True, .FullTiltDegrees = 25.0F}
```

Then press F5 again. These are the default values:

| Setting | Default | Meaning |
|---|---|---|
| `InvertX` | `False` | Flips left and right |
| `InvertY` | `False` | Flips up and down |
| `SwapAxes` | `False` | Use when the sensor is mounted rotated 90° |
| `FullTiltDegrees` | `20.0F` | How far you tilt the board (in degrees) to reach full speed |
| `Deadzone` | `0.08F` | Small tilts near level that are ignored |
| `Smoothing` | `0.35F` | 0 to 1; a lower value is smoother but responds later |
| `PortName` | auto | Forces one port, e.g. `"COM5"` |

| Problem | Fix |
|---|---|
| Ball goes left when you tilt right | `.InvertX = True` |
| Ball goes up when you tilt down | `.InvertY = True` |
| Tilting forward moves the ball sideways | `.SwapAxes = True`, then re-check the two Invert settings |
| Too twitchy | Increase `FullTiltDegrees` (try 25 to 30) |
| Too sluggish, or you have to tilt a lot | Decrease `FullTiltDegrees` (try 12 to 15) |
| Ball drifts when the board is at rest | Hold the board level and press **C**. If it still drifts, raise `Deadzone` (try 0.12 to 0.15) |
| Feels laggy | Raise `Smoothing` (try 0.5 to 0.7) |
| Footer stays at **NO BOARD** | 1) Close the Serial Monitor (and any other program using the port). 2) Check that the sketch uses `115200` baud. 3) Try a different USB cable, because charge-only cables carry no data. 4) Clone Unos with a **CH340** chip need the CH340 driver: if Device Manager doesn't list the board under **Ports (COM & LPT)**, install the driver. 5) Check that the line format matches section 3. |
| Menus scroll by themselves | Press C while the board is level, and raise `Deadzone` if that doesn't fix it |

> Pressing **C** on the name-entry screen types the letter C. Re-center from any other screen.

## 6. Report back

Send Jude:

1. **The settings you changed**, by copying your `board = New ArduinoController() ...` line from `Form1.vb`.
2. **A short screen recording**, about 30 to 60 seconds. Press F11 for windowed mode and record with Win+Alt+R (Game Bar) or Win+Shift+R (Snipping Tool). Show the footer with `BOARD COMx`, moving through the menus with the board, and the ball rolling in all four directions in Level 1. If you can, film the board in your hand too, on a phone.
3. Anything that felt wrong, such as drift, lag, a direction that's off, or disconnects, and which COM port and board you used (genuine Uno or a CH340 clone).
