# Platformer Player Controller: Documentation

A guide to the four scripts that make the player run, jump, climb slopes, slide down steep ones, and stop at walls. It assumes you know basic Unity and C# (components, `Update`, vectors) but not the maths or the design behind this controller.

---

## 1. What the player can do

- **Run** left and right with acceleration, deceleration, and a faster turn-around.
- **Jump** with separate rise and fall gravity, so jumps feel snappy.
- **Walk along slopes** up to a set angle (default 45°), following the surface.
- **Slide down steep slopes** the player can't walk on.
- **Stop at walls** and **bump ceilings**.
- **Keep momentum** when jumping off and landing.
- **Snap to the ground**, so walking down a slope doesn't make the player bounce.

---

## 2. Setting it up

1. Put all four scripts in your project (for example `Assets/Scripts/`):
   - `player.cs`
   - `PlayerSettings.cs`
   - `SurfaceClassifier.cs`
   - `SurfaceProbe.cs`
2. Keep `player.cs` named exactly that. Unity needs a MonoBehaviour's file name to match its class name (`player`).
3. Create a GameObject and add the `player` script. It adds a **BoxCollider** automatically.
4. **Do not add a Rigidbody.** The script moves the transform itself.
5. Give your ground, walls and ramps **colliders**.
6. In the player's Inspector, open **Surface** and set **Ground Mask** to the layers that count as ground and walls. A good habit is to put the player on its own layer and leave that layer out of the mask.
7. Make sure your project uses the **old Input Manager** with the default `Horizontal` and `Jump` inputs. If it uses the new Input System, change the two methods at the bottom of `player.cs` (see section 7).

**Good to know:** the game is treated as 2D-style, with movement on the X (left/right) and Y (up/down) axes. Z isn't used.

---

## 3. Concepts you need first

### Surface normal

A **normal** is an arrow pointing straight out of a surface.

| Surface | Normal points |
| --- | --- |
| Flat floor | straight up `(0, 1, 0)` |
| Slope rising to the right | up and to the left |
| Vertical wall | sideways |
| Ceiling | straight down |

### Dot product (how we tell ground from walls)

`Vector3.Dot(a, b)` compares the directions of two arrows of length 1:

| Result | Meaning |
| --- | --- |
| `1` | same direction |
| `0` | at right angles |
| `-1` | opposite directions |

We compare a surface's normal with `Vector3.up`:

| Surface | `Dot(normal, up)` |
| --- | --- |
| Flat floor | 1.0 |
| 45° slope | about 0.71 |
| Wall | 0 |
| Ceiling | -1 |

So one number tells us what kind of surface we hit. It's also cheaper than converting to degrees.

### Raycast

A **raycast** shoots an invisible line from a point in a direction and tells you what it hit, how far away, and the surface normal. The controller uses rays for everything it senses. A ray starting *inside* a collider ignores that collider, which is why the player's rays don't hit the player.

### Velocity and delta time

Each frame the player moves by `velocity * Time.deltaTime`. `Time.deltaTime` (called `dt` in the code) is the number of seconds the last frame took. Doing it this way keeps speed the same on fast and slow computers.

### Tangent and downhill

- **Tangent**: the direction *along* a surface, pointing right. On flat ground it's `(1, 0)`. On a slope it tilts up or down to match.
- **Downhill**: the direction *along* a surface that points downward. It's used for sliding.

### Snapping and skin

After moving, the script puts the player's feet exactly on the surface. This is **snapping**, and it stops the player floating above or sinking into the ground. A tiny gap, the **skin** (default 0.02 units), is left so the collider doesn't sit *inside* the surface.

### Modes (a simple state machine)

At any moment the player is in exactly one of three modes:

| Mode | Meaning |
| --- | --- |
| **Grounded** | standing or walking on flat ground or a walkable slope |
| **Sliding** | on a slope too steep to walk on |
| **Airborne** | jumping or falling |

