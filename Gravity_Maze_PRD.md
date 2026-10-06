# Gravity Maze --- Product Requirements Document (PRD)

Version: 1.0

## 1. Product Overview

Gravity Maze is an Arduino-powered physics-based tilt maze game built
using VB.NET Windows Forms. Players control a virtual metallic marble
through themed mazes using a gyroscope/accelerometer controller.

The goal is to combine hardware interaction, realistic movement, and an
engaging arcade experience.

------------------------------------------------------------------------

## 2. Core Vision

Create a digital version of a physical marble labyrinth where the player
feels like they are controlling a real board.

The project focuses on: - Realistic physics - Responsive controls -
Visual quality - Arduino integration

------------------------------------------------------------------------

## 3. Technology Stack

-   VB.NET Windows Forms
-   Graphics Drawing System
-   Arduino with gyroscope/accelerometer
-   Serial Communication
-   XML score storage
-   Text-based maze files

------------------------------------------------------------------------

## 4. Gameplay

Players guide a metallic marble from start to goal while avoiding walls
and managing momentum.

Core mechanics: - Acceleration - Velocity - Friction - Collision
detection - Goal detection - Timer - Attempts tracking

Health: players start each level/restart with three hearts. Each new wall contact
costs 1/4 heart, followed by one second of protection from wall damage. Sustained
wall contact does not repeatedly drain health. Pits cost one full heart even
during this protection and respawn the ball. Heart pickups restore one full
heart up to the three-heart cap, including when health is only partially missing.
The HUD shows full, 3/4, 1/2, 1/4, and empty hearts. Zero health ends the run.

Campaign progression: reaching a goal stops the level timer and records the
result once. The marble drops through the goal, the completed board lifts and
fades, and the next maze appears beneath it. The marble lands at the next start
with a bounce and pulse, followed by a short READY cue before steering resumes.
Time and stars appear during the transition instead of a blocking results menu.
The final campaign goal leads to victory. Level Select keeps its results menu
after the goal-drop animation. Pause and focus loss freeze the transition.

------------------------------------------------------------------------

## 5. Level Design

### Level 1 --- Wooden Workshop

Theme: Premium wooden labyrinth

Purpose: - Introduce controls - Teach physics

Features: - Large paths - Normal movement - No time pressure

Maze size: - 10x10

------------------------------------------------------------------------

### Level 2 --- Frozen Labyrinth

Theme: Ice environment

Purpose: - Test precision control

Features: - Larger maze - Narrower paths - Ice zones

Special tile: - I = Ice Zone

Effect: - Low grip (tilt steers and brakes weakly), near-frictionless sliding, higher top speed. Open ice rinks with several exits punish overshooting.

Maze size: - 21x21 (1-tile corridors and walls; entrance on left edge, exit on right edge)

------------------------------------------------------------------------

### Level 3 --- Neon Velocity

Theme: Futuristic arcade

Purpose: - Final challenge

Features: - Large maze - Speed zones - Holes - Timer (~1.4x measured par time)

Special tiles: - F = Fast Zone - H = Hole (ball falls and respawns at start; timer keeps running)

Effect: - Increased acceleration

Maze size: - 25x25

------------------------------------------------------------------------

## 6. Visual Design

Style: Premium arcade puzzle game.

Decisions: - Same metallic ball across all levels - Different themes per
level - Slightly angled board with subtle perspective tilt driven by the
resolved Arduino/keyboard input - Stationary HUD and menus

The perspective view is visual only: physics and collision coordinates remain
in tile space. Visual tilt eases toward input during play, holds while paused
or showing results, and returns to its resting pose on level load/restart.
Players can restore the original top-down view with F8 or the pause menu.

Effects: - Ball shadow - Goal animation - Collision feedback - Zone
effects

------------------------------------------------------------------------

## 7. Input System

The game uses an Input Manager.

Supported inputs:

Development: - Keyboard simulation

Final: - Arduino tilt controller

Architecture:

Keyboard / Arduino → Input Manager → Game Engine

------------------------------------------------------------------------

## 8. Arduino Integration

Arduino handles: - Sensor reading - Calibration - Processing tilt
values - Serial output

Example output:

0.25,-0.40

VB receives: - Tilt X - Tilt Y

------------------------------------------------------------------------

## 9. Data Storage

XML stores: - Player name - Completion time - Attempts - Total score -
Best records

The project saves performance history only, not game progress.

------------------------------------------------------------------------

## 10. Software Architecture

Gravity Maze

-   Main Form
-   Game Engine
    -   Physics Engine
    -   Collision System
    -   Timer
    -   Level Controller
-   Input Manager
    -   Keyboard Controller
    -   Arduino Controller
-   Renderer
    -   Maze Renderer
    -   Ball Renderer
    -   Effects
-   Maze Manager
-   Score Manager

------------------------------------------------------------------------

## 11. Team Responsibilities

Software Developer: - Game engine - UI - Physics - Maze system - XML -
Integration

Hardware Developer: - Arduino code - Sensor calibration - Serial
communication

------------------------------------------------------------------------

## 12. Scope

Must Have: - Arduino control - Physics movement - Collision - Three
levels - Themes - Speed zones - Timer - XML records

Bonus: - Sound effects - Particles - Checkpoints - Maze editor
