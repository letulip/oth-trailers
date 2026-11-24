# 13 – The T key, tail lamps, park and hitch again, a first co-op (TestTrailer 0.0.8 and 0.0.9)

Asked for by the owner on 24 November 2026 with `Latest (62).log`: tail lamps on the trailer («мы до сих пор этого не
сделали»), a first version for multiplayer «чтобы можно было проверить, как это работает», unhitching so the trailer stays
standing (the other player asked for it) – how it lives in the game as an object of its own, how you back up to it, hitch
and unhitch – and T instead of N («это трейлер»). The model's render faults (wrong colours on walls, see-through parts)
are the author's to fix.

The owner's answers (24 November 2026): the lamps follow the towing car's own lamps; anyone may hitch a parked trailer;
co-op starts with the simple variant (the host's physics only); the author will name his lamp parts. And: hitching is
never automatic – back up close, within 0.2 m of the coupling, then T; a parked trailer stays where it was left, on the
map too, and is kept between sessions in a way that breaks nothing.

It takes the first steps of 06 (park), 08 (lights; the overrun brake stays in 08) and 05 (co-op): two builds, each
played before the next.

- **0.0.8** – the T keys, tail lamps, park and hitch again, the trailer on the map, kept between sessions; solo and host
  only, as now.
- **0.0.9** – co-op, first step: every player with the mod tows and sees every trailer; the host's physics only.

## The keys: T

The game binds T only in its UI map (`UI/ModsSort`, the mods list's sorting, read from `PlayerInputActions` in the
cars bundle of 13 November 2026); in `Driving` it is free, and the trailer's keys work only while driving. So:

| Key | Does |
|---|---|
| T | no trailer: put one behind the car (standing); hitched: unhitch and park it (standing); near your parked trailer's coupling: hitch it |
| Shift+T | hitched or parked: the next size (S → M → L), rebuilt in place |
| Ctrl+T | remove your trailer (a line on the screen first, Ctrl+T again within 3 s) |
| F11 | the probe, as now |

The probe line «keys:» reports T in every map with its action, so a game update that takes T shows in the log.

## Tail lamps (0.0.8)

**Which lamps.** Each side: a tail lamp, a brake lamp (the same red lens, brighter) and a reverse lamp (white). M wears
the author's red lenses (the model's red part at its back, `Material.020`: two upright strips at the rear corners) for
tail and brake; S, L and every size's reverse lamps are small lamp boxes of ours on the square rear ends of the frame
(the place the owner kept for lamps). The author can later name his lamp parts (`LAMP_TAIL`, `LAMP_REVERSE`, …) and the
bake will take them by name.

**How they glow.** A lens gets an emissive material of its own (URP Unlit, an HDR colour scaled by the level, so the
game's bloom catches it), and each side a small red point light (the brake: brighter, about 4 m) and a white spot for
reverse, at night as by day like the car's.

**What they follow.** The towing car's own `LightsController` as this game shows it: the tail lamps the level of its
rear lights, the brake lamps its braking lights, the reverse lamps its reverse lights – each read every frame from the
car's lamp group (its `TurnableLights`: the first light's intensity over its configured one), so the trailer fades with
the car and, in co-op, shows exactly what the car shows on this screen. Nothing of ours is sent. A parked trailer's lamps
are off.

## Park and hitch again (0.0.8)

**The trailer as an object of its own.** One trailer per player, hitched or parked. Parked, it is a body of its own with
no car: no joint, no car checks, drawn from its own physics pose; its own checks (turned over, fallen through, wheels off
the ground for 2 s – then it is removed and the log says why).

**When the game moves the car** (a recover, a reset, a portal) or the car is gone, a hitched trailer is no longer removed:
it is parked where it stands. Turned over, it is put back on its wheels in place first.

**Unhitch: T with the trailer hitched, the car standing (under 0.5 m/s).**

- a jockey wheel drops under the drawbar's front (a small caster, radius 0.12 m, drawn with its clamp): a ray and a spring
  holding the coupling at the height it had on the ball;