Each mode has its own small function that decides how the player moves. This keeps the code readable.

---

## 4. How one frame works

Everything happens in `player.Update()`, in this order:

```
1. SenseGround()    Shoot rays down. What are we standing on?
2. ModeFor()        Pick Grounded, Sliding or Airborne from the surface type.
3. ChangeMode()     If the mode changed, convert speeds so momentum isn't lost.
4. Jump check       Only allowed when Grounded.
5. Tick<Mode>()     Work out this frame's velocity for the current mode.
6. Move()
     a. BlockAtWalls()     Shorten the move if a wall is in the way.
     b. BlockAtCeiling()   Shorten the move if a ceiling is in the way.
     c. Apply the move to transform.position.
     d. SnapToGround()     Put the feet exactly on the surface.
```

### How momentum is kept

The script uses two kinds of speed:

- **`surfaceSpeed`**: how fast you move *along* the ground or slope (used when Grounded or Sliding).
- **`velocity.x`**: how fast you move left/right (used in the air).

When the mode changes, `ChangeMode()` converts between them. Example: running at `surfaceSpeed = 8` up a 45° slope is only about 5.7 units/second *sideways* (8 × 0.71). Jump, and you keep that 5.7 sideways speed. Land on flat ground and you start at 5.7, then speed back up to 8 if you hold the key. Nothing stops dead.

---

## 5. The files

### 5.1 `PlayerSettings.cs`: all your tunable numbers

Four small `[Serializable]` classes, so they appear as foldouts in the Inspector. Some values are *derived* (worked out from the others), so you set intuitive numbers like "time to reach the top of the jump" and the script calculates gravity.

| Class | Derived values (read-only) | Formula |
| --- | --- | --- |
| `JumpSettings` | `RiseGravity` | `2 × height / timeToApex²` |
|  | `FallGravity` | `2 × height / timeToFall²` |
|  | `JumpVelocity` | `2 × height / timeToApex` |
| `RunSettings` | `Acceleration` | `maxSpeed / timeToReachMax` |
|  | `Deceleration` | `maxSpeed / timeToStop` |
|  | `TurnRate` | `maxSpeed × turnSpeedScalar / turnSpeed` |
|  | `AirAcceleration` | `maxSpeedOnAir / timeToReachMax` |
| `SlideSettings` | `Acceleration` | `maxSpeed / timeToReachMax` |
| `SurfaceSettings` | `FlatDot`, `WalkableDot`, `WallDot` | angles converted to dot-product values |

### 5.2 `SurfaceClassifier.cs`: "what kind of surface is this?"

**`SurfaceType`** (an enum) lists the possibilities: `None`, `Ground`, `WalkableSlope`, `UnwalkableSlope`, `Wall`, `Ceiling`.

**`SurfaceClassifier.Classify(normal)`** takes a surface normal and returns a `SurfaceType`. It checks in this order:

| Order | Test | Result |
| --- | --- | --- |
| 1 | dot is close to 0 (within `wallAngleTolerance` of vertical) | Wall |
| 2 | dot is below 0 | Ceiling |
| 3 | angle ≤ `flatAngle` | Ground |
| 4 | angle ≤ `maxWalkableAngle` | WalkableSlope |
| 5 | anything else | UnwalkableSlope |

With the default settings:

| Angle from flat | Type |
| --- | --- |
| 0° to 1° | Ground |
| 1° to 45° | WalkableSlope |
| 45° to 80° | UnwalkableSlope |
| 80° to 100° | Wall |
| over 100° | Ceiling |

**`SurfaceMath`** has two helpers, `Tangent(normal)` and `Downhill(normal)`, described in section 3.

### 5.3 `SurfaceProbe.cs`: all the raycasting

This file has three parts. **None of them know anything about player movement.** They only report what's around the box.

**`SurfaceHit`** is a small package describing one hit: `Type`, `Normal`, `Point`, `Distance`, and `IsHit` (true if anything was hit).

