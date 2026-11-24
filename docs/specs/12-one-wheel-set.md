# 12 – One wheel set on every car; the drawbar and a lift take the car's height (TestTrailer 0.0.7)

Asked for by the owner after the author's model went onto M (spec 11), 24 November 2026: «колеса… единые между всеми
машинами… 40… А все лифты и размеры колес самого тягача компенсировать за счет разного дышла… или разного подъема»; and
«сороковые колеса, диски переиспользуем просто от самого тягача… уменьшаем… или увеличиваем наоборот до 40».

## Why

Up to 0.0.6 the trailer took the car's wheels as they are: the Mammoth's 0.52 m, the Protector's 0.42 m. So the trailer
stood higher or lower with each car, its centre of mass and tip angle moved with it, the tandem's spacing changed, and
the author's arches (drawn for a 0.78 m tyre) were nearly filled by the Mammoth's 1.04 m wheels while the model's own
spare stayed small. A trailer drawn once should stand the same way behind every car.

## Design

**One wheel: 0.40 m radius (0.80 m across) on every car and size.** The drawn wheel stays the car's own (its rim and tyre,
its livery and dirt, as 0.0.2 decided), scaled as a whole by 0.40 / the car's rear wheel's radius: the Mammoth's by 0.77,
the Protector's by 0.95, a small car's wheel up. The tyre's width for the ground rays is the car's tyre's width times the
same factor. The travel is 0.35 m on every car (the springs 1.2 Hz on the load, sag 0.173 m, the bump stop 0.10 m over
static, as spec 10). The grip stays the car's tyre's: a trailer that grips differently from its car pushes or drags it.
In the author's arches the wheel's top stands 0.13 m over the floor under the arch's 0.36 m.

**The same height behind every car.** The axle at ground + 0.40, the frame on 0.15 m spring hangers: the floor 0.67 m over
the ground, the drawbar's line 0.61 m. The centre of mass and the tip angle no longer depend on the car.

**The drawbar takes the hitch's height.** The Z's step from the drawbar's line to the ball (as spec 04): on the Protector
(ball 0.79 m over the ground) 0.18 m, on the Mammoth (1.11 m) 0.50 m.

**A lift of 4 inches when the step is tall.** When the step would be over 0.40 m, the hangers grow by 0.10 m (4″): the
frame, the body and the drawbar's line go up 0.10 m, the wheels and arches stay the same (more air over the wheel). On
the Mammoth the floor comes to 0.77 m and the step to 0.40 m. With the Z's 0.6 m at most, a trailer hitches to a ball up
to 1.31 m over the ground (1.21 m before); higher, it is refused as now.

**Tandem.** L's axles 0.92 m apart (the wheel's diameter + 0.12 m) on every car. S and L stand their wheels beside the
body by the tyre's width; L no longer widens to the car's track. M's stay under the model's arches.

## Log

At hitching: «wheels: one set, radius 0.400 m (the car's rear wheel r m scaled by k), width …, travel 0.35 m»; in the size
line «floor … m over the ground (axle centre … + springs 0.15 + lift 0.10 + frame 0.12)… step …» and whether the lift is on
and why («lift 4″: the step would be 0.50 m, over 0.40»).

## Run

1. M on the Protector and on the Mammoth: the same wheels in the arches behind both, the drawbar stepping down 0.18 m and
   0.40 m (lifted) to the ball; drive, turns, bumps; send the log.
2. A car with small wheels (the Niva-like one): its wheels scaled up to 0.40 on the trailer; the drawbar steps up or
   down to its ball.
3. S and L on both: the same height, the tandem 0.92 m apart.

## Not in this step

A lift chosen by the player; other wheel sizes per trailer; a wheel of the author's own.
