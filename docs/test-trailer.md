# Test Trailer – a box on wheels behind any car, to learn how a trailer behaves

The spec of a test mod, asked for by the owner on 22 November 2026 together with another modder who builds trailers
for the game. Their first try failed four ways: the trailer fell through the ground, spun "like a fan", held the car
like an anchor, and rolled the car over in every turn at any speed. This mod is a test bench, not the trailer players
get: a plain box on wheels in three sizes, switched in the game, with numbers in the log, to find a setup the game's
physics carries – then the real trailers (the owner's pictures: a box trailer like the Pike 1902, a folding camper like
the Wilde Adventurer, a hard shell with a pop-top like the Decamp Cross X5, the Sasquatch XC) are built on it.

One branch (`feat/test-trailer`), stage by stage, each stage played by the owner before the next one starts. Nothing
here is checked in the game yet.

## 1. What the game gives

Read offline from the game of 13 November 2026 (`decomp/`, `game/probe/`), with what still has to be read in the game
marked so.

**Every car has a hitch point with coordinates.** The "tow bar" the first try looked for is only a cosmetic mesh
(`Parts/<Car>_CosmeticParts/Visual/Rear_Bumper/TowBar`, an upgrade, off by default, its LODs under it): no transform at
the ball. But every car carries the winch's rear attach point, a transform with a sphere collider:
`Winch/WinchAttachPoints/Rear`. In car space, from the prefabs in `game/probe/`:

| Car | Rear point (x, y, z) m | Car | Rear point (x, y, z) m |
|---|---|---|---|
| Protector | 0, 0.486, −1.870 | Brick | 0, 0.508, −1.768 |
| Tinny | 0, 0.410, −1.694 | DingDing | 0, 0.619, −1.840 |
| Wombat | 0, 0.559, −1.962 | Burro | 0, 0.715, −2.976 |
| Mammoth | 0, 0.935, −2.238 | BlastWagon | 0, 0.688, −2.783 |
| Cumbre | 0, 0.559, −2.776 | Marshal | 0, 0.731, −2.050 |
| Boar | 0, 0.577, −2.024 | Bull | 0, 0.533, −1.930 |
| GrandWoodman | 0, 0.434, −2.579 | Woodman | 0, 0.410, −2.583 |
| Lynx | 0, 0.549, −1.518 | Polyana | 0, 0.537, −1.705 |
| Roamer | 0, 0.531, −2.588 | RustOx | 0, 0.431, −2.460 |
| Timberbox | 0, 0.681, −2.383 | TrailBurner | 0, 0.559, −2.300 |
| Trailrat | 0, 0.512, −1.990 | Grizzly | 0, −0.021, −1.351 (look) |

The hitch is that point, moved back by the tow bar's reach when the tow bar is fitted (its mesh's bounds, read offline
in stage 1). The long cars of the other mods move this point with their body (to check per car in stage 1).

**The car's body.** The car's root has the `Rigidbody` (the Protector: 1600 kg, centre of mass at (0, 0.45, −0.03), a
`CoM` node), the `NetworkObject`, Vehicle Physics Pro's `VPVehicleController`. The class sets the mass (the Wagon class
2000 kg). VPP re-applies the inertia every step (`Inertia.DoUpdate`, game-engine.md). The hitch points sit near the
centre of mass's height (0.41–0.73 m against 0.45 m): a horizontal pull there tilts the car little – a trailer that rolls
the car over in a turn is held too stiffly, not hitched too high (section 3).

**Joints on the car work in this game.** The game hangs its own rigid bodies on the car with `ConfigurableJoint`s: the
camping lantern, the pan, the whip lights (`dump_Car_Protector(Clone).txt`). A jointed body behind the car is the
game's own way, not a foreign one.

**The winch is not a joint.** `VehicleWinchSystem` pulls cars and movable objects with its own rope solver in
`FixedUpdateNetwork` (`ApplyAlongRope`, `InverseMassAlong`, the tension stored): a rope, slack or taut, not a rigid
hitch. It shows two things: the game moves bodies in Fusion's network step, and a car can be pulled by a body that is
not a car. Its code is the model for the safety limits below, not for the hitch.

**The step.** Fusion runs at 50 ticks a second (NetDiag: `tick 50/s`); physics is stepped by Fusion's physics add-on
(`Il2CppFusion.Addons.Physics`, a `ClientPhysicsSimulation` mode). Which mode the game uses, and whether a client's game
steps physics at all, is read in stage 0. Every body in the scene, the mod's included, is stepped with the cars.