**`BodyDimensions`** measures the BoxCollider once (half width, half height, centre offset) and stores it. It's calculated at startup, so scaling the player while the game runs won't update it.

**`SurfaceProbe`** has three questions you can ask it:

| Method | Question it answers | How |
| --- | --- | --- |
| `Ground(extraDistance)` | What is under my feet? | 3 rays down (left, centre, right). The **highest** supporting hit wins, so the box rests on its highest corner instead of clipping into slopes. Counts Ground, WalkableSlope and UnwalkableSlope. |
| `SideClearance(dirX, travel)` | How far can I move sideways before hitting something? | 3 rays sideways (near feet, middle, near head). Blocked by Wall, Ceiling and UnwalkableSlope. **WalkableSlopes don't block**, because you're meant to walk up them. |
| `UpClearance(travel)` | How far can I move up before bumping my head? | 3 rays up. Blocked by Ceiling and Wall. |

### 5.4 `player.cs`: the controller

This is the only MonoBehaviour. It owns the player's state and calls the helpers above.

**Fields**

| Field | Purpose |
| --- | --- |
| `mode` | current mode (Airborne, Grounded, Sliding) |
| `velocity` | the player's real movement this frame |
| `surfaceSpeed` | speed along the surface (see "How momentum is kept") |
| `probe` | the `SurfaceProbe` used for all raycasts |
| `feetOffsetY` | distance from the transform's position down to the bottom of the box |

**Methods**

| Method | What it does |
| --- | --- |
| `Awake()` | measures the box and builds the probe and classifier |
| `Update()` | runs the frame steps from section 4 |
| `ModeFor(ground)` | the switch on surface type that picks the mode. Ground and WalkableSlope give Grounded, UnwalkableSlope gives Sliding, anything else gives Airborne |
| `ChangeMode()` | runs once when the mode changes and converts speeds (see below) |
| `StartJump()` | switches to Airborne and sets upward velocity |
| `TickGrounded()` | speeds up or slows down along the surface, then moves along the tangent |
| `TickSliding()` | speeds up downhill (no player control) |
| `TickAirborne()` | applies gravity and air steering |
| `GroundRate()` | picks acceleration, deceleration or turn rate |
| `SteerInAir()` | air control that can add speed but never takes away speed you already have |
| `Move()` | applies a move with wall and ceiling checks, then snaps |
| `BlockAtWalls()` / `BlockAtCeiling()` | shorten a move to fit the available space and zero the speed that hit something |
| `SnapToGround()` | places the feet on the surface |
| `SenseGround()` | asks the probe what's below (skipped while rising in a jump) |
| `ReadMoveInput()` / `JumpPressed()` | the only places that read input |

**What `ChangeMode()` converts**

| Entering | What it does |
| --- | --- |
| Grounded | `surfaceSpeed = velocity.x / tangent.x`, so your sideways speed is kept |
| Sliding | `surfaceSpeed` = the part of your speed already pointing downhill (never negative) |
| Airborne (from walking) | upward speed is cleared, so walking off the top of a slope doesn't launch you |

**Two small details**

- **Jump skipping the ground check:** while rising in the air, the ground check is skipped. Without this, the ray would still see the ground right after you jump and cancel the jump.
- **Snap distance:** while on the ground, the snap distance is large (default 0.3) so you stay stuck to slopes. In the air it's tiny (`groundSkin × 2`), so you land naturally instead of being pulled down from above.

---

## 6. Inspector reference

### Jump

| Setting | Default | Effect |
| --- | --- | --- |
| Time To Apex | 0.35 | seconds to reach the top. Lower means a snappier jump |
| Time To Fall | 0.25 | seconds to fall the same height. Lower means a heavier fall |
| Jump Height | 3 | height of the jump in units |

### Run

