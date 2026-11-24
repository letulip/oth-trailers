# Specs – the order of the work

Agreed with the owner on 24 November 2026, each step played before the next. Stages 0–3 (the probe, the box, the first
towed body, the car's own wheels) are in [`../test-trailer.md`](../test-trailer.md).

1. [04 – the body on its axle, a Z drawbar, straight-running wheels](04-axle-and-z-drawbar.md) (TestTrailer 0.0.4)
2. [10 – low weight, softer springs, a roll stop, the frame under the flat](10-roll-and-frame.md) (TestTrailer 0.0.5, added after
   0.0.4's run)
3. [11 – the author's first model on our base, in place of M's box](11-first-model.md) (TestTrailer 0.0.6)
4. [12 – one wheel set on every car; the drawbar and a lift take the car's height](12-one-wheel-set.md) (TestTrailer 0.0.7)
5. [13 – the T key, tail lamps, park and hitch again, a first co-op](13-t-lamps-park-coop.md) (TestTrailer 0.0.8, 0.0.9;
   the first steps of 06, 08 and 05)
6. [05 – co-op: everyone tows, everyone sees every trailer](05-coop.md)
7. [06 – unhitch and leave it, back up and hitch again](06-park-and-recouple.md)
8. [07 – real trailers from the owner's pictures](07-models.md)
9. [08 – overrun brakes and the trailer's lights](08-brakes-and-lights.md)
10. [09 – buying a trailer and keeping it](09-garage-and-save.md)

Each step: an implementing agent, an independent reviewer, the lead's acceptance (build, checks), then the owner's run
and log.

Links between them: 05's property is a versioned list of fields that 06 and 07 extend; 06's wheels and removal key are
settled for the release in 09; 08's lamps need the lights mask of 07's atlas.