**Who runs the car.** The host runs every car's physics, the clients' too (`VehicleBase.FixedUpdateNetwork` with the
player's input, game-engine.md, Multiplayer). So a trailer that pulls on a car must pull on the host's copy of it.

**No networked objects of a mod's own.** Fusion spawns only prefabs in its table, built with the game: a mod cannot
spawn a networked trailer. The trailer is a plain Unity object each game makes for itself, and what has to be shared
(which car tows which size) goes through the Photon room's custom properties – never a hook or Fusion's reliable data
(game-engine.md, Multiplayer: UnfoldingTent 0.2.0 crashed the game that way, 20.11.2026).

## 2. The four failures and what each one was

From the symptoms; the first try's code would confirm them (worth a look if the other modder shares it).

| Symptom | Usual cause in Unity's physics | The bench's rule |
|---|---|---|
| Falls through the ground | colliders on a layer that does not collide with the terrain, or created inside it; wheel colliders with no suspension room; a body made when physics is stepped by script and never stepped | the car's own layer (Car, 7, which meets the terrain), made above the ground at a raycast height, its wheels' rest within their travel |
| Spins like a fan | the trailer's colliders inside the car's (the drawbar in the bumper): the solver pushes them apart every step; or the joint's anchors apart at its creation (auto-configured anchor), snapped together at once; or an inertia tensor from a thin collider | collisions between the trailer and its car ignored pair by pair; anchors set by hand and the trailer placed so they meet before the joint is made; the inertia set from the box's size; `maxDepenetrationVelocity` low |
| An anchor: the car does not move | a body on the joint that is kinematic or frozen (infinite mass), a body or drawbar dragging on the ground, braked wheels, or a mass far above the car's | always dynamic, nothing but the wheels under the floor, `brakeTorque` 0 and `motorTorque` a hair above 0 so the wheels roll, the mass 20–90 % of the car's |
| The car rolls over in every turn | angular axes locked (a fixed joint or locked angles): the trailer's turn and roll are forced into the car | a ball joint: positions locked, the three angles free within limits (jackknife ±90°, pitch ±30°, roll ±25°) |

## 3. The trailer

**Sizes**, switched by a key, from the owner's pictures:

| Preset | Body L × W × H | Axles | Mass | Like |
|---|---|---|---|---|
| S | 2.4 × 1.4 × 0.9 m | 1 | 350 kg | a small box trailer |
| M | 3.6 × 1.8 × 1.6 m | 1 | 800 kg | the Pike 1902, the Sasquatch XC |
| L | 4.6 × 2.0 × 2.2 m | 2 (tandem) | 1400 kg | the Decamp Cross X5, the Wilde Adventurer |

The drawbar is 1.2 m (S) to 1.5 m (L) from the body's front to the coupling. The axle (the tandem's middle) at 58 % of
the body's length from its front, so 8–12 % of the mass rests on the hitch (tongue weight): a trailer with its mass
behind the axle sways and lifts the car's rear.

**Body.** One `Rigidbody` on the car's layer: the preset's mass, centre of mass set by hand low (a third of the body's
height over the floor) and ahead of the axle by the tongue weight's share; inertia tensor from the box's size
(`m/12·(h²+l²)` and so on), set by hand; interpolation on; `solverIterations` 12, `solverVelocityIterations` 4;
`maxDepenetrationVelocity` 1 m/s; `maxAngularVelocity` 7. Collider: one box for the body, its floor 0.45 m over the
ground (nothing drags), the drawbar without a collider. `Physics.IgnoreCollision` with every collider of its car.

**Wheels.** Unity's `WheelCollider`, two or four, radius 0.35 m (S) to 0.4 m (L), 25 kg each; suspension travel 0.2 m;
spring = load per wheel / (half the travel) (M: 800·9.81/2/0.1 ≈ 39 000 N/m), damper for a damping ratio of 0.4;
target 0.5; `brakeTorque` 0, `motorTorque` 1e-4 (a wheel with no torque at all locks up in PhysX's sleep);
sideways friction the stock curve (this keeps the trailer behind the car), forward friction stiffness 0.5;
`ConfigureVehicleSubsteps(5, 12, 15)` on the first wheel. If the wheel colliders misbehave in the game's step (stage 2
says), the fallback is the bench's own raycast wheels: a spring and a damper per wheel and a sideways friction impulse,
the way VPP's are built.