- the handbrake: each wheel's stick point holds along the wheel as well as across it;
- it sleeps when still (no wheel forces while asleep, so it stays asleep) and wakes on a collision or when hitched;
- its collisions with the car come back only once the car's colliders no longer overlap it (checked each frame), so the
  car does not jump out of it.

**Hitch again – never by itself.** Back up until the car's hitch is within 0.2 m of the coupling across the ground and
0.25 m in height, under 1 m/s: «T – hitch» on the screen; nothing happens until T. T: the jockey wheel lifts, the joint is made as at building (the trailer turned to
the car about the coupling, connected, turned back) so its limits are centred on the car, and a gap is closed over 0.5 s
by a spring before it locks. The Z drawbar is rebuilt for this car's hitch height (spec 12: the wheels are the same on
every car). Any car may hitch any parked trailer.

**Far away.** T on a car with your trailer parked elsewhere: «Your trailer is parked x m away (Ctrl+T removes it)».

**Out of reach.** The world around a parked trailer may not be there (another map after a portal, the ground not loaded
far away): a trailer whose map is not the current one, or with no ground under its wheels' rays, is held still
(kinematic, no forces, hidden on another map) and let go again only once the ground under all its wheels answers. So it
never falls through a world that is not loaded.

## On the map (0.0.8)

