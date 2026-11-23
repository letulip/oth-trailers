# 08 – Overrun brakes and the trailer's lights

Fifth in the agreed order.

## What the game gives

Each car has a `LightsController` (decompiled `Il2Cpp/LightsController.cs`): `headlightsMode`, `brakingLightsOn`,
`reverseLightsOn`, and its lamp groups `headlights`, `rearlights`, `brakingRearlights`, `reverselights`
(`TurnableLights`, with their lights and lens flares and a `ShaderParameterID`), fading through a curve. The game has no
turn signals. Only the headlights' mode is networked (`GetNetworkedHeadlightsMode`); `NetworkedVehicleStatus` has no brake
or reverse flag, so on a guest the other players' brake and reverse lamps may be the guest's own guesses – a probe step
reads them first (below).

## Design

**Probe first.** On a guest, log once a second for every other player's car its `brakingLightsOn`, `reverseLightsOn` and
`headlightsMode` and whether its rear lamps glow (the `TurnableLights` values), while that player brakes and reverses.

**Lights.** The trailer's rear panel carries a tail lamp, a brake lamp and a reverse lamp each side, and a number-plate
lamp: emissive faces through the lights mask in the trailer's atlas (spec 07), set on the trailer's own property block
with the same values the car's `TurnableLights` put on its own (`ShaderParameterID`), plus one small URP point light per
side for the brake lamps at night. Each frame they follow the towing car's `LightsController` as this game shows it: tail
lamps with `headlightsMode` on, brake lamps with `brakingLightsOn`, reverse with `reverseLightsOn`. Where the probe shows
another player's brake or reverse state is not known here, those lamps follow the car's own drawn lamps or stay off.
Nothing of ours is sent.

**Overrun brake.** A real trailer of this size brakes itself when it pushes on the car. The drawbar's push (the joint's
force along the drawbar, toward the car; `currentForce` is a step old) is low-passed over 0.1 s; the brake comes on above
0.8 kN and goes off below 0.4 kN (hysteresis); while on, each rolling wheel gets a force −sign(v)·min(k·push, μ·N) along
its rolling direction (k set so the full push stops the trailer's own mass), and only near standstill does the forward
stick point hold it. Reversing: a real one locks out the overrun with a reverse latch – here the brake is off while the
car's networked gear (`GearboxGear`) is reverse, not a lamp flag.

**Handbrake.** Parked (spec 06) the wheels hold forward too.

## Log

«brakes: push x kN, braking y kN» in the telemetry; «lights follow '…'s LightsController».

## Run

Downhill with L: the car alone vs. with the trailer – stopping distance, the push on the hitch; reversing up a slope; at
night, the brake lamps with the car's.
