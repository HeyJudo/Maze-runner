# Gravity Maze — Step 1: Draw the first maze

This package adds the first rendering milestone to the blank Visual Basic Windows Forms project you already created. It does not contain a replacement solution or project file.

## What this step does

- Loads a 10 × 10 Wooden Workshop maze from `Mazes/Level1.txt`.
- Draws wood-colored walls and paths, a green goal ring, and a shaded metallic marble at the start.
- Fits the board to the available window area, including when the window is resized.
- Separates level data, loading, rendering, and the Windows Forms drawing surface.

The marble is stationary in this milestone. Keyboard input, movement, physics, collision handling, other levels, scoring, and Arduino integration are later steps in the agreed development workflow. The drawing code does not move the marble or read input.

## Add the files to your existing project

1. In Visual Studio, stop the application if it is running and use **File → Save All**.
2. Click the **Solution Explorer** tab at the lower right. If it is hidden, use **View → Solution Explorer**.
3. Right-click the **GravityMaze project** and choose **Open Folder in File Explorer**. Locate the folder containing `GravityMaze.vbproj` and `Form1.vb`. This is the destination for the code below.
4. Close Visual Studio so it will not keep an old copy of `Form1.vb` open.
5. Download and extract this ZIP to a temporary folder **outside your project folder**. Do not put the entire extracted package under the project: Visual Studio can compile duplicate copies of `.vb` files in subfolders.
6. Before replacing the blank starter file, copy your current `Form1.vb` somewhere **outside the project folder** as a backup.
7. Copy these five items from this package into the folder containing `GravityMaze.vbproj`: the **Levels**, **Rendering**, **UI**, and **Mazes** folders, plus **Form1.vb**. Replace the existing blank `Form1.vb` when asked. Keep your existing `Form1.Designer.vb`, `Form1.resx` (if present), `.vbproj`, and solution files.
8. Open your existing `GravityMaze.slnx` or `GravityMaze.sln` again. You can also open `GravityMaze.vbproj`. An SDK-style project includes the added `.vb` source files automatically.
9. In Solution Explorer, expand **Mazes**, click **Level1.txt**, and press **F4** to open its Properties window. Set these values:

| Property | Value |
| --- | --- |
| Build Action | Content |
| Copy to Output Directory | Copy if newer |

10. Use **File → Save All**, then **Build → Build Solution** (`Ctrl+Shift+B`).
11. Press **F5** to run the application. It should open maximized with the wooden maze, metallic marble, green goal, and level heading.
12. Restore the running window and resize it. The board should stay centered and keep square tiles. The marble should stay at the start; no movement has been added yet.

Your form designer can still look blank. This step creates its controls in the `Form1_Load` event, so the board appears when you run the app.

## Where each responsibility lives

| File | Responsibility |
| --- | --- |
| `Form1.vb` | Sets up the window, creates the drawing surface, and requests the level. |
| `Levels/MazeDefinition.vb` | Stores grid dimensions and start/goal positions; checks the level's structure. |
| `Levels/MazeManager.vb` | Reads the external text file. |
| `UI/GameCanvas.vb` | Provides a buffered drawing surface and redraws when resized. |
| `Rendering/MazeRenderer.vb` | Draws the board, goal, and starter marble without changing game state. |
| `Mazes/Level1.txt` | Stores the actual level layout. |

Classes use the `GravityMaze` root namespace from your project. The code uses explicit types and `Option Strict On`. Drawing resources are disposed with `Using`, and screen dimensions are calculated from the loaded grid rather than hard-coded to 10 × 10.

Keep the existing project PRD and AGENTS.md as the project references. This milestone follows their first development step: maze rendering.

## Understanding the level file

Each character is one square tile. Rows must have equal length, the outside edge must be walls, and there must be exactly one `S` and one `G`.

| Symbol | Meaning |
| --- | --- |
| `1` | Wall |
| `0` | Normal path |
| `S` | Start |
| `G` | Goal |
| `I` | Ice; reserved for later level behavior |
| `F` | Fast zone; reserved for later level behavior |
| `M` | Mud symbol from the existing AGENTS.md; not used in this level or implemented as gameplay |

The loader checks the format, not reachability. The supplied level's start-to-goal route was checked separately. Recheck reachability if you edit the map.

## If something does not work

- **A message says the level file cannot be found:** Check the file is at `Mazes/Level1.txt` inside the project and that its Copy to Output Directory setting is **Copy if newer**. Rebuild.
- **Only the original blank Form1 appears:** Confirm that the supplied `Form1.vb` replaced the file beside `GravityMaze.vbproj`, then save, rebuild, and run.
- **Duplicate class or method errors:** Move any extracted package or backup `.vb` copies out of the project directory. Keep only one set of the supplied source files inside the project.
- **A GravityMaze namespace cannot be found:** In project properties, check that the root namespace is `GravityMaze`, matching the name used during project creation.
- **A maze format error appears:** Restore the supplied level file or correct its row lengths, boundary walls, symbols, and start/goal count.
- **The designer displays a 150% scaling notice:** The renderer calculates the board from the running control's size. Run the app and check the resize behavior rather than judging the runtime board from the designer notice.

## References

- [Microsoft: custom painting in Windows Forms](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/custom-painting-drawing)
- [Microsoft: double buffering for forms and controls](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/how-to-reduce-graphics-flicker-with-double-buffering-for-forms-and-controls)

## Verification

The supplied VB.NET source was compiled against .NET 10 Windows Forms with `Option Strict On` and a minimal blank Form1 designer: **0 warnings, 0 errors**. The supplied 10 × 10 level was checked for valid symbols, enclosed boundaries, exactly one start and goal, and a reachable goal. This environment cannot run the Windows Forms window, so the F5 and resize checks above are still required on your Windows computer. This package has not been tested against your actual local project files, which were not uploaded.

The next milestone adds a common tilt-input interface and keyboard input, then uses acceleration and velocity for marble movement. Arduino can later supply the same input values.
