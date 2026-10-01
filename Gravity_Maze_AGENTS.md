# Gravity Maze --- AGENTS.md

## Project Purpose

Gravity Maze is an Arduino-powered physics-based maze game developed
using VB.NET Windows Forms.

This document guides developers and AI coding assistants working on the
project.

------------------------------------------------------------------------

# Core Development Rules

## Maintain Separation of Concerns

Do not mix: - Input handling - Physics logic - Rendering - Data storage

Each system must have its own responsibility.

------------------------------------------------------------------------

# Architecture Rules

## Game Engine

Responsible for: - Ball physics - Movement - Collision - Game state

The Game Engine must not directly access Arduino or UI controls.

------------------------------------------------------------------------

## Input Manager

Responsible for player controls.

Supported sources: - Keyboard - Arduino

The Game Engine only receives:

tiltX tiltY

It should not know where input comes from.

------------------------------------------------------------------------

## Renderer

Responsible for: - Drawing maze - Drawing ball - Effects - UI visuals

Do not place gameplay logic inside rendering code.

------------------------------------------------------------------------

# Coding Standards

Classes: - PascalCase

Examples: - GameEngine - MazeManager - ArduinoController

Variables: - camelCase

Examples: - velocityX - currentLevel - ballPosition

------------------------------------------------------------------------

# Maze System

Maze files are external.

Symbols:

1 = Wall 0 = Normal Path S = Start G = Goal I = Ice Zone M = Mud Zone F
= Fast Zone H = Hole (respawn at start)

S and G may sit on the outer edge as entrance/exit openings; every other edge tile must be 1.

------------------------------------------------------------------------

# Physics Rules

Movement must use:

velocity acceleration friction

Avoid direct movement.

Bad: ballX += 5

Preferred:

velocity += acceleration position += velocity

------------------------------------------------------------------------

# Arduino Rules

Arduino sends processed tilt values.

Expected format:

x,y

Example:

0.25,-0.40

The Arduino handles: - Sensor reading - Calibration - Filtering

VB handles: - Gameplay physics - Rendering - Scoring

VB side (`Input/ArduinoController.vb`) is plug-and-play:

-   Auto-detects the board on any COM port at 115200 baud, reconnects if unplugged.
-   Accepts either `x,y` (processed, -1..1) or the raw sketch line `X: <ax> Y: <ay> Z: <az>` (m/s^2); other lines are ignored.
-   Zeroes itself on connect (hold the board level); `C` re-centers any time.
-   Calibration knobs in `Form1.vb`: FullTiltDegrees, Deadzone, Smoothing, SwapAxes, InvertX, InvertY.
-   Keyboard stays active: any held arrow/WASD key overrides the board.
-   Recommended send rate: about 50 lines per second (`delay(20)`).

------------------------------------------------------------------------

# Development Workflow

Build in this order:

1.  Maze rendering
2.  Ball movement
3.  Collision detection
4.  Physics system
5.  Level system
6.  UI polish
7.  Arduino integration
8.  Testing

------------------------------------------------------------------------

# AI Agent Instructions

When modifying this project:

1.  Preserve the existing architecture.
2.  Avoid unnecessary rewrites.
3.  Ask before adding major features.
4.  Prioritize reliability over complexity.
5.  Keep the project within academic scope.
