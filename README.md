# Over the Hill trailers – TestTrailer, a box on wheels behind the car

Trailers for the game Over the Hill, built as a MelonLoader mod. This repository starts with a test bench: a plain box
on wheels in three sizes behind any car, with numbers in the log, to learn how a trailer behaves in the game's physics
before real trailers are built. Not a mod for players yet and not on Nexus.

The spec, the game's facts it rests on and the stages, each played before the next: [`docs/test-trailer.md`](docs/test-trailer.md).
What comes next, in order: [`docs/specs/`](docs/specs/README.md).

## What it does (0.0.4)

- **N** hitches a trailer behind the car you drive, solo or as the host (not as a client yet), only with the car
  standing, upright and with room behind it. **Shift+N** picks the next size and rebuilds a hitched trailer. **F11**
  writes the probe into the log again. The keys work only while driving.
- **Sizes:** S 2.4 m long and 350 kg, M 3.6 m and 800 kg, L 4.6 m and 1400 kg on a tandem axle as wide as the car;
  11 % of the mass on the ball.
- **The trailer:** a rigid body of its own with a box collider that meets trees, rocks and cars but not the ground.
  The towing car's own wheels: their size, the tyres the car wears, its tyre friction on the grip of the surface under
  each wheel (the game's ground materials). The wheels stick sideways and slide past the grip, so a parked trailer
  holds on a slope. The wheels' forces go in once per physics step, in Fusion's own step (`OnBeforeSimulate` of its
  physics add-on).
- **The body on its axle:** each axle hangs under the frame on leaf springs, as a car's body on its axles – the axle at
  ground + the wheel's radius, the frame 0.15 m over it, the floor on the frame (0.69 m over the ground on the
  Protector's wheels, 0.79 m on the Mammoth's), so the centre of mass sits low whatever the hitch's height. The wheels
  rise at most 0.10 m over their static height (a bump stop), under fenders 0.13 m over their top; the axles and
  springs move with them.
- **The hitch:** a ball joint on the tow ball (estimated from the tow bar when it is fitted) or on the winch's rear
  attach point every car has. Free to roll, pitch ±45° soft, a soft stop at 85° of turn. A Z drawbar: level from the
  frame, then a 45° step up or down to the hitch's height near the coupling; a hitch more than 0.6 m above or below the
  drawbar's line is refused.
- **The wheels drawn straight:** copies of the car's wheels, the side from the car's own wheel, none of the live
  wheel's camber, toe or spin kept, turning about their axle every drawn frame at the ground speed.
- **Drawing:** the trailer is drawn from the drawn car (Fusion's interpolation target), so it moves as smoothly; after
  a few seconds of driving it decides whether it also needs its own interpolation and says so in the log.
- **Safety:** it detaches itself, saying why, when the hitch is overloaded, it jackknifes past 110°, it or the car
  turns over, it falls off, its wheels lose the ground for 2 s, or the game moves the car (a recover or a reset).
- **The probe:** the game's physics step and Fusion's, the car's rigid body, hitch point, wheels and the ground under
  them, the layers it collides with, which keys the game binds.
- Nothing is sent between players and nothing goes into the save.

Settings in `UserData/MelonPreferences.cfg`, section `[TestTrailer]`: `Key` (N), `ProbeKey` (F11), `LitShader` (false:
the box wears a plain copy of the car body's material; true: Unity's URP Lit).

## History

- **0.0.4** – stage 4 ([`docs/specs/04-axle-and-z-drawbar.md`](docs/specs/04-axle-and-z-drawbar.md)): the body on its
  axle on leaf springs (the floor and the centre of mass down by 0.10–0.31 m), a Z drawbar, a bump stop, the wheels drawn
  straight (no figure eight). Not played yet.
- **0.0.3** – stage 3: the car's own wheels and grip, stick-slip, the trailer drawn from the drawn car, the box no
  longer meeting the ground, roll free at the hitch, a soft yaw stop, L lower and wider, longer rays at hitching.
  Played on 24 November 2026 («очень хорошо, трейлер ведёт себя сильно лучше»): calm on the Protector; on the Mammoth
  the L, its floor at 1.1 m, turned over twice on slopes, and the wheels wobbled in a figure eight.
- **0.0.2** – stage 2, the first towed body (the core of the earlier Trailer Lab prototype with its wheels stepped in
  Fusion's step). Played on the Mammoth and a Protector: it drives and its wheels find the ground; it crept sideways on
  slopes, jerked while driving, its long box caught bumps, and two tight turns ended in a jackknife.
- **0.0.1** – stages 0 and 1: the probe (physics stepped by Fusion's add-on, `SimulateAlways`, 50 ticks a second) and the
  box drawn at the hitch without physics.

## Building

The mod is built against the game's interop assemblies, which MelonLoader generates on the player's machine and which
stay out of git:

1. Copy `Over the hill/MelonLoader/Il2CppAssemblies` into `ref/Il2CppAssemblies` in this repository.
2. `dotnet build src/TestTrailer -c Release`
3. Put `src/TestTrailer/bin/Release/net6.0/TestTrailer.dll` into the game's `Mods` folder (MelonLoader 0.7.3).

Rules the code keeps, each learned in the game: the Harmony prefix on `VehicleBase.Spawned` skips the world's wrecks
first; no native hooks and no data of its own on Fusion's network path (a mod's data between players goes through the
Photon room's custom properties); every Unity call the game itself never makes is guarded.