| Setting | Default | Effect |
| --- | --- | --- |
| Max Speed | 8 | top running speed |
| Max Speed On Air | 4 | the limit for *steering* in the air. It never slows you down if you're already faster |
| Time To Reach Max | 0.3 | seconds to reach top speed |
| Time To Stop | 0.1 | seconds to stop after letting go. Lower means a faster stop |
| Turn Speed | 0.5 | acts like a time. **Lower** means a snappier turn-around |
| Turn Speed Scalar | 2 | **higher** means a snappier turn-around |

### Slide

| Setting | Default | Effect |
| --- | --- | --- |
| Max Speed | 10 | top sliding speed |
| Time To Reach Max | 0.5 | seconds to reach it |

### Surface

| Setting | Default | Effect |
| --- | --- | --- |
| Ground Mask | Everything | which layers count as ground, walls and ceilings |
| Flat Angle | 1° | surfaces this flat count as Ground |
| Max Walkable Angle | 45° | steepest slope you can walk on |
| Wall Angle Tolerance | 10° | how far from vertical still counts as a Wall |
| Ground Snap Distance | 0.3 | how far below the feet we still stick to the ground |
| Ground Skin | 0.02 | small gap between the feet and the ground |
| Wall Skin | 0.02 | small gap between the box and walls |

---

## 7. Common changes

**Make slopes steeper but still walkable:** raise *Max Walkable Angle*. Keep it below 80°, because walls begin there.

**Make sliding faster or slower:** change the Slide *Max Speed* and *Time To Reach Max*.

**Let the player steer while sliding:** in `TickSliding()`, read `ReadMoveInput()` and add a small sideways amount to `velocity` before calling `Move()`.

**Allow jumping while sliding:** in `Update()`, change `mode == MoveMode.Grounded && JumpPressed()` to also allow `MoveMode.Sliding`.

**Use the new Input System:** replace the bodies of `ReadMoveInput()` and `JumpPressed()` at the bottom of `player.cs`. Nothing else reads input.

**Add a new surface or mode (for example ice):**

1. Add a value to the `SurfaceType` enum.
2. Add the test for it in `SurfaceClassifier.Classify()`.
3. Add it to the `MoveMode` enum and the `ModeFor()` switch.
4. Write a `Tick...()` method and add a case for it in the `Update()` switch.

---

## 8. Troubleshooting

| Problem | Likely cause and fix |
| --- | --- |
| Player falls through the floor | The floor has no collider, or its layer isn't in *Ground Mask* |
| Player floats slightly above the ground | Normal. That's *Ground Skin*. Lower it if you want it smaller |
| Player can't walk up a slope | The slope is steeper than *Max Walkable Angle*. Or it's a rounded collider whose normals change across its surface |
| Player slides on a slope you wanted walkable | Raise *Max Walkable Angle* |
| Player gets stuck on tiny bumps | Walls block at foot height and there's no step-up. Smooth the geometry, or add step-up logic in `BlockAtWalls()` |
| Player pops or hops when going down slopes | *Ground Snap Distance* is too small. Raise it a little |
| Player sticks to the ground when running off a ledge | *Ground Snap Distance* is too large. Lower it |
| Jump does nothing | The player isn't Grounded (check *Ground Mask*), or the project has no `Jump` input |
| Player ignores walls | The wall has no collider, isn't in *Ground Mask*, or isn't steep enough to count as a Wall (80° or more) |
| Nothing happens at all | Make sure there's no Rigidbody on the player and that the script's file name is `player.cs` |

---

## 9. Known limits

- **Tall stairs and steps** block the player. There's no step-up.
- **Very thin floors** can be fallen through at extreme fall speeds, because the ground check only reaches about half the player's height plus the snap distance. If you need very high fall speeds, add a maximum fall speed.
- **Rotating or scaling the player** at runtime isn't supported. The box size is measured once at startup.
- **Moving platforms** aren't handled.
- **Rounded colliders** (spheres, capsules) have constantly changing normals, so the player can change between walking and sliding on them. Flat ramps behave best.
- The game is treated as **2D-style on the X and Y axes**.