**Hitch.** A `ConfigurableJoint` on the trailer: `connectedBody` the car's rigid body; `anchor` the coupling in the
trailer's space, `autoConfigureConnectedAnchor` off and `connectedAnchor` the hitch point in the car's space; x, y, z
motion locked; angular x (pitch) limited ±30°, y (yaw) ±90°, z (roll) ±25°, the limits soft (a spring and a bounce of
0); `projectionMode` position and rotation (0.05 m, 5°) so the coupling never drifts apart; `enableCollision` off;
`connectedMassScale` so the car counts at most four times the trailer in the solver (a heavy car and a light trailer
jitter otherwise); no break force – the bench's own safety detaches instead.

**Making it.** Only with the car standing still and upright, behind the car on the ground: the yaw the car's, each
wheel's contact found by a raycast down, the body set so the coupling sits exactly on the hitch point, then the joint.
When there is no room (the camp's overlap check, as UnfoldingTent's), it says so on the screen and makes nothing.

**Safety.** It detaches itself, with a line on the screen and in the log, when the joint's force passes 8 times the
trailer's weight for half a second, the jackknife passes 100°, the trailer turns over (more than 60° off upright) or the
car does, or the trailer falls more than 2 m below its car – the four failures above end there instead of in a spin.

## 4. Controls and numbers

- `N`: hitch a trailer, or drop it; `Shift+N`: the next size (S → M → L); while driving only, the car standing to
  hitch. N is bound to nothing in the game's input actions (read from the public beta's cars bundle on 19.11.2026: the
  free letters were B, J, N, U, X; T is the game's, J Paired Planks', U the tent's) – checked again in stage 0.
- On the screen, a line under the car's HUD: the size, the hitch force in kN, the angle between car and trailer, the
  trailer's roll, each wheel on the ground or not.
