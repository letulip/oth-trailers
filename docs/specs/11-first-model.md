# 11 – The author's first model on our base (TestTrailer 0.0.6)

The owner brought the first trailer model from the other modder (`Trailer V1.fbx`, 24 November 2026): «прикинуть к
нашей базе, к нашей раме, вместо коробки среднего размера M… убрать из неё рессоры и колёса с осью».

## The model as it came

A box camper: a black shell and roof, khaki sides, a sloped front locker with a gas bottle and jerry cans on its deck,
wheels in arches cut into the sides, a spare wheel, tail lamps and jack stands at the back, its own chassis, A-frame,
coupler and jockey wheel. Flat colours, no textures. Built about twice real size (tyres 1.55 m across, jerry cans
0.92 m tall); at 0.5 the body is 3.34 × 2.40 × 1.50 m and its tyres 0.78 m. About 1.5 million triangles, most of them in
CAD parts: the coupler 296 000, the jockey wheel 373 000, the number plate 108 000, the gas bottle 81 000.

## Baked for the bench

`tools/bake_model.py` (Blender, headless) makes `src/TestTrailer/Models/M.bin`, embedded in the DLL:

- dropped: the wheels, hubs, axle and leaf springs (the car's wheels and our axle and springs are used), the chassis,
  the A-frame and the coupler (our frame and Z drawbar), the jockey wheel and its clamp (spec 06), the number plate;
- the gas bottle, jerry cans and spare wheel decimated to 1200–1500 triangles each: 8069 triangles in all, 14 colours;
- scaled by 0.5 and turned into the trailer's space (x right, y up, z toward the car), the body's bottom and front at 0;
- the collider: a convex mesh of its body, roof and front locker.

## On our base

M takes the model's body (its mass stays 800 kg, its weight packed low as spec 10):

- the floor on our frame (as every size), the model's bottom on it;
- the axle under the model's arches, 2.06 m behind its front; the body placed 1.51 m behind the coupling so that axle
  takes 11 % of the mass on the ball;
- the car's wheels at ±1.105 m (the model's), in its arches: on the Mammoth the wheel's top 0.25 m over the floor
  under arches 0.36 m high (0.11 m for the bump stop's 0.10), on the Protector 0.15 m;
- our rails at ±0.81 m, inside the wheel wells; no fenders of ours (the model has its arches);
- each colour a copy of the trailer's material tinted as the author painted it.

`tools/model_fit.py` draws it on the Mammoth's and the Protector's wheels: `preview/model/fit_M.png`.

## For the owner and the author

- The body is 2.40 m wide, 2.56 m over the wheels: wider than the cars.
- The Mammoth's 1.04 m wheels fill the arches (made for 0.78 m tyres) to within 1 cm at the bottom corners; at the bump
  stop they reach about 5 cm into the arches' lower edges.
- The number plate goes back as a light part; the jockey wheel comes with spec 06.

## Run

Shift+N to M on the Protector and the Mammoth: the model stands on the frame, the wheels in the arches, the drawbar to the
front locker; drive, turn, bumps. Send `Latest.log`: «… (the author's model 'M', drawbar 1.508 m to its front) …» and
«… the body is model 'M' (baked from the author's FBX: …)».
