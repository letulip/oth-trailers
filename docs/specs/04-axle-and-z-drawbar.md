# 04 – The body on its axle, a Z drawbar, straight-running wheels (TestTrailer 0.0.4)

Asked for by the owner on 24 November 2026 after playing 0.0.3 («это было очень хорошо, трейлер ведёт себя сильно
лучше»):

- «колёса как будто "восьмёрками" крутятся на трейлере»;
- «прицеп должен стоять по отношению к осям трейлера как кузов машины по отношению к осям машины, то есть ось на
  рессорах крепится к прицепу, из-за этого центр тяжести опустится»;
- «мы можем использовать Z-образную сцепку от прицепа до авто, чтобы с разных уровней переходить без проблем».

## What 0.0.3 does and what the log showed

0.0.3 (`Latest (58).log`, 24 November 2026) puts the floor and a level drawbar at the hitch's height, with the car's own
wheels beside the body. On the Protector the floor is 0.78–0.79 m over the ground and everything drove calmly. On the
Mammoth the hitch is high (1.09–1.13 m over the ground), so the L body (2.2 m tall) stood with its floor at 1.1 m and its
top at 3.3 m: it turned over twice (61° off upright) on slopes the car took.

The wheels are copies of the car's live drawn wheels. Each copy takes the live wheel's whole rotation relative to the car
(`Trailer.cs`, `CopyWheel`: `conj(car rotation) × source rotation`), which carries what the game's
`WheelTransformUpdater` gives the car's wheel at that moment – its spin angle and any camber or toe (it also has
`simulateSolidAxleVisual`). The copy is then spun about its holder's X axis: when the copied wheel's own axle is tilted
from that X axis, it wobbles in a figure eight.

## Design

**Wheels.** Which side a wheel is on comes from the car's own wheel (VPP's left/right flag on the wheel, `m_isLeftWheel`,
or the collider's x in car space), not from the copy's axes: a wheel mirrored by a negative scale would read wrong. The
copy's base rotation is the identity for a left wheel and 180° about Y for a right one (the game's wheel meshes are made
for the left side: in all 22 car prefabs the right wheel's LODGroup node is turned 180° about Y), its scale the absolute of the
source's; the rotation of the node `WheelTransformUpdater.wheelTransform` names and of every node between it and the copied
LODGroup is reset, so no camber, toe or frozen spin survives. The spin is about the axle (X) through the hub, its angle
advanced every drawn frame from the trailer's ground speed at that wheel (smooth at any frame rate, and not multiplied by
a guest's resimulated steps in spec 05).

**Body on its axle, as a car's body on its axles.** Each axle hangs under the frame on leaf springs:

- the axle's centre at ground + wheel radius (the car's), at static compression;
- spring hangers and leaf springs over the axle: the frame rails' underside at axle centre + 0.15 m;
- the frame rails 0.12 m deep; the drawbar's line at the rails' middle (axle centre + 0.21 m); the floor on the rails
  (axle centre + 0.27 m);
- the body between the wheels on the frame, the wheels beside it under fenders, as in 0.0.3.

The floor and every height that depends on it are computed per trailer (0.0.3 hard-codes the floor at the body's origin:
`Look.cs` `FloorY = 0`, and `Room()` and `Body()` assume it). For the Protector's 0.42 m wheels the floor comes to 0.69 m
(0.79 in 0.0.3), for the Mammoth's 0.52 m wheels to 0.79 m (1.10): the centre of mass comes down by the same, the L on the
Mammoth by 0.31 m.

**Suspension room.** A wheel can rise by its travel less its static compression (0.35 − 0.086 ≈ 0.26 m), more than the
room between the wheel's top and the fender or under the frame. A bump stop limits the rise to 0.10 m above static (the
spring force ramps up hard in the last 0.03 m), the fenders sit 0.13 m over the static wheel top, and the axle beam and
springs are drawn with the wheels (they follow the compression), not baked into the body's mesh.

**Z drawbar.** The drawbar runs level forward from the frame at the drawbar's line and, near the coupling, steps to the
hitch's height at 45°, then runs level for the last 0.2 m into the coupling head (a Z seen from the side). The step's
height h = the hitch's height over the ground − the drawbar's line; its run is |h| (45°), so it begins 0.2 + |h| m before
the coupling. The coupling (the joint's anchor, at the trailer's origin) is on the hitch exactly as now, so nothing in
the physics changes except the drawbar's shape and the body's height. Measured so far: the Protector 130's hitch 0.79 m
over the ground against a 0.63 m line (a 0.16 m step up), the Mammoth's 1.09–1.13 m against 0.73 m (about 0.38 m up); the
other cars' are logged at their first hitching. A step over 0.6 m either way is refused at hitching («The hitch is
x m too high/low for this trailer»). The drawbar has no collider, as now.

**Mass and inertia.** Unchanged sizes and masses (S 350, M 800, L 1400 kg), 11 % on the ball (the axle placed from the
centre of mass as now); the centre of mass at a quarter of the body's height over the new floor for L, a third for S and
M; the inertia from the boxes at their new heights.

## Log

At hitching: «… floor x m over the ground on its axle(s) (axle centre a m + springs 0.15 + frame 0.12), drawbar line
l m, step ±s m over r m (Z, 45°)», «wheels: side from the car's wheel, base rotation identity / 180°, camber c° and toe t°
of the source dropped», «bump stop at +0.10 m». In the telemetry: a wheel at its bump stop is marked.

## Run

1. On the Protector and the Mammoth, each size: the wheels from the side at 20–40 km/h – no wobble; the Z – the step at
   the car's end, level elsewhere; over bumps – no wheel through a fender or the frame.
2. L on the Mammoth across the same kind of slope that turned it over in 0.0.3: it stays up; send the log.
3. A tight turn and a crest: nothing new catches.

## Not in this step

Co-op, parking, the models, brakes and lights, the garage: specs 05–09.