The game's map screen (`MapView`) draws the players' pins and the points of interest as `MapIcon`s on its own layer
(`aboveFogOfWarTransform`). A parked trailer gets one of ours: a copy of the map's own icon (`mapIconPrefab`) with the
label «Trailer» (the size and the owner's name in co-op), placed by the same world-to-map transform the game uses for the
players' pins – read at run time by putting the local player's car through it and comparing with the game's own pin; the
probe logs both, and if they disagree by more than a few pixels the icon is left out rather than drawn in a wrong place.
A hitched trailer has none (the car's pin shows it).

## Kept between sessions (0.0.8)

Our own small file, never the game's save: `UserData/TestTrailer/Parked.cfg`, one line per parked trailer –
`profile;map;size;x,y,z;yaw;pitch;roll;time` (the game's profile name, the map's scene name). Written when a trailer is
parked or hitched, every 30 s while a parked one moves, and on leaving the session; written to a temporary file and moved
over, so a crash leaves the old one whole.

On loading a world with that profile and map, once the player's car stands, each listed trailer is built parked at its
place (held still until the ground under it answers, above). Why it breaks nothing:

- the game's save is never read or written, so a save without the mod, a game update or removing the mod leaves the game
  as it was (the trailers simply are not there);
- a line that cannot be read, from an older version or another map, is skipped and logged, never fatal;
- a trailer that would stand inside something (another car, a building: its collider overlapping on load) is moved up to
  2 m up and aside to a free place, or skipped with a line in the log.

**Backups first** (the owner: «самое важное – сейвы не сломать… всегда был какой-то бэкап»). The mod never writes the
game's save, and still keeps copies so nothing can be lost:

- the game's saves: once each game start, before any world loads, every save folder the game uses is copied as it is into
  `UserData/TestTrailer/Backups/<date time>/` – `Documents\My Games\over the hill\Saves` (or OneDrive's Documents) and
  `AppData\LocalLow\Funselektor\over the hill\Steam\<id>\Saves` (the location the game moves them to from its
  17 November 2026 update), whichever exist; copied only, never read or changed; the last 5 kept, older ones deleted;
  a copy over 500 MB or one that fails is skipped and logged, never stops the game;
- our own file: before each write of `Parked.cfg` the previous one is kept as `Parked.cfg.1` … `.5`; a file that cannot be
  read on loading is set aside as `Parked.cfg.bad-<date>` (not deleted) and the newest readable backup is used;
- the log says where every backup went («backups: game saves 3 folders, 12.4 MB, to …; 5 kept»).

In co-op the host keeps the session's parked trailers in its own file (the guests' too, with their names); a guest's game
keeps none from another's session (spec 09's rule).

## Co-op, first step (0.0.9)

**What the game allows** (spec 05): a mod cannot spawn networked objects, so every game builds its own trailers; a mod's
data goes between players only in each player's custom properties in the Photon room; the host runs every car's physics.

**The host's trailers are the real ones.** Each player with the mod writes one property, `trailer`:
`v=1;want=<S|M|L|none>;act=<n>` – what they want and a counter of their T presses (hitch, park, hitch again, remove). The
host acts on it with its copy of that player's car (the same checks as solo), simulates every trailer – hitched behind any
car, or parked – and publishes in its own property what exists:
`v=1;host=1;t=<id>:<size>:<owner raw>:<car network id or 0>[:<x>,<y>,<z>,<yaw> when parked]`, the parked pose at most
twice a second while it moves. A refusal goes back as `refused=<raw>:<reason>` and shows on that player's screen. No host
flag within 5 s, or a host without the mod: T says «The host has no trailer mod: towing is off here».

**The guests draw.** A guest builds every trailer the host lists, its own included, without physics:

- hitched: drawn from the drawn car's hitch; the drawbar points at the coupling, the axle follows the coupling's path
  (a trailing arm: the axle moves only along the trailer's own length), pitch and roll from rays under its wheels; the
  wheels turn with the ground speed; the lamps follow that car;
- parked: at the host's pose, eased over 0.2 s between updates.

**What this tests.** The host's car and the host's view are exact. A guest's own car is pulled by the trailer only in the
host's physics: the guest's prediction does not know the pull, so the host's snapshots correct it, more under hard pulls
(starting, braking, a slope). The log counts it; if it shakes, the next step is spec 05's full trailers on the guests
(history by tick, stepped in the resimulation).

## Log

- `keys: T: UI/ModsSort only, free while driving`;
- `lamps: tail/brake from 'rearlights'/'brakingRearlights' (k lights), reverse from 'reverselights'; lenses: the model's
  'Material.020' / our boxes on the frame ends`, and once a second while towing «lamps tail a, brake b, reverse c»;
- `parked at (x, y, z), jockey wheel on '<ground>'`, «kept: n trailers in Parked.cfg for '<profile>' on '<map>'», «loaded:
  n parked, held until the ground answers / let go», «map icon: world-to-map checked against the game's pin, off by x px», «asleep / awake», «hitched again by '<car>' (gap x m, the joint
  centred on the car)», «removed: …»;
- co-op: «host flag seen / missing after 5 s», «player N wants M; built / refused: …», «guest: drawing n trailers
  (hitched h, parked p)», and on a guest each second «own car corrected k times» beside the towing.

## Run

0.0.8, solo:

1. T on a car: the trailer comes; Shift+T changes the size; drive in the dark: tail lamps with the headlights, brake
   lamps on braking, reverse lamps on reversing.
2. Stop, T: it stays, on its jockey wheel, on flat ground and on a slope for a minute (no creeping); drive away.
3. Back up to it: nothing until within 0.2 m, then «T – hitch»; T; drive off. From the side at 20°: it hitches straight
   or is refused.
4. The log's «backups:» line names the copy; the folder is there with the saves in it. Park it, drive far away and back: it is where it was, on the map too. Leave the game, come back: it is still there.
   Recover the car while towing: the trailer stays parked where it was.
5. Hitch it with another car (the Pik-Up, the Mammoth): the drawbar fits the new hitch.

0.0.9, a host and a guest, both with the mod:

6. Both tow; each sees the other's trailer behind the other's car, the lamps too; drive side by side, then one behind the
   other through a turn.
7. The guest parks, the host hitches the guest's trailer, and back.
8. The guest starts and brakes hard on a slope: how much its own car jerks. Each sends `Latest.log`.

## Not in this step

The overrun brake (08); the guests' trailers with physics (05's prediction); a hitched trailer kept between sessions and
buying (09); far cars (05).