- In the log every second while towing, and at every event: speed, the car's roll and pitch, the trailer's yaw, roll and
  sway (the yaw's swing over the last second), the hitch force, the wheels' contacts, frame cost. A run's summary when it
  ends: the largest of each.

## 5. Stages

0. **The probe** (one solo session and one co-op, no trailer). Log: the physics add-on's `ClientPhysicsSimulation`,
   `Physics.simulationMode`, the fixed step, Fusion's tick; on the player's car its rigid body (mass, centre of mass,
   inertia, interpolation, solver iterations, layer) and the rear point's world position; the layer matrix's row for
   the car's layer; on a client, whether its own car's rigid body is kinematic and whether the game's whip lights swing
   (a client that steps physics). The keys (N, Shift+N) still free in the game's input actions. Answers: can a client's game run
   a trailer at all, and on whose physics.
1. **The box at the hitch, no physics.** The trailer drawn where it would hang, carried as the car's child, each size on
   each car: offline pictures of all 22 cars and the long ones, then in the game. Checks the hitch point per car (the tow
   bar's reach, the long cars), the wheels' height, the look.
2. **The trailer, solo.** The body, the wheels, the joint, the safety, the keys, the numbers. The owner drives each
   size: straight at 30 and 60 km/h, a slalom, a 90° turn at 20 and 40 km/h, a steep climb and descent, an off-camber
   slope, reversing, a jackknife on purpose. Pass: no fall through, no spin, the car drives with each size, no roll-over
   in turns a car alone takes; the log's numbers per case into a table here.
3. **Tuning.** From stage 2's numbers: masses, tongue weight, limits, the wheels' spring and friction, the mass scale,
   the raycast wheels if the wheel colliders failed. Each change one at a time, played.
4. **Co-op.** Which car tows which size goes into the Photon room (`trailer.<player>` = "1|size"), every game makes the
   trailer behind that car. On the host it is the real one: its physics pulls the car, the client's too, since the host
   runs it. On a client by stage 0's answer: the same body if the client steps physics and predicts its car, else a
   follower (the trailer's yaw from the hitch's path, no forces). Played with the host and a client both towing.
5. **After the bench.** The models from the pictures, a trailer bought and kept (a save and the shop are the hard part:
   the game's catalog and save hold cars only, see the long cars' ItemIds), the camping fold-outs like the tent's.

## 6. Where the code comes from

A new mod, `src/TestTrailer/`, adding no car. From UnfoldingTent (`feat/unfolding-tent`), copied, not shared: the cars'
tracking from `VehicleBase.Spawned` with the wreck guard first (`Mod.cs`, `Tents.Track`), the key while driving and the
game's notification (`Pitch.cs`), the overlap check before making it (`Tent.Clear`), the Photon room for co-op
(`Net.cs`), the box and quad builders of `Shape.cs` for the body's mesh. Nothing of the tent's look.

## 7. Stage 0 and 1 build (0.0.1)

`src/TestTrailer`, one DLL, nothing but logs and a drawn box: no rigid body, no collider, no joint, nothing sent, no
Harmony patch but the `VehicleBase.Spawned` prefix that notes the cars (the wreck guard first: a `Broken_Car_*` name,
or a `BrokenCarBehaviour` component on the car). This player's car is the one whose `NetworkObject.InputAuthority` is
`OverlandNetworkRunner.LocalPlayerRef`, looked for three times a second, and not at all while no car is spawned (the
menus). A step that throws is paused for 5 s and logged; after 5 failures it stops for the session.

**Standing only.** The wheels' ground is measured at rest, so the probe and the box want the car below 0.5 m/s (its root
rigid body's velocity, or its moves if that read fails): F11, N and Shift+N on a moving car say "Stop the car …" on the
screen and in the log and do nothing.

**The probe** (lines «Test Trailer probe: …»), for every car of this player once it has been found for 2 s and stands
(after 30 s of waiting it is probed anyway: "never stood still … probed anyway, the wheels' ground may be off"), the
session's part only for a new runner; a car is told apart by its root's pointer and instance id, so a new car at a
reused pointer is probed. All of it again by F11 while driving, standing:

- `physics:` `Physics.simulationMode`, `Time.fixedDeltaTime`, `Physics.defaultSolverIterations`,
  `defaultSolverVelocityIterations` (both Unity calls the game build may have stripped: then "unread"), `Physics.gravity`.
- `Fusion:` the runner (`OverlandNetworkRunner.Instance.NetworkRunner`): `IsServer`, `IsClient`, `IsSinglePlayer`,
  `GameMode`, `Mode`, `TickRate`, `DeltaTime`.
- `Fusion physics add-on:` the `RunnerSimulatePhysics3D` on the runner's object (else any `RunnerSimulatePhysicsBase`
  under it or under `OverlandNetworkRunner`'s): `ClientPhysicsSimulation`, `PhysicsAuthority`, `PhysicsTiming`,
  `SetUnityFixedTimestep`, `DeltaTimeMultiplier`, `enabled`.
- `keys:` every binding of every map of `GameInput.PlayerInputActions.asset` (the player's rebinding first) on
  `<Keyboard>/n`, `/b`, `/x`, `/f11`, with the map and action; "N free" when none has N.
- `body:` the root's `Rigidbody`: mass, centre of mass, inertia tensor and its rotation, interpolation, collision
  detection, solver iterations (position, velocity), max depenetration and angular velocity, kinematic or not; the
  root's layer and its name.
- `Winch/WinchAttachPoints/Rear:` car space and world; `tow bar:` whether `Parts/<X>_CosmeticParts/Visual/Rear_Bumper/TowBar`
  exists and is fitted, and if fitted its drawn bounds in car space and the height at its rear-most end (from LOD0's
  vertices if the mesh is readable, else the bounds' middle – the line says which); `hitch:` the point the box uses and
  which of the two it is.
- `wheel …:` each `VPFunselektor3DRaycastWheelCollider` under `Axles/WheelColliders` (every car's wheel type in the
  prefabs): the drawn wheel's centre (`wheelTransform`) and the collider node's in car space, radius, suspension travel,
  bottom; `ground:` the median of the wheels' bottoms (one drooped wheel does not set it) and the lowest, the car's
  ground in car space.
- `layer … collides with:` and `ignores:` the car layer's row of the collision matrix (`Physics.GetIgnoreLayerCollision`).
- `accessory bodies move (standing): yes/no (N bodies, K kinematic; most moved … mm relative to the car in 1 s, the car
  went … m)`: the active rigid bodies under the car other than its own (lantern, pan, whip lights, shackles), sampled
  every frame for a second after the probe. Then `accessory bodies move while driving: …`, the same for a second, by
  itself the first time the car drives faster than 3 m/s within 3 minutes of the probe (else "not read"). On a client
  "no" while driving means its game does not step physics.

**The box** (lines «Test Trailer: …»): N toggles it on this player's car (or moves it there from a car this player no
longer drives), Shift+N picks the next size (S → M → L) and rebuilds it if shown, only while the game's driving
controls are on and the car stands. A child of the car's root on the car's layer,
mesh built in code (body box, drawbar from the body's front at floor height to the coupling head, 12-sided wheels
beside the body, axle beams), the coupling's tip on the hitch, the wheels' bottoms on the measured ground, the floor
0.45 m over it, the axle (the tandem's middle, its axles 0.9 m apart) at 58 % of the body's length. Each press logs
the size, the coupling and where it came from, the axle(s), the floor's and the coupling's heights over the car's
ground, the body's span, the drawbar's slope over the rod's own run (the drawbar less the 0.2 m coupling head) and
the material (a copy of the car body's with
`_Main_Texture`, `_Livery_Texture`, `_AO_Texture` white, `_Mask_Texture` black, `_BaseColor`/`_Color` grey where the
shader has them; URP Lit when it has none, or with `LitShader = true`). It goes with the car, by N, and when the
active scene changes (not at an additive scene's unload).

**What the owner does, one session** (solo, then if possible as a client in someone's co-op session):

1. Drive any car; stop and press F11 once, then drive off faster than about 10 km/h for a few seconds: the moving read
   of the accessories is taken by itself (F11 works standing only).
2. Standing: N (size S), Shift+N (M), Shift+N (L), look at each from behind and the side (the coupling at the bumper, the wheels
   on the ground, nothing through the car), N to hide it.
3. Do it with the tow bar fitted at the mechanic, and without.
4. Two or three different cars (a short one like the Tinny, a long one like the Burro, one of the long cars of the
   other mods if at hand).
5. Send `MelonLoader/Latest.log`; screenshots of anything that looks wrong.

## 8. Stage 2 build (0.0.2)

What the 0.0.1 probe read in the owner's game (Mammoth, solo, 23 November 2026): `Physics.simulationMode` Script,
fixed step 0.02 s; Fusion 50 ticks a second; `RunnerSimulatePhysics3D` on the runner's object, `ClientPhysicsSimulation`
SimulateAlways, `PhysicsTiming` FixedUpdateNetwork, `SetUnityFixedTimestep` True – the game's physics is stepped by
Fusion's add-on, not by Unity's FixedUpdate. The car: 3200 kg, centre of mass (0, 0.65, −0.05), interpolation None,
Discrete, solver 10/10, layer 7 Car, which collides with Default, Car, Terrain, Objects, BrokenCar, Tree, Rock,
CampableRock, Lobby and the camera and tool layers. The keys probe failed on a null; 0.0.2 names each read on the way
and falls back to the input asset's JSON.

The base is the owner's earlier prototype, Trailer Lab (0.1.x): a rigid body with passive raycast wheels and a ball
joint. Its one recorded run had no wheel find the ground: its wheel mounts were set from the hitch's height, so their
rays (about 0.9 m) ended above the ground about a metre under the hitch, and the trailer hung on the ball and swung
like a pendulum, up to 13.8 kN on the hitch. Its forces were also added in `OnFixedUpdate`, which the game's Fusion-led
step does not follow. 0.0.2 sets the wheels from the ground the car's own wheels stand on, and steps them with Fusion.

**What it does** (N while driving, solo or host):

- **Refuses** (a line on the screen, «Test Trailer: size M not hitched: …» in the log): no car, no runner, a client
  game («Stage 2 is solo/host only for now»), the car moving (0.5 m/s or more) or leaning over 20°, no ground under a
  wheel, the ground behind too uneven (more than 25° of pitch or 20° of roll to the car), something in the trailer's
  volume (layers Default, Car, BrokenCar, Tree, Rock, CampableRock, Objects; the towing car ignored: «No room for the
  trailer: 'X' (Layer, its top … m over the ground under the trailer)»; Default only from 0.35 m over the ground, so a
  bumpy ground mesh on Default does not count, the others from 0.15 m), no physics add-on to step with. A trailer
  being replaced this frame (Shift+N, or N on another car) is taken out of the physics at once (its box off, its body
  kinematic without collisions) and never counts as in the way.
- **The body:** its own GameObject in the car's scene (not the car's child), on the car's layer. `Rigidbody`: the size's
  mass (S 350, M 800, L 1400 kg), centre of mass a third of the body's height over the floor and ahead of the axle so
  11 % of the mass rests on the ball (logged as Trailer Lab's TonguePercent), inertia from two boxes (body 85 %,
  chassis and wheels 15 %, Trailer Lab's BoxInertia), interpolation None as the car's (in Script mode Unity's
  interpolation is likely ignored, and where it is not it lags the ball), ContinuousSpeculative, solver 12/4,
  maxDepenetrationVelocity 1, maxAngularVelocity 7, damping 0.04/0.12. One `BoxCollider`, the body only (its bottom the
  floor, 0.45 m over the ground); `Physics.IgnoreCollision` with every collider of the towing car once the box and the
  body exist, checked on one pair with `Physics.GetIgnoreCollision` (logged), and set again every 2 s for colliders the
  car gets later (by instance id). No collider on the drawbar or the wheels.
- **The wheels** (Trailer Lab's PassiveWheel): two, four on L, radius 0.35/0.38/0.40 m, beside the body. Three rays each
  (the tread's middle and two shoulders) on Trailer Lab's ground layers (Default, Terrain, Rock, CampableRock, Objects,
  Lobby, DefaultNotOccludingCamera, DefaultAllowingPlacedTools), skipping the trailer and the towing car and loose
  bodies. Travel 0.30 m; spring rate the wheel's load × ω² (ω 10.68 rad/s, 1.7 Hz: 8.6 cm static compression), damper
  1.5 × load × ω, side grip up to 0.75 of the normal force, rolling resistance 1.5 %. Their forces go in once per
  physics step, in Fusion's: the add-on's `OnBeforeSimulate`, dt the add-on's `PhysicsSimulationDeltaTime` (DeltaTime ×
  DeltaTimeMultiplier) if readable, else the runner's `DeltaTime` (logged which). If the car's body is gone in the
  middle of a frame, the step makes the trailer kinematic and removes the joint at once, and the frame detaches it. If it cannot join that
  step, it refuses; if the step never calls in 1.5 s, it detaches – never Unity's FixedUpdate.
- **Placing it:** the coupling exactly on the hitch, the car's yaw; pitched and rolled round the coupling (two passes)
  so each wheel stands at its static compression on the ground its rays find. The joint is made and connected with the
  trailer at the car's own rotation, so its zero angles are the car's pose, and only then is the trailer turned to the
  placed pose (the pitch and roll to the car are logged); the car's velocity at the coupling and
  its spin. At hitching each wheel's ground is logged (collider, layer, hit distance, compression); a wheel with none
  logs every hit of a ray on all layers (name, layer, distance, trigger, body kinematic or dynamic) and nothing is made.
- **The hitch:** with the tow bar fitted, the tow ball estimated from the tow bar's LOD0 bounds as Trailer Lab did
  (their top less 2.5 cm, their rear plus 3.5 cm); else the winch's rear attach point; the line says which. The tow bar
  is never shown by the mod. A `ConfigurableJoint` on the trailer: anchor the coupling, connected anchor the hitch in the
  car's space (set by hand), positions locked. Its axis is up and its secondary axis right, so the joint's X (the twist)
  is the yaw, free; its Y the pitch, ±30°; its Z (along the trailer) the roll, ±35°. Y and Z are soft limits sharing
  Unity's one `angularYZLimitSpring`: set from the pitch (the trailer's inertia round the hitch at 2 Hz, damping ratio
  0.8), which leaves the roll stiffer and more than critically damped (both logged). Projection position and rotation
  (0.05 m, 5°), no collision with the car. `connectedMassScale` is left at 1: Unity scales the connected body's inverse
  mass with it, so the 4:1 idea needs numbers first (stage 3).
- **Detaches** (a line on the screen and «… ended after … – why» in the log): the hitch force over 8 × the trailer's
  weight for 0.5 s; a jackknife over 100°; the trailer or the car over 60° off upright; the trailer's coupling more than
  2 m below the car's hitch; no wheel on the ground for 2 s; the car gone; the active scene changed; a failure in the
  step.
- **Numbers:** «Test Trailer telemetry: …» once a second: the size, car and trailer speed (km/h), the angle between car
  and trailer, the trailer's roll, the car's roll and pitch, the hitch force (kN) and its vertical part (kg), each wheel
  (its ground and layer, compression, vertical force in N, or «air»), the physics steps the wheels ran in that second
  (about 50) and their cost. At the end the run's largest of each and the fewest steps in a second.
- Unity calls the game itself never makes are guarded: `Joint.currentForce` (without it no overload check and no hitch
  numbers, said once), `Physics.IgnoreCollision` and `Physics.GetIgnoreCollision`, `Joint.enablePreprocessing` (a
  failure is in the hitching lines). A failing telemetry line is said once and the checks go on; if the checks
  themselves fail, the trailer is detached, never left running unchecked.

**What the owner does** (solo, or hosting; with the tow bar fitted and without; each size S, M, L; on one car first,
then a light one and a heavy one): hitch standing on flat ground, then

1. straight at 30 km/h, then at 60 km/h;
2. a slalom;
3. a 90° turn at 20 km/h, then at 40 km/h;
4. a steep climb and the descent;
5. an off-camber slope;
6. reversing;
7. a jackknife on purpose (reverse with the wheel turned until it detaches).

Note for each what was seen (sway that grows or dies, the car pulled about, a wheel in the ground, a detach), and send
`MelonLoader/Latest.log`. Pass: no fall through, no spin, the car drives with each size, no roll-over in turns the car
alone takes; the telemetry's numbers per case go into a table here.

## 9. Stage 3 build (0.0.3)

**What 0.0.2 showed** (the owner, 23 November 2026, `Latest (57).log`, Mammoth and Protector): it drives and every
wheel finds the ground. But a parked trailer crept sideways on any slope; while driving it jerked, not smooth like the
car; the long L box caught bumps with its lower edges and stopped; on a side slope the Mammoth with L showed the
trailer's roll at −32° (near the ±35° joint limit), a 42 kN hitch spike and the car rolled to 47°; two tight turns
ended in "jackknifed" (over 100°); one hitching on a slope was refused, a wheel's ray 4 cm short.

**What changed, and why:**

- **The car's own wheels** (the owner's decision). Radius, tyre width and track from the car's rear wheel colliders
  (`VPFunselektor3DRaycastWheelCollider.radius`, `tireWidth`, their positions); suspension travel the car's, at most
  0.35 m; spring 1.7 Hz on the wheel's load as before. The drawn tyres are copies of the car's rear wheels as it wears
  them now (the LODGroup under `Axles/WheelMeshes/RL` and `RR`; TireShop's tyres too), colliders removed; the plain
  12-sided wheel only if the copy fails. A copy is made under an inactive holder (nothing in it wakes), stripped of every
  component but Transform, LODGroup, MeshFilter and MeshRenderer, then shown; the car's renderers' property blocks (its
  livery, its dirt) are copied to the copies at hitching and every 2 s.
- **The car's grip.** The tyre: the car's rear left wheel's friction as it drives now (`VehicleBase.m_wheelState` to
  find the wheel, `GetWheelTireFriction(i)` as `TireFrictionStandard`, its `settings`), else the car's
  `VPVehicleController.tireData.tireSettings`. The surface: the game's ground material under each trailer wheel – the
  car's `groundMaterialManager.GetGroundMaterial(car, GroundMaterialHit{collider, physicMaterial, hitPoint})`, its
  `Config.Grip` (and terrain layer for the name), read when the wheel's ground changes and four times a second. VPP's own
  slip-to-force curve (`GetTireForce` with the wheel's `ContactPatch`) is native and wants the car's per-wheel state,
  so it is not used: the trailer takes the same coefficients – lateral peak friction while sticking, lateral limit
  friction while sliding, sticking again below `maxAdherentVelocity` – each × the surface's grip. If the ground
  material cannot be read, every wheel takes the car's own current grip (said once). The configs' wetness modifiers
  (`WetnessGripModifier` and the like) are left out for now: rain does not change the trailer's grip in this build.
  `GetGroundMaterial` hands back a `GroundMaterial` the car's wheels share and sets its current terrain layer and
  physic material (its code is native), so before each call the writable fields (`CurrentTerrainLayer`,
  `CurrentPhysicMaterial`, `customData`) of every material the car can be reading – its wheel states' own, the manager's
  `fallback`, any handed to the trailer before – are saved, and put back right after; one `GroundMaterialHit` is reused. At hitching the car's rear left
  wheel's own `tireForceMult` and `dynamicGrip` (grip, target, tyre tier) are logged beside it, to see how the game
  combines them.
- **Stick-slip (fix 1).** Each wheel keeps a stick point at its contact, held sideways by a spring (3 Hz on the wheel's
  load) and a critical damper up to the peak friction × the normal force; past it the tyre slides at the limit friction
  and drags the stick point along; it sticks again when it slides slower than the adherent speed and the spring alone
  (not the damper) is within the peak grip. The damper works on the tyre's sideways speed at the contact point, not at
  the hub; a stick point more than 1 m off sideways is set again at the contact; a wheel whose rays miss keeps its
  stick point for 3 steps (a rut, a bump) before it is dropped. Forward it rolls freely (no brake), with 1.5 % rolling
  resistance. A parked trailer
  should not creep on a slope; each wheel's state (sticking or sliding, its offset, its sideways slip) is in the
  telemetry.
- **Level drawbar, high floor.** The drawbar is level at the hitch's height and the body's floor is there too, so the
  clearance under the body is the hitch's height; the wheels stand beside the body at ground + radius with fenders over
  them and hangers down to the axles (looks only). The axle is set where the body's centre of mass puts 11 % of the mass
  on the ball (sizes, masses and the 11 % as before).
- **Drawn from the drawn car (fix 2).** The physics body has no renderers; its looks are a separate object placed every
  LateUpdate. The drawn car is its `NetworkRigidbody3D`'s `InterpolationTarget` when it has one other than the root
  (Fusion then moves that child and leaves the root at the body's pose; its offset from the root taken at hitching),
  else the root. Until 150 frames with the car faster than 1 m/s have been seen (no time limit while towing) the look
  is placed at carDrawn × inverse(carPhysics) × trailerPhysics while the mod measures, in those frames only, how far the
  drawn car is from its `Rigidbody` pose and whether it moves in frames with no physics step. Then it picks a mode and logs it («Test Trailer: drawing: … mode … – why», with `Physics.autoSyncTransforms`):
  - **DrawnCar** – the drawn car is 1 mm or more from its body's pose on average: Fusion interpolates it, and the same
    formula keeps the ball on its drawn tow bar;
  - **OwnInterpolation** – the drawn car is at its body's pose but moves in at least a fifth of the frames without a
    step (its body's pose itself reads interpolated): the trailer's and the car's poses after the last two finished
    steps (recorded in the add-on's `OnAfterSimulate`, joined and left with the same delegate; at the start of
    `OnBeforeSimulate` if it cannot be joined, logged) are interpolated by the runner's `LocalAlpha` (or the time since
    the last step over its dt, clamped, if that is unreadable – logged when used), and the look is carDrawn ×
    inverse(carInterpolated) × trailerInterpolated;
  - **Raw** – the drawn car is at its body's pose and does not move between steps: the car itself is not interpolated,
    so interpolating the trailer alone would pull the ball off the car; the trailer is drawn at its step pose with
    the car, and the log says interpolation cannot help. The telemetry gives the
  hitch force's spread over the second's steps and its largest step-to-step jump, to tell real oscillation from noise.
- **The box does not meet the ground (fix 3).** `Physics.IgnoreCollision` between the box and every `TerrainCollider`
  (all found by type once at hitching; after that only a terrain collider a wheel stands on that is not ignored yet;
  by instance id). Trees, rocks, cars and objects still meet it. M and L
  have their lower front and rear edges chamfered (45°, 0.25 m) in the look.
- **Roll free at the hitch (fix 4):** the joint's roll is free; pitch ±45° soft (widened from 30°: whether Unity's PhysX
  joint measures its swing in the car's frame or the trailer's is in the engine's native code, not in the game's, so a
  limited pitch could catch the roll at large turn angles). The telemetry logs the joint's yaw, pitch and roll to the
  car each second, with the second's largest pitch and roll measured every step, so the off-camber run shows it.
- **L lower and wider (fix 5):** its centre of mass a quarter of the body's height over the floor; its track the car's,
  or the body's width and a tyre, whichever is wider.
- **Jackknife (fix 6):** the joint's yaw is free to ±85°, then a soft stop (a spring and damper from the trailer's yaw
  inertia round the hitch at 2 Hz, damping 0.8); it detaches only past 110°.
- **Hitching on slopes (fix 7):** at hitching each wheel's rays look 0.3 m past its travel; a wheel that far hangs
  at full travel and the trailer settles.

**Safety in 0.0.3:** unhitching and every detach first take the trailer out of the physics at once (its object
deactivated, the body and the joint with it), then leave Fusion's step, then destroy it – never a kinematic body still
on the car's hitch for a tick. Each step compares the car's body with the last step's: a jump over 2 m or 30° (the game
recovering, resetting or flipping the car) deactivates the trailer before the step and detaches it («the car was moved
by the game»). A failure in drawing it ends this trailer only.

**Log lines** at hitching: «Test Trailer: size M hitched to '…'» (body, floor height, coupling), «… wheels from the
car's rear wheels: radius, width, travel, track, axles; looks: N of N wheels the car's own drawn wheels», «… grip: tyre
… (peak, limit, adherent speed; the car's rear left now: tireForceMult, grip, tier on its surface); surfaces …», «… mass:
…» (CoM, 11 %, inertia, the terrain ignore), «… hitch: …» (yaw soft stop, pitch, roll free), «… wheels at hitching; …»
(ground, compression, surface, grip, friction), «… steps with Fusion …»; once, «… Fusion draws the car … from its rigid
body's pose …»; every second «Test Trailer telemetry: …» with each wheel's surface, grip, friction, sticking or
sliding and the hitch force's spread and jump; at the end the summary with the largest spread.

**What the owner does** (solo or hosting, the same car first, then a second car with different wheels):

1. Park with the trailer on a side slope for 30 s, the car standing: does the trailer stay where it is?
2. With L, drive over bumps and a crest.
3. With L, an off-camber slope.
4. A tight turn until the soft stop (it should hold near 85° and detach only past 110°).
5. On two different cars: are the trailer's wheels the car's own, and does it look as smooth as the car while driving?

Send `MelonLoader/Latest.log`; a screenshot or a clip of any jerk or creep.
