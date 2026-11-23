# 06 – Unhitch and leave it, back up and hitch again

Third in the agreed order (24 November 2026): the trailer becomes something you can park and come back to.

## Design

**A rig without a car.** 0.0.3's rig needs its car in every step (`Step` fails with no car body, `Check` tests jackknife
and drop against the car, `Visuals` draws from the drawn car). 06 adds a parked state: no joint, no car checks, drawn from
its own physics pose, its own checks (turned over, fallen through, wheels off the ground).

**Parked.** N with the trailer hitched (the car standing) unhitches it in place:

- a jockey wheel drops under the drawbar's front: a small caster (radius 0.12 m) with its own ray and spring, holding the
  coupling at its height above the ground;
- the handbrake: each wheel's stick point holds forward as well as sideways;
- it may sleep: while the body is asleep the wheels apply no forces (a force every step would keep it awake); it wakes on a
  collision or when hitched;
- the collisions with its car come back only once the car's colliders no longer overlap the trailer's box (checked each
  frame after unhitching), so the car does not explode out of it.

Ctrl+N while not towing removes the parked trailer (Shift+N stays the next size on the bench): a line on the screen first,
Ctrl+N again within 3 s.

**Hitching again.** Back the car up until its hitch is within 0.3 m level and 0.25 m in height of the coupling, the car
below 1 m/s; «Press N to hitch» on the screen; N: the jockey wheel lifts and the joint is made as at building – the trailer
turned to the car's rotation about the coupling, the joint connected, then turned back – so its limits are centred on the
car, not on the angle it was parked at. A gap is closed over 0.5 s by a spring on the joint's target before it locks.
The Z drawbar is rebuilt for the new car's hitch height; the trailer keeps its own wheels (the first car's on the bench;
a bought trailer's own in spec 09).

**One per player.** A player has one trailer, hitched or parked. N on another car with a parked trailer far away: «Your
trailer is parked x km away».

**Co-op.** The host owns every parked trailer: it simulates it and publishes its pose in its property when it moves
(`parked=<id>:<x>,<y>,<z>,<yaw>`, at most twice a second while moving); guests draw parked trailers as followers of that
pose. A player who backs up to one asks (`claim=<id>`); the host grants the first claim it reads (`owner=<id>:<raw>`) and
then builds it hitched as in spec 05.

**Sessions.** A parked trailer lives in the session; its saving belongs to spec 09.

## Log

«… unhitched and parked at …», «… asleep / awake», «… hitched again by '…' (gap x m, the joint centred on the car)»,
the jockey wheel on its ground.

## Run

Park on a slope 60 s (no roll, it sleeps); push it with the car slowly; back up and hitch on flat ground and on a slope;
hitch from the side at 20° (refused or works, the limits centred); drive off: the car does not hit its own trailer. In
co-op the other player sees it parked and hitches it.
