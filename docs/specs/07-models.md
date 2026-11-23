# 07 – Real trailers from the owner's pictures

Fourth in the agreed order. The owner's pictures (22 November 2026): a box trailer like the Pike 1902, a hard shell with
a pop-top like the Decamp Cross X5, a folding camper like the Wilde Adventurer, the Sasquatch XC with side lockers and a
roof rack.

## Design

**The sizes become types.** The physics stays the bench's (body, axle on springs, the car's wheels, the Z drawbar); each
type gives its shape, mass and features:

| Type | From | Size (closed) | Axles | Mass | Features |
|---|---|---|---|---|---|
| Box | Pike 1902 | 3.0 × 1.7 × 1.4 m | 1 | 600 kg | side door, spare wheel, gas bottle, toolboxes on the drawbar |
| Overland | Sasquatch XC | 3.6 × 1.9 × 1.3 m | 1 | 800 kg | side lockers, a roof rack of its own mesh, spare wheel |
| Pop-top | Decamp Cross X5 | 4.0 × 2.0 × 1.8 m | 1 | 1100 kg | the roof rises in camp, door, windows, awning |
| Folding camper | Wilde Adventurer | 4.2 × 2.0 × 1.4 m | 2 | 1000 kg | the tent unfolds sideways in camp |

With the body on its axle (spec 04) the floors sit 0.69–0.79 m up; the log gives each type's centre of mass height and its
roll-over limit (in g) at hitching, and a type below 0.6 g is lowered before it ships (0.0.3's L, 2.2 m tall on a high
floor, turned over).

**How they are made.** As UnfoldingTent's tent: a mesh built in code (the shell, the doors and lockers as insets, the
fenders, the jockey wheel, the drawbar's A-frame) and an atlas painted by a tool from flat colours with soft shade – the
game's own look, nothing taken from the photos. The material: a copy of the car body's, with the trailer's own atlas,
its own dirt mask and a lights mask (spec 08) in it: the car's property block cannot be copied as the tent does (the
tent's renderer shares the game's tent textures; the car's block points at the car's atlas), so only the car's dirt
amounts (the block's float values) are copied onto the trailer's own block. Offline pictures of each type behind three cars first; the owner
picks and corrects; then in the game.

**Camp features.** The pop-top's roof and the folding camper's tent use the tent's camp detection and its book-like
animation (UnfoldingTent `Camp.cs`, `Unfold.cs`): they open as the campfire goes up when the trailer is within 30 m of the
campfire and parked or its car stands, fold when the camp is over, and by a key when standing. Unfolded parts are looks
only: the collider and the centre of mass stay those of the closed trailer (it only unfolds standing).

## Questions for the owner

- Which two types first? Proposed: the box (simplest, proves the look) and the folding camper (the most wanted, reuses
  the tent).
- Colours: plain (a grey like the cars' primer) or the car's livery colour?
