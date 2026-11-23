# 10 – A trailer that stands like the car: low weight, softer springs, a roll stop, the frame under the flat (TestTrailer 0.0.5)

Asked for by the owner after playing 0.0.4 (`Latest (59).log`): «сильно легко болтается из стороны в
сторону… сильно валкий. Машина… более стабильно стоит… до последнего пытается выровняться»; «мне не нравятся торчащие из
прицепа наружу уголки профиля рамы… рама должна быть в длину такая же, как нижняя часть прицепа, где скосы». And on the
fix: the trailer is full by default, each size keeps its own mass, only its weight goes down; the springs softer; a soft
stop at the coupling.

Comes before 05: the order is 04, 10, then 05–09.

## What the log and the game show

M on the Mammoth turned over after 74 s on a side slope: its last roll 37.6°, the car's 36.8° (the car had leant to
50.9° and stayed up). Against the car the trailer leant little – the slope took both.

The game's cars have no anti-roll bar (no `VPAntiRollBar` in any car prefab) and no righting help (`CarKicker` is the
player's push). They stay up by their shape: the Mammoth's centre of mass 0.65 m up in its own space (0.83 m over the
ground), 3200 kg, wheels at ±0.90 m, springs 40 000 N/m a wheel (1.1 Hz on its 800 kg, ζ 0.62), bump stops.

The limit on a side slope, standing (tan = half the track / the centre of mass's height):

| On the Mammoth | Centre of mass over the ground | Half the track | Turns over at |
|---|---|---|---|
| the car | 0.83 m | 0.90 m | 47° |
| 0.0.4 S | 1.09 m | 0.915 m | 40° |
| 0.0.4 M | 1.32 m | 1.115 m | 40° (37–38° with its lean) |
| 0.0.4 L | 1.34 m | 1.215 m | 42° |

0.0.4 puts the centre of mass a third of the box's height over the floor (a quarter for L) – an evenly filled box.

## Design

**The weight low, the mass as it is.** Each size keeps its mass (S 350, M 800, L 1400 kg) and its look; as on a car, the
look adds nothing. The mass is split: the chassis (frame, axles, drawbar, floor) 20 % at the rails' middle, the shell
(walls, roof) 15 % at the box's centre, the load 65 % packed on the floor as a box of the body's width and length, its
height chosen so the whole comes to the floor + 0.2 × the box's height (S: 0.15, it is narrow). The centre of mass is the
weighted mean of the three, the inertia the sum of the three boxes round it; the axle is placed from it as now (11 % on
the ball). Expected on the Mammoth: S 44.7°, M 45.1°, L 44.6°; on the Protector (floor 0.69 m) about 46°.

**Softer springs.** 1.2 Hz on each wheel's load (ω 7.54 rad/s, was 1.7 Hz), damped as now (ζ 0.75): the static sag
0.173 m, the body still at the height spec 04 gives at rest, the bump stop still 0.10 m over static. A car whose travel is
under 0.35 m gets the spring that sags half its travel (logged).

**A soft roll stop at the coupling.** A ball coupling lets the trailer roll against the car only so far before its body
meets the ball's neck. The trailer's roll against the car (the joint's roll, as the telemetry reads it) is free to ±25°;
past it a spring and damper act on both: 2 Hz on the trailer's roll inertia round its drawbar's line (its own inertia
round the centre of mass plus the mass times the centre's distance from that line), ζ 0.8, as the yaw and pitch stops.
The torque goes on the trailer along its forward axis and the opposite on the car, applied each physics step with the
wheels (OnBeforeSimulate), and never more than twice the trailer's own weight times its half track, so a trailer can
never throw the car over. Roll stays free inside ±25°: the joint is unchanged. Detaching at 60° off upright stays.

**The frame under the flat only.** On M and L the rails run under the flat bottom between the chamfers (from the front
0.25 m in to the back 0.25 m in), the cross members at their ends, the drawbar's level run from the front cross member.
S has no chamfers and keeps full-length rails.

**The collider as the body looks.** 0.0.4's box collider keeps square lower corners, so its ends catch the ground where
the chamfers are drawn. On M and L the collider becomes a convex mesh of the chamfered profile (the body's shape exactly);
S keeps its box. Everything that ignores or tests the box (the car's colliders, the terrain, the ground rays) works on it
as on the box.

## Log

At hitching: «mass: CoM … (chassis 20 %, shell 15 %, load 65 % packed x m high) …», «turns over on a side slope at a° standing
(the car: b°)», «springs f Hz (sag s m)», «roll stop: free to ±25°, then k N·m/rad, c N·m·s/rad, at most t N·m»,
«collider: convex mesh of the chamfered body» or «box». In the telemetry: «roll stop x N·m» in a second it acted.

## Run

1. M and L on the Mammoth along the slope that turned 0.0.4's M over, and across ruts: the trailer leans with the car,
   stays up past 40°; send the log.
2. One wheel into a hole and over a rock at walking pace: the trailer stops against the car at 25° and comes back.
3. Crests and the end of a ramp: the ends no longer catch; nothing under the chamfers sticks out.

## Not in this step

An anti-roll bar between the trailer's wheels (as `VPAntiRollBar`): only if 1–3 still rock it.
