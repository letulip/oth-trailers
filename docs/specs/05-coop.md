# 05 – Co-op: everyone tows, everyone sees every trailer

The order of the work after 0.0.4, agreed by the owner on 24 November 2026 («остальное делаем по порядку»): co-op first,
because without it the trailers cannot be played with friends.

## What the game gives

- **Solo is a Fusion session too** (mode Single, host, 50 ticks): the same code runs solo and as a host.
- **The host runs every car's physics**, the guests' too: `VehicleBase.FixedUpdateNetwork` with each player's input
  (oth-modes game-engine.md, Multiplayer). A trailer that pulls on a guest's car must pull on the host's copy of it.
- **Guests step physics** (`ClientPhysicsSimulation = SimulateAlways`, read by the probe on 23 November 2026) **and
  predict every car**: on each snapshot from the host a guest's Fusion rolls the networked bodies back and simulates the
  whole scene again over the ticks it runs ahead – 1.7 forward and 2.4–4.4 resimulated ticks a frame on 17 November 2026
  (GuestLite's notes, guest-netcode.md).
- **Bodies that are not networked get those resimulated steps on top.** The game's dangling parts swing two to three and a
  half times too fast on a guest. GuestLite 0.3.0 (`Dangling.cs`): nothing during the resimulation ticks; before the
  first forward tick after one, each part is put back at the pose and motion relative to its car kept after the previous
  frame's ticks. That suits parts of a few kilos; a trailer of 350–1400 kg hangs on the car's hitch and its pull is part
  of the car's predicted motion, so it needs more (below).
- **GuestLite's Far** draws cars over 150 m away kinematic from snapshots: no physics to pull on there.
- **A mod cannot spawn networked objects**: every game builds its own trailers.
- **A mod's data between players goes through each player's custom properties in the Photon room**
  (`LocalPlayer.SetCustomProperties`, read from `CurrentRoom.Players`; CosmeticMix `Peers.cs`), never Fusion's reliable
  data or a hook on its path. A host flag goes inside the host's own message with a grace time for late properties, as
  TireShop's co-op does (`Coop.cs`: is-host in the message, 5 s HostGrace).

## Design

**Rigs per car.** 0.0.3 has one static rig and refuses clients (`Trailer.cs`); 05 keeps a rig per towing car, keyed by the
car's network object.

**What is shared.** Each player with the mod writes one property, `trailer`, a versioned list of fields so 06 and 07 can
add theirs: `v=1;p=<PlayerRef.Raw>;want=<size or none>;host=<0/1>;built=<list of raw:size the host built>`. Readers look
twice a second and on every properties' change.

**The host builds, the guests follow it.** A guest asks (its `want`); the host runs the checks (standing, upright, room,
ground) on its copy of the guest's car, builds the trailer and lists it in its `built`; only then does the guest build its
own copy. A host that refuses says why in its property (`refused=<raw>:<reason>`), and the guest shows it on the screen.
Without a host flag within 5 s of joining, or with a host without the mod, nobody tows in that session: N says «The host
has no trailer mod: towing is off here», and nothing is drawn.

**The host's trailers are the real ones.** Full bodies with their wheels and joints on the players' cars; the host runs
those cars' physics, so the pull reaches the guests' cars as authority.

**A guest's trailers.** Each guest builds the same full bodies behind every towing car it simulates, its own included, so
its prediction of each car includes the trailer's pull. In resimulation:

- after every forward tick, each trailer's state relative to its car is kept by tick number (pose, velocity, angular
  velocity, the wheels' stick points and slip states), for the last 32 ticks;
- at the first resimulation tick of a frame, each trailer is set to its kept state for that tick relative to where the
  rolled-back car is, and then steps fully through the resimulation, wheel forces included, so the predicted car feels
  the same pull as the host's car did;
- the teleport check (0.0.3: a car jump over 2 m or 30° detaches) takes its last car pose from the same history, so a
  rollback is not a teleport;
- the wheels' visual spin advances per drawn frame (spec 04), not per step.

A car drawn kinematic by GuestLite's Far, or with no history yet, gets a follower instead: the trailer's yaw follows the
hitch's path, no forces, drawn behind the drawn car; it becomes a full body again when the car is simulated.

**Portals and respawns.** When the game respawns a car (a portal, a recover), its trailer is removed and, if the host's
`built` still lists it, rebuilt behind the car once it stands.

## Log

«… trailers shared; host flag seen / missing after 5 s», «player N asks for M; host built it / refused: …», «guest:
K resimulation ticks this second, trailers set from history at tick T», with NetDiag's corrections count beside it.

## Run

Host and guest, both with the mod, both tow (S and L); drive side by side 2 minutes, then one behind the other through a
turn; each sends `Latest.log`. Then the host without the mod: the guest is refused, nothing breaks. With GuestLite on and
off; one car far away (over 150 m) and back.
