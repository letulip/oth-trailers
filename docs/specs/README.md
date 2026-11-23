# Specs – the order of the work

Agreed with the owner on 24 November 2026, each step played before the next. Stages 0–3 (the probe, the box, the first
towed body, the car's own wheels) are in [`../test-trailer.md`](../test-trailer.md).

1. [04 – the body on its axle, a Z drawbar, straight-running wheels](04-axle-and-z-drawbar.md) (TestTrailer 0.0.4)
2. [05 – co-op: everyone tows, everyone sees every trailer](05-coop.md)
3. [06 – unhitch and leave it, back up and hitch again](06-park-and-recouple.md)
4. [07 – real trailers from the owner's pictures](07-models.md)
5. [08 – overrun brakes and the trailer's lights](08-brakes-and-lights.md)
6. [09 – buying a trailer and keeping it](09-garage-and-save.md)

Each step: an implementing agent, an independent reviewer, the lead's acceptance (build, checks), then the owner's run
and log.

Links between them: 05's property is a versioned list of fields that 06 and 07 extend; 06's wheels and removal key are
settled for the release in 09; 08's lamps need the lights mask of 07's atlas.
