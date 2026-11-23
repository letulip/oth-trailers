using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Il2Cpp;
using Il2CppFusion;
using Il2CppFusion.Addons.Physics;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppVehiclePhysics;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using N3 = System.Numerics.Vector3;
using NQ = System.Numerics.Quaternion;
using Object = UnityEngine.Object;

namespace TestTrailer
{
    // Stage 2 (docs/test-trailer.md, section 8): the box as a real towed body behind this player's car, solo or on the
    // host only. N hitches it (the car standing and upright, with room behind it) or unhitches it; Shift+N the next size.
    // It detaches itself, with a line on the screen and in the log, when it goes wrong (Rig.Check).
    internal static class Trailer
    {
        private static int _size;
        private static Rig _rig;

        public static void Toggle()
        {
            if (_rig != null)
            {
                var mine = Cars.Mine();
                if (mine == null || mine.Id == _rig.Car.Id)
                {
                    End("unhitched by the key", null);
                    Look.Say("Test trailer unhitched");
                    return;
                }
            }
            TrySpawn(_size);
        }

        public static void NextSize()
        {
            int next = (_size + 1) % Look.Presets.Length;
            if (_rig != null)
            {
                TrySpawn(next);
                return;
            }
            _size = next;
            MelonLogger.Msg($"Test Trailer: size {Look.Presets[_size].Name} chosen, not hitched ({Mod.Key.Value} hitches it)");
            Look.Say($"Test trailer size {Look.Presets[_size].Name}");
        }

        // Every frame: the trailer's checks and its telemetry once a second
        // Every frame: the trailer's checks, the car's colliders ignored, its telemetry once a second. A failure of the
        // checks detaches it (it never runs unchecked); a failure of the telemetry is said once and the checks go on.
        public static void Tick()
        {
            if (_rig == null)
                return;
            try
            {
                string why;
                try
                {
                    why = _rig.Check();
                }
                catch (Exception e)
                {
                    why = $"its checks failed ({e.GetType().Name}: {e.Message})";
                }
                if (why != null)
                {
                    End(why, "Test trailer detached: " + why);
                    return;
                }
            }
            catch (Exception e)
            {
                // Even ending it failed: drop it as it is
                MelonLogger.Error($"Test Trailer: the trailer dropped, its end failed: {e}");
                var rig = _rig;
                _rig = null;
                try
                {
                    rig?.Destroy();
                }
                catch (Exception)
                {
                    // nothing more to do
                }
                return;
            }
            try
            {
                _rig.KeepIgnoring();
                _rig.Telemetry();
            }
            catch (Exception e)
            {
                _rig.TelemetryFailed(e);
            }
        }

        // The trailer's looks; a failure there ends this trailer only
        public static void LateTick()
        {
            if (_rig == null)
                return;
            try
            {
                _rig.Visuals();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Test Trailer: drawing the trailer failed: {e}");
                try
                {
                    End($"drawing it failed ({e.GetType().Name}: {e.Message})", "Test trailer detached: drawing it failed");
                }
                catch (Exception)
                {
                    _rig = null;
                }
            }
        }

        public static void Remove(string why)
        {
            if (_rig != null)
                End(why, null);
        }

        private static void End(string why, string say)
        {
            var rig = _rig;
            _rig = null;
            try
            {
                rig.Summary(why);
            }
            finally
            {
                rig.Destroy();
            }
            if (say != null)
                Look.Say(say);
        }

        private static void TrySpawn(int size)
        {
            try
            {
                Spawn(size);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Test Trailer: the trailer of size {Look.Presets[size].Name} not hitched: {e}");
                Look.Say("Test trailer failed (see the log)");
            }
        }

        private static void Spawn(int size)
        {
            var preset = Look.Presets[size];
            var car = Cars.Mine();
            if (car == null)
            {
                Refuse(preset, "no car of this player found", "No car of yours found");
                return;
            }
            var runner = Probe.Runner();
            if (runner == null)
            {
                Refuse(preset, "no Fusion runner (OverlandNetworkRunner.Instance.NetworkRunner)", "No game session for the trailer");
                return;
            }
            if (runner.IsClient && !runner.IsServer)
            {
                Refuse(preset, "this game is a client in someone's session", "Stage 2 is solo/host only for now");
                return;
            }
            if (!car.Stands(out float speed, out string how))
            {
                Refuse(preset, $"the car is moving ({(float.IsNaN(speed) ? "speed unknown" : $"{speed:0.00} m/s")} from {how})", "Stop the car for the trailer");
                return;
            }
            float tilt = Rig.Tilt(Rig.Q(car.Root.rotation));
            if (tilt > Rig.MaxSpawnTilt)
            {
                Refuse(preset, $"the car leans {tilt:0}° (at most {Rig.MaxSpawnTilt:0}°)", "The car leans too far for the trailer");
                return;
            }
            if (_rig != null)
                End(_rig.Car.Id == car.Id ? $"rebuilt as size {preset.Name}" : "moved to this player's car", null);
            var rig = Rig.Create(car, runner, preset, out string refusal, out string sayRefusal);
            if (rig == null)
            {
                Refuse(preset, refusal, sayRefusal);
                return;
            }
            _rig = rig;
            _size = size;
            Look.Say($"Test trailer {preset.Name} hitched");
        }

        private static void Refuse(Preset preset, string why, string say)
        {
            MelonLogger.Msg($"Test Trailer: size {preset.Name} not hitched: {why}");
            Look.Say(say);
        }
    }

    // One towed trailer: a GameObject of its own in the car's scene (not the car's child), a Rigidbody with one box
    // collider for the body, passive raycast wheels of the car's own size and grip (Wheel) stepped in Fusion's physics
    // step (the add-on's OnBeforeSimulate), a ConfigurableJoint ball hitch on the car's Rigidbody. Its space: the
    // coupling at the origin. What is seen is a separate object (Visuals): drawn from the drawn car, not the raw step.
    internal sealed class Rig
    {
        public const float MaxSpawnTilt = 20f;
        private const float MaxPitchFix = 25f, MaxRollFix = 20f, SpawnReach = 0.3f;
        private const float PitchLimit = 45f, YawSoftFrom = 85f, LimitHz = 2f, LimitDamping = 0.8f;
        // The roll stop (spec 10): free to ±25° against the car, then a spring and damper on both bodies, never more
        // than RollStopTimes × the trailer's weight × its half track nor half the car's righting moment
        private const float RollFree = 25f, RollStopTimes = 2f;
        private float _rollSpring, _rollDamper, _rollMaxTorque, _rollTorqueMax;
        private int _rollStopSteps;
        private const float OverloadTimes = 8f, OverloadSeconds = 0.5f, Jackknife = 110f, MaxTilt = 60f, MaxDrop = 2f, NoGroundSeconds = 2f;
        private const double FirstStepSeconds = 1.5, IgnoreEvery = 2.0;
        private const int DrawSample = 150;
        private static readonly string[] GroundLayers =
        {
            "Default", "Terrain", "Rock", "CampableRock", "Objects", "Lobby", "DefaultNotOccludingCamera", "DefaultAllowingPlacedTools",
        };
        private static readonly string[] RoomLayers = { "Default", "Car", "BrokenCar", "Tree", "Rock", "CampableRock", "Objects" };

        public readonly Car Car;
        public readonly Preset Preset;
        public readonly Layout Layout;
        public Rigidbody Body;
        public PhysicsScene Scene;
        public readonly Il2CppStructArray<RaycastHit> Hits = new(16);
        public int GroundMask;
        // The car's tyre (its rear left wheel's, as it drives now): lateral peak and limit friction, adherent speed
        public float TirePeak = 1f, TireSlide = 0.9f, AdherentSpeed = 0.3f;

        private readonly Transform _carRoot;
        private readonly VehicleBase _vehicle;
        private readonly NetworkRunner _runner;
        private readonly Measure.CarWheels _carWheels;
        private readonly Dictionary<int, bool> _ground = new();
        private readonly Dictionary<int, string> _groundNames = new();
        private readonly Dictionary<int, Collider> _colliders = new();
        private readonly HashSet<IntPtr> _carColliders = new();
        private Rigidbody _carBody;
        private float _carMass, _gravity;
        private N3 _hitchCar;
        private GameObject _go, _look;
        private Collider _box;
        private ConfigurableJoint _joint;
        private Wheel[] _wheels = Array.Empty<Wheel>();
        private readonly List<Object> _assets = new();
        private RunnerSimulatePhysicsBase _addOn;
        private Action _step, _after;
        private Il2CppSystem.Action _hook, _afterHook;
        private bool _subscribed, _afterSubscribed;
        private string _afterHow = "not recorded";
        private GroundMaterialManagerBase _manager;
        private bool _surfaceBroken;
        private float _fallbackGrip = 1f;
        private string _fallbackSurface = "?";

        // The step's own record, read in the frame
        private volatile string _stepFailure;
        private float _overload, _noGround;
        private int _steps, _stepsTotal;
        private long _stepTicks, _stepMaxTicks;
        private double _forceSum, _forceSq, _forceJump, _forcePrev = -1.0;
        private int _forceN;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _nextTelemetry = 1.0, _nextIgnore = IgnoreEvery;
        private readonly Max _max = new();

        // How far Fusion draws the car from its physics pose (sampled for the first frames)
        private int _drawFrames;
        private double _drawSum, _drawMax, _drawAngleSum;

        // How the trailer is drawn, chosen after the first 150 frames (Visuals)
        private enum Draw { Measuring, DrawnCar, OwnInterpolation, Raw }
        private Draw _draw = Draw.Measuring;
        private const float SamePose = 0.001f, MovesBetweenSteps = 0.2f;
        // The car as drawn: its root, or the child Fusion's NetworkRigidbody3D interpolates (InterpolationTarget) with
        // its pose relative to the root at hitching (the car standing: no interpolation offset)
        private Transform _carTarget;
        private N3 _targetRestPos;
        private NQ _targetRestRot = NQ.Identity;
        private string _carDrawnHow = "the car's root transform";
        // The bodies' poses after the last two physics steps (recorded at the start of each OnBeforeSimulate: the pose
        // the previous step left), and when the last one came
        private N3 _trailerPrevPos, _trailerCurPos, _carPrevPos, _carCurPos;
        private NQ _trailerPrevRot = NQ.Identity, _trailerCurRot = NQ.Identity, _carPrevRot = NQ.Identity, _carCurRot = NQ.Identity;
        private int _posesRecorded;
        private long _lastStepTicks;
        private float _lastDt = 0.02f;
        // Frames without a physics step since the last one, and how many of them still moved the drawn car
        private int _framesSteps = -1, _noStepFrames, _noStepMoves;
        private N3 _lastDrawnPos;
        private bool _alphaFailed;
        private string _alphaHow = "the runner's LocalAlpha";

        private sealed class Max
        {
            public float CarSpeed, TrailerSpeed, Yaw, TrailerRoll, CarRoll, CarPitch, HitchKn, StepMs, Spread;
            public int MinSteps = int.MaxValue;
        }

        private Rig(Car car, NetworkRunner runner, Preset preset, float ground, WheelSpec wheels, Measure.CarWheels carWheels)
        {
            Car = car;
            _carRoot = car.Root;
            _vehicle = car.Vehicle;
            _runner = runner;
            _carWheels = carWheels;
            Preset = preset;
            Layout = new Layout(preset, ground, wheels);
        }

        public static N3 N(Vector3 v) => new(v.x, v.y, v.z);
        public static Vector3 U(N3 v) => new(v.X, v.Y, v.Z);
        public static NQ Q(Quaternion q) => new(q.x, q.y, q.z, q.w);
        public static Quaternion U(NQ q) => new(q.X, q.Y, q.Z, q.W);
        private static readonly N3 Up = new(0f, 1f, 0f), Forward = new(0f, 0f, 1f), Right = new(1f, 0f, 0f);

        // How far a rotation's up is off the world's up, degrees
        public static float Tilt(NQ q) => Degrees(Math.Acos(Math.Clamp(N3.Transform(Up, q).Y, -1f, 1f)));

        private static float Degrees(double radians) => (float)(radians * 180.0 / Math.PI);

        // The car's own limit on a side slope, standing, for the log: its centre of mass over the ground under the coupling
        // (along the car's up) against half its track
        private string CarTipHow(N3 hitchWorld, Layout l)
        {
            try
            {
                var carUp = N3.Transform(Up, Q(_carBody.rotation));
                float height = N3.Dot(N(_carBody.worldCenterOfMass) - hitchWorld, carUp) - l.Ground;
                float half = _carWheels.Track / 2f;
                return height > 0.05f && half > 0.05f
                    ? $"{Degrees(Math.Atan2(half, height)):0.0}°, its centre of mass {height:0.00} m up, half its track {half:0.00} m"
                    : $"unread (centre of mass {height:0.00} m, half track {half:0.00} m)";
            }
            catch (Exception e)
            {
                return $"unread ({e.Message})";
            }
        }

        // --- making it ---------------------------------------------------------------------------------------------
        public static Rig Create(Car car, NetworkRunner runner, Preset preset, out string refusal, out string say)
        {
            var root = car.Root;
            var hitch = Measure.HitchOf(root);
            var ground = Measure.GroundOf(root);
            var carWheels = Measure.RearWheels(root);
            var spec = new WheelSpec
            {
                Radius = carWheels.Radius > 0.1f ? carWheels.Radius : preset.Radius,
                Width = carWheels.Width > 0.05f ? carWheels.Width : 0.25f,
                Travel = Math.Min(carWheels.Travel > 0.05f ? carWheels.Travel : 0.3f, WheelSpec.MaxTravel),
                Track = carWheels.Track,
                From = carWheels.Left != null || carWheels.Right != null ? "the car's rear wheels" : "the size's own (the car's rear wheels not found)",
            };
            var rig = new Rig(car, runner, preset, ground.Y - hitch.Point.Y, spec, carWheels) { _hitchCar = hitch.Point };
            try
            {
                if (!rig.Build(hitch, ground, out refusal, out say))
                {
                    rig.Destroy();
                    return null;
                }
                return rig;
            }
            catch
            {
                rig.Destroy();
                throw;
            }
        }

        private bool Build(Measure.Hitch hitch, Measure.Ground ground, out string refusal, out string say)
        {
            refusal = say = null;
            _carBody = _carRoot.GetComponent<Rigidbody>();
            if (_carBody == null)
            {
                refusal = "the car has no Rigidbody on its root";
                say = "This car cannot tow";
                return false;
            }
            _carMass = _carBody.mass;
            _gravity = Math.Abs(Physics.gravity.y);
            _addOn = Probe.AddOn(_runner, out string addOnWhere);
            if (_addOn == null)
            {
                refusal = "Fusion's physics add-on (RunnerSimulatePhysics3D) not found: the wheels could not step with the game's physics";
                say = "Test trailer: no physics step found";
                return false;
            }
            foreach (var name in GroundLayers)
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer >= 0)
                    GroundMask |= 1 << layer;
            }
            foreach (var collider in _carRoot.GetComponentsInChildren<Collider>(true))
                if (collider != null)
                    _carColliders.Add(collider.Pointer);
            // The Z drawbar's step from its line to the hitch: at most 0.6 m either way
            if (!Layout.StepFits)
            {
                float over = Math.Abs(Layout.Step) - Layout.MaxStep;
                string way = Layout.Step > 0f ? "high" : "low";
                refusal = $"the hitch is {Math.Abs(Layout.Step):0.00} m {(Layout.Step > 0f ? "over" : "under")} the drawbar's line, {over:0.00} m more than the Z's {Layout.MaxStep} m";
                say = $"The hitch is {over:0.00} m too {way} for this trailer";
                return false;
            }
            string tireHow = Tire();
            string surfaceHow = Surfaces();

            var l = Layout;
            var w = l.Wheels;
            var com = l.Com;
            float tonguePercent = l.TonguePercent;
            int n = l.Axles.Length * 2;
            float wheelLoad = Preset.Mass * (1f - tonguePercent / 100f) / n;
            var wheels = new List<Wheel>();
            foreach (float z in l.Axles)
                foreach (float side in new[] { -1f, 1f })
                    wheels.Add(new Wheel($"{(l.Axles.Length > 1 ? (z > l.AxleZ ? "front " : "rear ") : "")}{(side < 0f ? "left" : "right")}",
                        new N3(side * l.WheelX, l.HubY, z), w.Radius, w.Width, w.Travel, wheelLoad, _gravity));
            _wheels = wheels.ToArray();

            // The trailer's own object in the car's scene, the coupling on the hitch, the car's yaw (and lean, at first)
            _go = new GameObject("TestTrailer");
            SceneManager.MoveGameObjectToScene(_go, _carRoot.gameObject.scene);
            _go.layer = _carRoot.gameObject.layer;
            Scene = _go.scene.GetPhysicsScene();
            var carRotation = Q(_carRoot.rotation);
            var hitchWorld = N(_carRoot.TransformPoint(U(hitch.Point)));
            _go.transform.position = U(hitchWorld);
            _go.transform.rotation = U(carRotation);

            // Placement: pitched and rolled round the coupling until the wheels stand at their static compression on the
            // ground their rays find (twice, as the turn round the coupling moves them a little along too)
            float pitch = 0f, roll = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                var rotation = Rotation(carRotation, pitch, roll);
                var up = N3.Transform(Up, rotation);
                float sum = 0f, sumZ = 0f, left = 0f, right = 0f;
                foreach (var wheel in _wheels)
                {
                    var mount = hitchWorld + N3.Transform(wheel.Mount, rotation);
                    if (!Below(mount + up * 1.5f, -up, 4f, out float distance))
                    {
                        Dump(wheel, mount + up * 1.5f, -up, 4f);
                        refusal = $"no ground found under the {wheel.Name} wheel within 2.5 m of where it would stand";
                        say = "No ground behind the car for the trailer";
                        return false;
                    }
                    // How far the hub must rise to stand at its static compression on that ground
                    float rise = wheel.Radius + (wheel.Travel - wheel.StaticCompression) + 1.5f - distance;
                    sum += rise;
                    sumZ += wheel.Mount.Z;
                    if (wheel.Mount.X < 0f)
                        left += rise;
                    else
                        right += rise;
                }
                float meanRise = sum / _wheels.Length, meanZ = sumZ / _wheels.Length;
                pitch += Degrees(Math.Asin(Math.Clamp(meanRise / -meanZ, -1f, 1f)));
                roll += Degrees(Math.Asin(Math.Clamp((right - left) / (_wheels.Length / 2f) / (2f * l.WheelX), -1f, 1f)));
            }
            if (Math.Abs(pitch) > MaxPitchFix || Math.Abs(roll) > MaxRollFix)
            {
                refusal = $"the ground behind the car is too uneven: the trailer would stand pitched {pitch:0}° and rolled {roll:0}° to the car";
                say = "The ground behind the car is too uneven";
                return false;
            }
            var placed = Rotation(carRotation, pitch, roll);

            // Each wheel's own rays from where it will stand, reaching 0.3 m further than its travel (a slope): every one
            // must find ground. No wheel may start inside its bump stop's ramp: if one would (uneven ground under a
            // tandem), the trailer is pitched up round the coupling until the highest wheel is at most 0.03 m over its
            // static height (the others hang lower), or it is not made.
            float lift = 0f;
            Wheel highest = null;
            float highestRise = 0f;
            for (int attempt = 0; ; attempt++)
            {
                var up = N3.Transform(Up, placed);
                var forward = N3.Transform(Forward, placed);
                highest = null;
                highestRise = float.MinValue;
                foreach (var wheel in _wheels)
                {
                    var mount = hitchWorld + N3.Transform(wheel.Mount, placed);
                    if (!wheel.Find(this, mount, up, forward, SpawnReach))
                    {
                        Dump(wheel, mount + up * Wheel.Padding, -up, 4f);
                        refusal = $"the {wheel.Name} wheel's rays found no ground where the trailer would stand (looking {SpawnReach} m past its travel)";
                        say = "No ground under the trailer's wheels";
                        return false;
                    }
                    float rise = wheel.Compression - wheel.StaticCompression;
                    if (rise > highestRise)
                    {
                        highestRise = rise;
                        highest = wheel;
                    }
                }
                if (highestRise <= Wheel.StartRoom)
                    break;
                if (attempt >= 3)
                {
                    refusal = $"the ground behind the car is too uneven: the {highest.Name} wheel would start {highestRise * 100f:0.0} cm over its static height, inside its bump stop (from {Wheel.BumpFrom * 100f:0} cm)";
                    say = "The ground behind the car is too uneven";
                    return false;
                }
                float step = Degrees(Math.Asin(Math.Clamp(highestRise / Math.Max(0.5f, -highest.Mount.Z), -1f, 1f)));
                pitch += step;
                lift += step;
                placed = Rotation(carRotation, pitch, roll);
            }
            if (Math.Abs(pitch) > MaxPitchFix)
            {
                refusal = $"the ground behind the car is too uneven: the trailer would stand pitched {pitch:0}° to the car";
                say = "The ground behind the car is too uneven";
                return false;
            }
            var lines = new StringBuilder();
            foreach (float z in l.Axles)
            {
                lines.Append($"; axle at z {z:0.000}:");
                foreach (var wheel in _wheels)
                {
                    if (Math.Abs(wheel.Mount.Z - z) > 1e-3f)
                        continue;
                    wheel.Refresh(this);
                    lines.Append($" {(wheel.Mount.X < 0f ? "left" : "right")} '{GroundName(wheel.GroundId)}' hit {wheel.HitDistance:0.000} m, " +
                                 $"compression {wheel.Compression * 100f:0.0} cm (static {wheel.StaticCompression * 100f:0.0}), {wheel.Surface} grip {wheel.Grip:0.00}, " +
                                 $"friction {wheel.PeakMu:0.00}/{wheel.SlideMu:0.00}{(wheel.Mount.X < 0f ? ";" : "")}");
                }
            }
            if (lift != 0f)
                lines.Append($"; pitched up {lift:0.0}° more so no wheel starts in its bump stop");

            // Room: nothing in the body's volume from a little over the ground to its top, wheels included
            if (!Room(placed, hitchWorld, out string blocker))
            {
                refusal = $"no room behind the car: {blocker} in the way";
                say = $"No room for the trailer: {blocker}";
                return false;
            }

            // Looks, on their own object (Visuals places it every frame from the drawn car)
            string looksHow = Looks();
            CarDrawnSource();

            // The joint takes its zero angles from the bodies' relative rotation when it is connected: the trailer is made
            // at the car's own rotation (the coupling on the hitch), connected, and only then turned round the coupling
            // to its placed pose on the ground
            _go.transform.rotation = U(carRotation);

            // The body: one collider for the body only (its bottom the floor, on the frame over the axle), the car's layer:
            // on M and L a convex mesh of the chamfered body, so its ends do not catch where the chamfers are drawn
            // (spec 10); on S a box
            string colliderHow;
            if (l.Chamfered)
            {
                var hull = Look.Hull(l);
                _assets.Add(hull);
                var meshCollider = _go.AddComponent<MeshCollider>();
                meshCollider.convex = true;
                meshCollider.sharedMesh = hull;
                _box = meshCollider;
                colliderHow = $"convex mesh of the chamfered body ({hull.vertexCount} vertices, chamfers {l.ChamferSize:0.00} m)";
            }
            else
            {
                var box = _go.AddComponent<BoxCollider>();
                box.center = U(l.BodyCentre);
                box.size = U(l.BodySize);
                _box = box;
                colliderHow = $"box {Measure.F(l.BodySize)} at {Measure.F(l.BodyCentre)}";
            }
            Body = _go.AddComponent<Rigidbody>();
            Body.mass = Preset.Mass;
            Body.centerOfMass = U(com);
            Body.inertiaTensorRotation = new Quaternion(0f, 0f, 0f, 1f);
            var inertia = Inertia(com);
            string bumpHow = Bumps(com, inertia);
            Body.inertiaTensor = U(inertia);
            // As the car: in Script mode Unity's interpolation is likely ignored (the drawn trailer is Visuals' job)
            Body.interpolation = RigidbodyInterpolation.None;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.solverIterations = 12;
            Body.solverVelocityIterations = 4;
            Body.maxDepenetrationVelocity = 1f;
            Body.maxAngularVelocity = 7f;
            Body.linearDamping = 0.04f;
            Body.angularDamping = 0.12f;

            // Never touching the towing car (set again every 2 s for parts fitted later), nor the ground's terrain
            // colliders (all found at hitching; later ones when a wheel stands on them): trees, rocks, cars and objects still meet it
            string ignoreLine = IgnoreCar(true);
            string terrainLine = IgnoreTerrain(true);

            // The hitch: a ball, its anchors set by hand and meeting at once. Twist axis = up, so the joint's X is the
            // yaw: free to ±85°, a soft stop past it (its own spring from the yaw inertia round the hitch). Secondary
            // axis = right, so its Y is the pitch, ±30° soft. Its Z (up × right, along the trailer) is the roll: free.
            float pitchInertia = inertia.X + Preset.Mass * (com.Y * com.Y + com.Z * com.Z);
            float yawInertia = inertia.Y + Preset.Mass * (com.X * com.X + com.Z * com.Z);
            float wn = 2f * (float)Math.PI * LimitHz;
            float pitchSpring = pitchInertia * wn * wn, pitchDamper = 2f * LimitDamping * (float)Math.Sqrt(pitchSpring * pitchInertia);
            float yawSpring = yawInertia * wn * wn, yawDamper = 2f * LimitDamping * (float)Math.Sqrt(yawSpring * yawInertia);
            // The roll stop's spring from the roll inertia round the drawbar's line (forward through the coupling)
            float rollInertia = inertia.Z + Preset.Mass * (com.X * com.X + com.Y * com.Y);
            _rollSpring = rollInertia * wn * wn;
            _rollDamper = 2f * LimitDamping * (float)Math.Sqrt(_rollSpring * rollInertia);
            // capped by the trailer's own (twice its weight × its half track) and by half the car's righting moment (its
            // weight × half its track), so the trailer can never throw the car over
            float carRighting = _carMass * _gravity * Math.Max(0.3f, _carWheels.Track / 2f);
            _rollMaxTorque = Math.Min(RollStopTimes * Preset.Mass * _gravity * l.WheelX, 0.5f * carRighting);
            _joint = _go.AddComponent<ConfigurableJoint>();
            _joint.autoConfigureConnectedAnchor = false;
            _joint.anchor = new Vector3(0f, 0f, 0f);
            _joint.axis = new Vector3(0f, 1f, 0f);
            _joint.secondaryAxis = new Vector3(1f, 0f, 0f);
            _joint.connectedBody = _carBody;
            _joint.connectedAnchor = U(_hitchCar);
            _joint.xMotion = ConfigurableJointMotion.Locked;
            _joint.yMotion = ConfigurableJointMotion.Locked;
            _joint.zMotion = ConfigurableJointMotion.Locked;
            _joint.angularXMotion = ConfigurableJointMotion.Limited;    // twist round up: yaw, a soft stop at ±85°
            _joint.angularYMotion = ConfigurableJointMotion.Limited;    // round right: pitch, ±30° soft
            _joint.angularZMotion = ConfigurableJointMotion.Free;       // round up × right (along the trailer): roll
            _joint.lowAngularXLimit = new SoftJointLimit { m_Limit = -YawSoftFrom };
            _joint.highAngularXLimit = new SoftJointLimit { m_Limit = YawSoftFrom };
            _joint.angularXLimitSpring = new SoftJointLimitSpring { m_Spring = yawSpring, m_Damper = yawDamper };
            _joint.angularYLimit = new SoftJointLimit { m_Limit = PitchLimit };
            _joint.angularYZLimitSpring = new SoftJointLimitSpring { m_Spring = pitchSpring, m_Damper = pitchDamper };
            _joint.enableCollision = false;
            string jointNotes = "";
            try
            {
                _joint.enablePreprocessing = true;
            }
            catch (Exception e)
            {
                jointNotes += $"; enablePreprocessing not set ({e.Message})";
            }
            _joint.projectionMode = JointProjectionMode.PositionAndRotation;
            _joint.projectionDistance = 0.05f;
            _joint.projectionAngle = 5f;

            // Now to the placed pose, round the coupling (the root's origin): the joint's zero stays the car's pose
            _go.transform.rotation = U(placed);
            Body.rotation = U(placed);
            Body.position = U(hitchWorld);

            // At rest with the car: the car's velocity at the coupling, its spin
            var spin = _carBody.angularVelocity;
            Body.angularVelocity = spin;
            var atHitch = N(_carBody.GetPointVelocity(U(hitchWorld)));
            var worldCom = hitchWorld + N3.Transform(com, placed);
            Body.linearVelocity = U(atHitch - N3.Cross(N(spin), hitchWorld - worldCom));

            // The step's dt: the add-on's own (DeltaTime × DeltaTimeMultiplier) if readable, else the runner's DeltaTime
            string dtHow;
            try
            {
                float own = _addOn.PhysicsSimulationDeltaTime;
                _dtFromAddOn = own > 0f && own < 1f;
                dtHow = _dtFromAddOn ? $"the add-on's PhysicsSimulationDeltaTime ({own:0.0000} s)" : $"the runner's DeltaTime (the add-on's reads {own})";
            }
            catch (Exception e)
            {
                _dtFromAddOn = false;
                dtHow = $"the runner's DeltaTime (the add-on's PhysicsSimulationDeltaTime unread: {e.Message})";
            }

            // In step with Fusion: the wheels once per physics step, from the add-on's OnBeforeSimulate
            try
            {
                _step = Step;
                _hook = _step;
                _addOn.add_OnBeforeSimulate(_hook);
                _subscribed = true;
                // The poses after each finished step, for the trailer's own interpolation (Visuals)
                try
                {
                    _after = After;
                    _afterHook = _after;
                    _addOn.add_OnAfterSimulate(_afterHook);
                    _afterSubscribed = true;
                    _afterHow = "recorded in the add-on's OnAfterSimulate";
                }
                catch (Exception e)
                {
                    _afterHow = $"recorded at the start of OnBeforeSimulate (OnAfterSimulate not joined: {e.Message})";
                }
            }
            catch (Exception e)
            {
                refusal = $"could not join Fusion's physics step (RunnerSimulatePhysicsBase.add_OnBeforeSimulate): {e.Message}";
                say = "Test trailer: could not join the physics step";
                return false;
            }
            Visuals();

            MelonLogger.Msg($"Test Trailer: size {Preset.Name} hitched to '{Car.Name}' ({_carMass:0} kg): {Preset.Mass:0} kg, body {Preset.Length} x {Preset.Width} x {Preset.Height} m " +
                            $"{(l.Chamfered ? $"(lower edges chamfered {l.ChamferSize:0.00} m, the collider too; the rails as long as the flat bottom, square ends)" : "")}; " +
                            $"floor {l.FloorY - l.Ground:0.000} m over the ground on its axle{(l.Axles.Length > 1 ? "s" : "")} (axle centre {l.AxleY - l.Ground:0.000} m + springs {Layout.Springs} + frame {Layout.Frame}), " +
                            $"drawbar line {l.LineY - l.Ground:0.000} m, step {l.Step:+0.000;-0.000;0.000} m over {Math.Abs(l.Step):0.000} m (Z, 45°), the hitch {-l.Ground:0.000} m over the ground; " +
                            $"coupling at {Measure.F(_hitchCar)} car space – {hitch.Source}; ground ({ground.How})");
            MelonLogger.Msg($"Test Trailer: wheels from {w.From}: radius {w.Radius:0.000} m, width {w.Width:0.000} m, travel {w.Travel:0.00} m (the car's {_carWheels.Travel:0.00}, at most {WheelSpec.MaxTravel}), " +
                            $"{_wheels.Length} of them at ±{l.WheelX:0.000} m (the car's track {w.Track:0.000}), axle{(l.Axles.Length > 1 ? "s" : "")} at z {string.Join(", ", Array.ConvertAll(l.Axles, z => z.ToString("0.000")))}{(l.Axles.Length > 1 ? $" ({l.Spacing:0.00} m apart: the wheel's diameter + {Layout.TandemGap} m, at least {Layout.TandemSpacing})" : "")}; " +
                            $"springs {_wheels[0].SpringOmega / (2f * Math.PI):0.00} Hz on the load (ω {_wheels[0].SpringOmega:0.00} rad/s, sag {_wheels[0].StaticCompression:0.000} m{(_wheels[0].SpringOmega > Wheel.Omega + 1e-3f ? ", stiffened: the travel is short" : "")}); looks: {looksHow}");
            MelonLogger.Msg($"Test Trailer: wheels: side from the car's wheel ({_carWheels.SideHow}), base rotation identity (left) / 180° about up (right); " +
                            $"{(_wheelNotes.Count > 0 ? string.Join("; ", _wheelNotes) : "no copies")}; scale the source's absolute; spin about the axle each drawn frame from the ground speed; " +
                            $"bump stop at +{Wheel.BumpRise:0.00} m over static ({bumpHow}), fenders {Layout.FenderGap} m over the static wheel top");
            MelonLogger.Msg($"Test Trailer: grip: tyre {tireHow}; surfaces {surfaceHow}; friction used = the tyre's lateral peak (sticking) or limit (sliding) friction × the surface's grip");
            MelonLogger.Msg($"Test Trailer: mass: CoM {Measure.F(com)} trailer space, {com.Y - l.FloorY:0.000} m over the floor ({(com.Y - l.FloorY) / Preset.Height:0.00} of the body's height; " +
                            $"chassis {Layout.ChassisShare:P0}, shell {Layout.ShellShare:P0}, load {Layout.LoadShare:P0} packed {l.LoadHeight:0.00} m high), {com.Y - l.Ground:0.000} m over the ground; " +
                            $"turns over on a side slope at {Degrees(Math.Atan2(l.WheelX, com.Y - l.Ground)):0.0}° standing (the car: {CarTipHow(hitchWorld, l)}), " +
                            $"static tow-ball load {tonguePercent:0.0} % ({Preset.Mass * tonguePercent / 100f:0} kg), {wheelLoad:0} kg a wheel; inertia {Measure.F(inertia)} (boxes); " +
                            $"interpolation None, ContinuousSpeculative, solver 12/4, maxDepenetrationVelocity 1, maxAngularVelocity 7; " +
                            $"collider: {colliderHow}; {ignoreLine}; {terrainLine}");
            MelonLogger.Msg($"Test Trailer: hitch: ball joint, axis up (joint X = yaw: free to ±{YawSoftFrom:0}°, then a soft stop, spring {yawSpring:0} damper {yawDamper:0} from {yawInertia:0} kg·m²; " +
                            $"detached past {Jackknife:0}°), secondary axis right (joint Y = pitch ±{PitchLimit:0}° soft, spring {pitchSpring:0} damper {pitchDamper:0} from {pitchInertia:0} kg·m²; " +
                            $"{LimitHz} Hz, ζ {LimitDamping}), Z = roll free in the joint; roll stop: free to ±{RollFree:0}° against the car, then {_rollSpring:0} N·m/rad, " +
                            $"{_rollDamper:0} N·m·s/rad from {rollInertia:0} kg·m² round the drawbar's line, the spring at most {_rollMaxTorque:0} N·m, with the damper {1.5f * _rollMaxTorque:0} (the car's righting moment {carRighting:0}), on the trailer and back on the car; projection position+rotation 0.05 m / 5°; connectedMassScale 1{jointNotes}; " +
                            $"the joint's zero at the car's pose, the trailer then placed pitched {pitch:0.0}° and rolled {roll:0.0}° to the car");
            MelonLogger.Msg($"Test Trailer: wheels at hitching{lines}");
            MelonLogger.Msg($"Test Trailer: steps with Fusion: {_addOn.GetIl2CppType().Name}.OnBeforeSimulate ({addOnWhere}), dt {dtHow}; the poses for drawing {_afterHow}; " +
                            $"ground layers 0x{GroundMask:X}");
            return true;
        }

        // Each wheel's bump stop from its effective vertical mass – the body free: 1 / (1/M + z²/Ix + x²/Iz) for the wheel's
        // place round the centre of mass – and the physics step's dt
        private string Bumps(N3 com, N3 inertia)
        {
            float dt = 0.02f;
            try
            {
                float read = _runner.DeltaTime;
                if (read > 0f && read < 1f)
                    dt = read;
            }
            catch (Exception)
            {
                // 50 Hz, the game's
            }
            float least = float.MaxValue;
            Wheel weakest = null;
            foreach (var wheel in _wheels)
            {
                var r = wheel.Mount - com;
                float mass = 1f / (1f / Preset.Mass + r.Z * r.Z / Math.Max(1f, inertia.X) + r.X * r.X / Math.Max(1f, inertia.Z));
                wheel.SetBump(mass, dt);
                if (mass < least)
                {
                    least = mass;
                    weakest = wheel;
                }
            }
            return weakest == null ? "no wheels"
                : $"a linear ramp from +{Wheel.BumpFrom:0.00} m, stiffness {weakest.BumpStiffness:0} N/m and damper {weakest.BumpDamper:0} N·s/m (ζ {Wheel.BumpZeta}) " +
                  $"from the smallest effective wheel mass {least:0} kg ({weakest.Name}), ω·dt {weakest.BumpOmegaDtActual:0.00} at dt {dt:0.000} s; " +
                  $"all of a wheel's force at most {Wheel.MaxTimes} × its weight";
        }

        private static NQ Rotation(NQ car, float pitch, float roll) =>
            car * NQ.CreateFromAxisAngle(Right, pitch * (float)Math.PI / 180f) * NQ.CreateFromAxisAngle(Forward, roll * (float)Math.PI / 180f);

        // The boxes' inertia round the centre of mass (Trailer Lab's BoxInertia), as the mass is split (spec 10): the
        // chassis with its axles and wheels (20 %) at the rails' middle, the shell (15 %) as the body's box, the load
        // (65 %) packed on the floor
        private N3 Inertia(N3 com)
        {
            var l = Layout;
            var chassis = new N3(2f * l.WheelX + l.Wheels.Width, 0.4f, Preset.Length);
            var load = new N3(Preset.Width, l.LoadHeight, Preset.Length);
            return BoxInertia(Layout.ChassisShare * Preset.Mass, chassis, new N3(0f, l.LineY, l.BodyCentre.Z) - com)
                   + BoxInertia(Layout.ShellShare * Preset.Mass, l.BodySize, l.BodyCentre - com)
                   + BoxInertia(Layout.LoadShare * Preset.Mass, load, new N3(0f, l.FloorY + l.LoadHeight / 2f, l.BodyCentre.Z) - com);
        }

        private static N3 BoxInertia(float mass, N3 size, N3 offset)
        {
            var m = size * size / 12f + offset * offset;
            return mass * new N3(m.Y + m.Z, m.X + m.Z, m.X + m.Y);
        }

        // --- grip ----------------------------------------------------------------------------------------------------
        // The car's tyre: its rear left wheel's friction as the car drives now (VehicleBase.GetWheelTireFriction, a
        // TireFrictionStandard; TireShop may have changed it), else its tyre data asset. VPP's own slip-to-force model
        // (GetTireForce with the wheel's ContactPatch) is native and wants the car's per-wheel state, so the trailer uses
        // the same coefficients in a stick-slip model instead: lateral peak friction while sticking, limit friction while
        // sliding, sticking again under the max adherent velocity.
        private string Tire()
        {
            string how = "defaults (peak 1.0, limit 0.9, adherent 0.3 m/s): the car's tyre unread";
            int index = -1;
            Il2CppReferenceArray<VehicleBase.WheelState> states = null;
            try
            {
                states = _vehicle != null ? _vehicle.m_wheelState : null;
                var rearLeft = _carWheels.Left;
                for (int i = 0; states != null && rearLeft != null && i < states.Length; i++)
                {
                    var state = states[i];
                    if (state != null && state.wheelCol != null && state.wheelCol.Pointer == rearLeft.Pointer)
                        index = i;
                }
                TireFrictionStandard.Settings settings = null;
                string from = null;
                if (index >= 0)
                {
                    var friction = _vehicle.GetWheelTireFriction(index);
                    var standard = friction != null ? friction.TryCast<TireFrictionStandard>() : null;
                    settings = standard != null ? standard.settings : null;
                    from = settings != null ? $"the car's wheel {index} (rear left) as it drives now"
                        : $"the car's wheel {index} has {(friction != null ? friction.GetIl2CppType().Name : "no friction")}";
                }
                if (settings == null)
                {
                    var controller = _carRoot.GetComponent<VPVehicleController>();
                    var data = controller != null ? controller.tireData : null;
                    settings = data != null ? data.tireSettings : null;
                    from = (from != null ? from + "; " : "") + (settings != null ? "the car's tyre data asset" : "no tyre data asset");
                }
                if (settings != null && settings.lateralPeakFriction > 0.05f && settings.lateralPeakFriction < 3f)
                {
                    TirePeak = settings.lateralPeakFriction;
                    TireSlide = settings.lateralLimitFriction > 0.05f ? Math.Min(settings.lateralLimitFriction, TirePeak) : TirePeak * 0.9f;
                    AdherentSpeed = Math.Clamp(settings.maxAdherentVelocity, 0.05f, 1f);
                    how = $"{from}: lateral peak {settings.lateralPeakFriction:0.000} at {settings.lateralPeakSlip:0.0}°, limit {settings.lateralLimitFriction:0.000} at {settings.lateralLimitSlip:0.0}°, " +
                          $"max adherent velocity {settings.maxAdherentVelocity:0.00} m/s, nominal load {settings.nominalLoad:0} N";
                }
                else
                    how = $"{from}: {how}";
            }
            catch (Exception e)
            {
                how += $" ({e.GetType().Name}: {e.Message})";
            }
            // The car's own grip on its own ground now, to compare with what the trailer's wheels read
            try
            {
                if (index >= 0)
                {
                    var state = states[index];
                    var grip = state.dynamicGrip;
                    var config = grip != null ? grip.GroundMaterial : null;
                    _fallbackGrip = grip != null && grip.Grip > 0f ? grip.Grip : 1f;
                    _fallbackSurface = $"the car's own: {(config != null ? config.name : "?")}";
                    how += $"; the car's rear left now: tireForceMult {state.tireForceMult:0.000}, grip {(grip != null ? grip.Grip : 0f):0.000} " +
                           $"(target {(grip != null ? grip.TargetGrip : 0f):0.000}, tyre tier +{(grip != null ? grip.TierAdditionalGrip : 0f):0.000}) on '{(config != null ? config.name : "?")}' " +
                           $"(its Grip {(config != null ? config.Grip : 0f):0.000})";
                }
            }
            catch (Exception e)
            {
                how += $"; the car's own grip unread ({e.Message})";
            }
            return how;
        }

        private string Surfaces()
        {
            try
            {
                _manager = _vehicle != null ? _vehicle.groundMaterialManager : null;
                return _manager != null ? $"from the car's ground material manager ({_manager.GetIl2CppType().Name}.GetGroundMaterial at each wheel's contact)"
                    : "unread: the car has no ground material manager, every wheel takes the car's own grip";
            }
            catch (Exception e)
            {
                _manager = null;
                return $"unread ({e.Message}): every wheel takes the car's own grip";
            }
        }

        // The surface under a wheel: the game's own ground material for its collider and point, its Grip (a failure once
        // and every wheel takes the car's own grip from then on). GetGroundMaterial hands back a GroundMaterial the car's
        // wheels share and sets its current terrain layer and physic material (its native code is not readable), so the
        // writable fields of every material the car can be reading – its wheels' own, the manager's fallback, any the
        // trailer was handed before – are saved before the call and put back right after it.
        private GroundMaterialHit _hit;
        private readonly Dictionary<IntPtr, GroundMaterial> _materials = new();
        private readonly List<(GroundMaterial material, TerrainLayer layer, PhysicsMaterial physic, Il2CppSystem.Object custom)> _saved = new();

        public void Surface(Wheel wheel)
        {
            if (_surfaceBroken || _manager == null)
            {
                wheel.Grip = _fallbackGrip;
                wheel.Surface = _fallbackSurface;
                return;
            }
            try
            {
                if (!_colliders.TryGetValue(wheel.GroundId, out var collider) || collider == null)
                {
                    wheel.Grip = _fallbackGrip;
                    wheel.Surface = "?";
                    return;
                }
                _hit ??= new GroundMaterialHit();
                _hit.collider = collider;
                _hit.physicMaterial = collider.sharedMaterial;
                _hit.hitPoint = U(wheel.Contact);
                Save();
                GroundMaterial material;
                try
                {
                    material = _manager.GetGroundMaterial(_vehicle, _hit);
                    var config = material != null ? material.Config : null;
                    if (config == null)
                    {
                        wheel.Grip = _fallbackGrip;
                        wheel.Surface = "no ground material";
                    }
                    else
                    {
                        wheel.Grip = config.Grip > 0f ? config.Grip : _fallbackGrip;
                        var layer = material.CurrentTerrainLayer;
                        wheel.Surface = config.name + (layer != null ? "/" + layer.name : "");
                    }
                }
                finally
                {
                    Restore();
                }
                if (material != null && !_materials.ContainsKey(material.Pointer))
                    _materials[material.Pointer] = material;
            }
            catch (Exception e)
            {
                _surfaceBroken = true;
                wheel.Grip = _fallbackGrip;
                wheel.Surface = _fallbackSurface;
                MelonLogger.Warning($"Test Trailer: the ground material unread ({e.GetType().Name}: {e.Message}); every wheel takes the car's own grip ({_fallbackGrip:0.00}) from now");
            }
        }

        private void Save()
        {
            _saved.Clear();
            var seen = new HashSet<IntPtr>();
            void Add(GroundMaterial m)
            {
                if (m == null || !seen.Add(m.Pointer))
                    return;
                _saved.Add((m, m.CurrentTerrainLayer, m.CurrentPhysicMaterial, m.customData));
            }
            var states = _vehicle.m_wheelState;
            for (int i = 0; states != null && i < states.Length; i++)
                if (states[i] != null)
                    Add(states[i].groundMaterial);
            Add(_manager.fallback);
            foreach (var m in _materials.Values)
                Add(m);
        }

        private void Restore()
        {
            foreach (var (material, layer, physic, custom) in _saved)
            {
                material.CurrentTerrainLayer = layer;
                material.CurrentPhysicMaterial = physic;
                material.customData = custom;
            }
            _saved.Clear();
        }

        // --- looks ----------------------------------------------------------------------------------------------------
        // The body and drawbar mesh, and the car's own wheels as it wears them now (its rear left and right drawn wheels
        // copied, colliders taken off), on an object of their own
        private readonly List<Transform> _axleLooks = new();
        private readonly List<(int axle, float side, float end, Transform look)> _leaves = new();
        // What the copies dropped of their sources, for the log
        private readonly List<string> _wheelNotes = new();

        private string Looks()
        {
            _look = new GameObject("TestTrailerLook");
            SceneManager.MoveGameObjectToScene(_look, _carRoot.gameObject.scene);
            _look.layer = _carRoot.gameObject.layer;
            _look.transform.position = _go.transform.position;
            _look.transform.rotation = _go.transform.rotation;
            var material = Look.MakeMaterial(_carRoot, out string materialHow);
            _assets.Add(material);
            var bodyMesh = Look.ToMesh(Look.Body(Layout), "TestTrailer");
            _assets.Add(bodyMesh);
            Part("Body", bodyMesh, material, _look.transform);
            Mesh fallback = null;
            int copied = 0;
            foreach (var wheel in _wheels)
            {
                var holder = new GameObject("Wheel " + wheel.Name);
                holder.layer = _look.layer;
                holder.transform.SetParent(_look.transform, false);
                wheel.Visual = holder.transform;
                bool left = wheel.Mount.X < 0f;
                var source = left ? _carWheels.LeftLook : _carWheels.RightLook;
                if (CopyWheel(source, left ? _carWheels.LeftUpdater : _carWheels.RightUpdater, left, holder.transform))
                {
                    copied++;
                    continue;
                }
                if (fallback == null)
                {
                    fallback = Look.ToMesh(Look.Wheel(wheel.Radius, wheel.Width), "TestTrailerWheel");
                    _assets.Add(fallback);
                }
                Part("Tyre", fallback, material, holder.transform);
            }
            // The axles with their leaf springs, drawn with the wheels: they follow the compression
            var axleMesh = Look.ToMesh(Look.Axle(Layout), "TestTrailerAxle");
            _assets.Add(axleMesh);
            _axleLooks.Clear();
            for (int i = 0; i < Layout.Axles.Length; i++)
                _axleLooks.Add(Part($"Axle {i + 1}", axleMesh, material, _look.transform).transform);
            var unit = Look.ToMesh(Look.Unit(), "TestTrailerLeaf");
            _assets.Add(unit);
            _leaves.Clear();
            for (int i = 0; i < Layout.Axles.Length; i++)
                foreach (float side in new[] { -1f, 1f })
                    foreach (float end in new[] { -1f, 1f })
                        _leaves.Add((i, side, end, Part($"Leaf {i + 1} {(side < 0f ? "left" : "right")} {(end < 0f ? "back" : "front")}", unit, material, _look.transform).transform));
            return $"{copied} of {_wheels.Length} wheels the car's own drawn wheels ({_stripped} other components taken off the copies, {_blocks.Count} renderers' property blocks copied)" +
                   $"{(copied < _wheels.Length ? $", {_wheels.Length - copied} plain" : "")}; material {materialHow}";
        }

        // The copy is made under an inactive holder (so nothing in it wakes), stripped of every component but Transform,
        // LODGroup, MeshFilter and MeshRenderer, then shown; the source renderers' property blocks (the livery, the dirt
        // the game sets on the car's renderers) are copied over now and every 2 s
        private static readonly HashSet<string> Kept = new() { "Transform", "LODGroup", "MeshFilter", "MeshRenderer" };
        private readonly List<(MeshRenderer source, MeshRenderer copy)> _blocks = new();
        private MaterialPropertyBlock _block;
        private int _stripped;

        // The copy's base turn: a left wheel's drawn node as on the car (identity), a right one's turned 180° about up,
        // as every car's prefab has its right LODGroup node; nothing of the live wheel's camber, toe or spin
        private const float SpinInAir = 0.5f;

        private static readonly NQ RightTurn = NQ.CreateFromAxisAngle(new N3(0f, 1f, 0f), (float)Math.PI);

        private bool CopyWheel(Transform source, WheelTransformUpdater updater, bool left, Transform holder)
        {
            if (source == null)
                return false;
            GameObject shell = null;
            try
            {
                shell = new GameObject("Copy");
                shell.layer = holder.gameObject.layer;
                shell.SetActive(false);
                shell.transform.SetParent(holder, false);
                var made = Object.Instantiate(source.gameObject, shell.transform, false);
                var copy = made != null ? made.TryCast<GameObject>() : null;
                if (copy == null)
                {
                    Object.Destroy(shell);
                    return false;
                }
                copy.name = source.gameObject.name;
                var t = copy.transform;
                t.localPosition = new Vector3(0f, 0f, 0f);
                // Its turn: the side's base, nothing of the live wheel's (the axle stays along x, so the spin about x
                // does not wobble); its size the absolute of the source's (a mirroring negative scale dropped)
                var baseTurn = left ? NQ.Identity : RightTurn;
                t.localRotation = U(baseTurn);
                var scale = source.lossyScale;
                t.localScale = new Vector3(Math.Abs(scale.x), Math.Abs(scale.y), Math.Abs(scale.z));
                _wheelNotes.Add(Dropped(source, updater, copy.transform, baseTurn, left));
                foreach (var component in copy.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || Kept.Contains(component.GetIl2CppType().Name))
                        continue;
                    try
                    {
                        Object.DestroyImmediate(component);
                        _stripped++;
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Warning($"Test Trailer: a '{component.GetIl2CppType().Name}' left on the copied wheel: {e.Message}");
                    }
                }
                var sources = source.GetComponentsInChildren<MeshRenderer>(true);
                var copies = copy.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < sources.Length && i < copies.Length; i++)
                    if (sources[i] != null && copies[i] != null)
                        _blocks.Add((sources[i], copies[i]));
                shell.SetActive(true);
                CopyBlocks();
                return true;
            }
            catch (Exception e)
            {
                if (shell != null)
                    Object.Destroy(shell);
                MelonLogger.Warning($"Test Trailer: the car's wheel '{source.name}' not copied: {e.Message}");
                return false;
            }
        }

        // What the live source carried that the copy drops: its camber and toe (its axle's tilt from the car's x, the
        // side's base taken off), and the turn of the node the game's WheelTransformUpdater moves (wheelTransform) and
        // of every node between it and the copied LODGroup, reset to none in the copy where they are part of it
        private string Dropped(Transform source, WheelTransformUpdater updater, Transform copy, NQ baseTurn, bool left)
        {
            var relative = NQ.Conjugate(Q(_carRoot.rotation)) * Q(source.rotation) * NQ.Conjugate(baseTurn);
            var axle = N3.Transform(new N3(1f, 0f, 0f), relative);
            float camber = Degrees(Math.Asin(Math.Clamp(axle.Y, -1f, 1f)));
            float toe = Degrees(Math.Atan2(axle.Z, axle.X));
            string reset = "no wheelTransform";
            try
            {
                var moved = updater != null ? updater.wheelTransform : null;
                if (moved != null)
                {
                    if (moved.Pointer == source.Pointer || moved.IsChildOf(source))
                    {
                        // inside the copy: the same path there, each node from it up to (not including) the copy's root
                        var path = new List<string>();
                        for (var n = moved; n != null && n.Pointer != source.Pointer; n = n.parent)
                            path.Insert(0, n.name);
                        var node = path.Count > 0 ? copy.Find(string.Join("/", path)) : null;
                        int count = 0;
                        for (; node != null && node.Pointer != copy.Pointer; node = node.parent)
                        {
                            node.localRotation = new Quaternion(0f, 0f, 0f, 1f);
                            count++;
                        }
                        reset = $"wheelTransform '{moved.name}' inside the copy, {count} node{(count == 1 ? "" : "s")} reset";
                    }
                    else
                        reset = $"wheelTransform '{moved.name}' above the copied node (not copied)";
                }
            }
            catch (Exception e)
            {
                reset = $"wheelTransform unread ({e.Message})";
            }
            return $"{(left ? "left" : "right")} from '{source.parent?.name}': camber {camber:+0.0;-0.0;0.0}° and toe {toe:+0.0;-0.0;0.0}° of the source dropped, {reset}";
        }

        private void CopyBlocks()
        {
            _block ??= new MaterialPropertyBlock();
            foreach (var (source, copy) in _blocks)
            {
                if (source == null || copy == null)
                    continue;
                source.GetPropertyBlock(_block);
                copy.SetPropertyBlock(_block);
            }
        }

        private GameObject Part(string name, Mesh mesh, Material material, Transform parent)
        {
            var part = new GameObject(name);
            part.layer = parent.gameObject.layer;
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return part;
        }

        // --- ground and room ---------------------------------------------------------------------------------------------
        // A ray down for the placement: the nearest ground hit
        private bool Below(N3 origin, N3 direction, float length, out float distance)
        {
            distance = float.MaxValue;
            int count = Scene.Raycast(U(origin), U(direction), Hits, length, GroundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && i < Hits.Length; i++)
            {
                var hit = Hits[i];
                if (hit.m_Distance < distance && hit.m_Normal.y >= 0.45f && IsGround(hit))
                    distance = hit.m_Distance;
            }
            return distance < float.MaxValue;
        }

        // Every hit of a ray on every layer, for a wheel that found no ground: what the ray meets
        private void Dump(Wheel wheel, N3 origin, N3 direction, float length)
        {
            var text = new StringBuilder();
            try
            {
                foreach (var hit in Physics.RaycastAll(U(origin), U(direction), length, ~0, QueryTriggerInteraction.Collide))
                {
                    var c = hit.collider;
                    if (c == null)
                        continue;
                    var body = c.attachedRigidbody;
                    text.Append($"; '{c.name}' layer {c.gameObject.layer} {LayerMask.LayerToName(c.gameObject.layer)} at {hit.distance:0.000} m" +
                                $"{(c.isTrigger ? " (trigger)" : "")}{(c.transform.IsChildOf(_carRoot) ? " (the towing car)" : "")}" +
                                $"{(body != null ? $", body '{body.name}'{(body.isKinematic ? " kinematic" : " dynamic")}" : ", no body")}");
                }
            }
            catch (Exception e)
            {
                text.Append($"; the ray failed: {e.Message}");
            }
            MelonLogger.Msg($"Test Trailer: the {wheel.Name} wheel found no ground; every hit from {Measure.F(origin)} down {length} m on all layers" +
                            (text.Length > 0 ? text.ToString() : ": none"));
        }

        // Whether a hit is ground the wheels may stand on: not the trailer, not the towing car, no loose body (cached by
        // the collider's instance id, with the collider for the ground material)
        public bool IsGround(RaycastHit hit)
        {
            int id = hit.m_Collider;
            if (_ground.TryGetValue(id, out bool ok))
                return ok;
            var collider = hit.collider;
            ok = false;
            if (collider != null && (_box == null || collider.Pointer != _box.Pointer) && !_carColliders.Contains(collider.Pointer)
                && !collider.transform.IsChildOf(_carRoot))
            {
                var body = collider.attachedRigidbody;
                ok = body == null || (body.isKinematic && (Body == null || body.Pointer != Body.Pointer) && body.Pointer != _carBody.Pointer);
            }
            _ground[id] = ok;
            if (collider != null && collider.TryCast<TerrainCollider>() != null && !_terrain.Contains(collider.GetInstanceID()))
                _pendingTerrain.Add(collider);
            if (collider != null)
            {
                _colliders[id] = collider;
                _groundNames[id] = $"{collider.name}/{LayerMask.LayerToName(collider.gameObject.layer)}";
            }
            return ok;
        }

        private string GroundName(int id) => _groundNames.TryGetValue(id, out var name) ? name : "?";

        // Room: nothing in the trailer's volume. Two bands: low (from 0.15 m over the ground under the trailer) without
        // Default, as bumpy ground meshes are on it; from 0.35 m up all the room layers (Default, Car, BrokenCar, Tree,
        // Rock, CampableRock, Objects). The towing car and any test trailer (one going this very frame) do not count.
        private const float LowBand = 0.15f, DefaultFrom = 0.35f;

        private bool Room(NQ rotation, N3 hitchWorld, out string blocker)
        {
            var l = Layout;
            int all = 0;
            foreach (var name in RoomLayers)
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer >= 0)
                    all |= 1 << layer;
            }
            int noDefault = all & ~(1 << Math.Max(0, LayerMask.NameToLayer("Default")));
            float top = l.FloorY + Preset.Height;
            return Clear(rotation, hitchWorld, l.Ground + LowBand, l.Ground + DefaultFrom, noDefault, out blocker)
                   && Clear(rotation, hitchWorld, l.Ground + DefaultFrom, top, all, out blocker);
        }

        private bool Clear(NQ rotation, N3 hitchWorld, float bottom, float top, int mask, out string blocker)
        {
            blocker = null;
            var l = Layout;
            var centre = new N3(0f, (bottom + top) / 2f, l.BodyCentre.Z);
            var half = new N3(l.WheelX + l.Wheels.Width / 2f - 0.05f, Math.Max(0.01f, (top - bottom) / 2f - 0.02f), Preset.Length / 2f - 0.05f);
            var hits = new Il2CppReferenceArray<Collider>(32);
            int n = Physics.OverlapBoxNonAlloc(U(hitchWorld + N3.Transform(centre, rotation)), U(half), hits, U(rotation), mask, QueryTriggerInteraction.Ignore);
            float groundY = (hitchWorld + N3.Transform(new N3(0f, l.Ground, l.BodyCentre.Z), rotation)).Y;
            for (int i = 0; i < n; i++)
            {
                var hit = hits[i];
                if (hit == null || _carColliders.Contains(hit.Pointer) || hit.transform.IsChildOf(_carRoot) || hit.transform.root.name == "TestTrailer")
                    continue;
                float over = hit.bounds.max.y - groundY;
                blocker = $"'{hit.name}' ({LayerMask.LayerToName(hit.gameObject.layer)}, its top {over:0.00} m over the ground under the trailer)";
                return false;
            }
            return true;
        }

        // --- what the box does not touch -----------------------------------------------------------------------------------
        // Physics.IgnoreCollision between the box and every collider of the car not yet ignored (by instance id); it and
        // Physics.GetIgnoreCollision are calls the game itself never makes (restored by the interop), so a failure is
        // said and the trailer goes on, the box then able to touch the car in a jackknife
        private readonly HashSet<int> _ignored = new();
        private bool _ignoreFailed;

        private string IgnoreCar(bool first)
        {
            if (_ignoreFailed || _box == null)
                return "collisions with the car not ignored";
            int added = 0;
            Collider sample = null;
            foreach (var collider in _carRoot.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null)
                    continue;
                _carColliders.Add(collider.Pointer);
                if (!_ignored.Add(collider.GetInstanceID()))
                    continue;
                try
                {
                    Physics.IgnoreCollision(_box, collider, true);
                    added++;
                    sample ??= collider;
                }
                catch (Exception e)
                {
                    _ignoreFailed = true;
                    string text = $"Physics.IgnoreCollision failed ({e.Message}): the box can touch the car";
                    if (!first)
                        MelonLogger.Warning($"Test Trailer: {text}");
                    return text;
                }
            }
            if (!first)
            {
                if (added > 0)
                    MelonLogger.Msg($"Test Trailer: collisions with {added} more of the car's colliders ignored (parts fitted since)");
                return null;
            }
            string check;
            try
            {
                check = sample == null ? "no pair to check"
                    : $"Physics.GetIgnoreCollision(box, '{sample.name}') {Physics.GetIgnoreCollision(_box, sample)}";
            }
            catch (Exception e)
            {
                check = $"Physics.GetIgnoreCollision unread ({e.Message})";
            }
            return $"collisions with {added} of the car's colliders ignored ({check})";
        }

        // The ground's terrain colliders: the box never meets the ground (its wheels carry it), so a long box does not
        // catch a bump with its edges. Found by type (Object.FindObjectsOfType(TerrainCollider)) once at hitching; after
        // that only a terrain collider a wheel stands on (KeepIgnoring); cached by instance id.
        private readonly HashSet<int> _terrain = new();
        private readonly List<Collider> _pendingTerrain = new();
        private bool _terrainFailed;

        private string IgnoreTerrain(bool first)
        {
            if (_ignoreFailed || _terrainFailed || _box == null)
                return "the terrain not ignored";
            int added = 0, found = 0;
            try
            {
                var all = Object.FindObjectsOfType(Il2CppType.Of<TerrainCollider>());
                for (int i = 0; all != null && i < all.Length; i++)
                {
                    var collider = all[i] != null ? all[i].TryCast<Collider>() : null;
                    if (collider == null)
                        continue;
                    found++;
                    if (!_terrain.Add(collider.GetInstanceID()))
                        continue;
                    Physics.IgnoreCollision(_box, collider, true);
                    added++;
                }
            }
            catch (Exception e)
            {
                string text = $"the terrain not ignored ({e.GetType().Name}: {e.Message}): the box can meet the ground";
                _terrainFailed = true;
                if (!first)
                    MelonLogger.Warning($"Test Trailer: {text}");
                return text;
            }
            if (!first)
            {
                if (added > 0)
                    MelonLogger.Msg($"Test Trailer: collisions with {added} more terrain colliders ignored (terrain loaded since)");
                return null;
            }
            return $"collisions with {added} terrain colliders ignored ({found} found)";
        }

        // Every 2 s: the ignore for colliders the car got since, the wheels' property blocks copied again; every frame:
        // a terrain collider a wheel stands on that is not ignored yet (no search of the scene after hitching)
        public void KeepIgnoring()
        {
            double now = _clock.Elapsed.TotalSeconds;
            if (now >= _nextIgnore)
            {
                _nextIgnore = now + IgnoreEvery;
                IgnoreCar(false);
                CopyBlocks();
            }
            if (_pendingTerrain.Count == 0 || _terrainFailed || _ignoreFailed || _box == null)
                return;
            int added = 0;
            try
            {
                foreach (var collider in _pendingTerrain)
                {
                    if (collider == null || !_terrain.Add(collider.GetInstanceID()))
                        continue;
                    Physics.IgnoreCollision(_box, collider, true);
                    added++;
                }
            }
            catch (Exception e)
            {
                _terrainFailed = true;
                MelonLogger.Warning($"Test Trailer: a terrain collider not ignored ({e.Message}): the box can meet the ground");
            }
            _pendingTerrain.Clear();
            if (added > 0)
                MelonLogger.Msg($"Test Trailer: collisions with {added} more terrain collider{(added > 1 ? "s" : "")} ignored (a wheel's ground)");
        }

        private bool _dtFromAddOn, _dtTold;

        // The step's dt: the add-on's own if it was readable at hitching (falling back for good if it fails later)
        private float Dt()
        {
            if (_dtFromAddOn)
            {
                try
                {
                    return _addOn.PhysicsSimulationDeltaTime;
                }
                catch (Exception e)
                {
                    _dtFromAddOn = false;
                    if (!_dtTold)
                    {
                        _dtTold = true;
                        MelonLogger.Warning($"Test Trailer: the add-on's PhysicsSimulationDeltaTime failed ({e.Message}); the runner's DeltaTime from now");
                    }
                }
            }
            return _runner.DeltaTime;
        }

        // --- the physics step ----------------------------------------------------------------------------------------
        // Called by Fusion's physics add-on right before it steps the scene: the wheels' forces for this step. Nothing
        // may escape from here into the game's step: a failure is noted and the frame detaches the trailer.
        private void Step()
        {
            if (!_subscribed || _stepFailure != null)
                return;
            long start = Stopwatch.GetTimestamp();
            try
            {
                if (Body == null || _go == null)
                    return;
                // The car gone in mid-frame: the joint would read its car-space anchor as a world position from now on.
                // The trailer stops being simulated (kinematic), the joint goes, and the frame detaches it.
                // Out of the physics at once (the body and the joint with it), never kinematic while jointed
                if (_carBody == null)
                {
                    _stepFailure = "the car's body is gone";
                    _go.SetActive(false);
                    return;
                }
                // The car moved by the game between two steps (recovered, reset, flipped): the trailer goes before the
                // joint can yank either of them across the jump
                var carPos = N(_carBody.position);
                var carRot = Q(_carBody.rotation);
                if (_carSeen)
                {
                    float jump = N3.Distance(carPos, _carLastPos);
                    float turn = Degrees(2.0 * Math.Acos(Math.Min(1.0, Math.Abs(NQ.Dot(carRot, _carLastRot)))));
                    if (jump > TeleportDistance || turn > TeleportAngle)
                    {
                        _stepFailure = $"the car was moved by the game ({jump:0.0} m, {turn:0}° in one step)";
                        _go.SetActive(false);
                        return;
                    }
                }
                _carSeen = true;
                _carLastPos = carPos;
                _carLastRot = carRot;
                float dt = Dt();
                var position = N(Body.position);
                var rotation = Q(Body.rotation);
                _lastDt = dt;
                if (!_afterSubscribed)
                    Record(position, rotation, carPos, carRot);
                JointAngles(carRot, rotation);
                RollStop(carRot, rotation);
                var up = N3.Transform(Up, rotation);
                var forward = N3.Transform(Forward, rotation);
                bool any = false;
                foreach (var wheel in _wheels)
                {
                    wheel.Step(this, position, rotation, up, forward, dt);
                    any |= wheel.Grounded;
                }
                _noGround = any ? 0f : _noGround + dt;
                if (Force(out var f))
                {
                    double force = Math.Sqrt(f.x * f.x + f.y * f.y + f.z * f.z);
                    _overload = force > OverloadTimes * Preset.Mass * _gravity ? _overload + dt : 0f;
                    _forceSum += force;
                    _forceSq += force * force;
                    _forceN++;
                    if (_forcePrev >= 0.0)
                        _forceJump = Math.Max(_forceJump, Math.Abs(force - _forcePrev));
                    _forcePrev = force;
                }
                _steps++;
                _stepsTotal++;
            }
            catch (Exception e)
            {
                _stepFailure = $"the physics step failed: {e.GetType().Name}: {e.Message}";
                MelonLogger.Error($"Test Trailer: {_stepFailure}\n{e}");
            }
            long ticks = Stopwatch.GetTimestamp() - start;
            _stepTicks += ticks;
            _stepMaxTicks = Math.Max(_stepMaxTicks, ticks);
        }

        // The trailer's turn against the car each step, as the joint sees it (its zero is the car's pose): yaw round the
        // car's up, pitch (the trailer's front up, +), roll (its right side up, +); the second's largest pitch and roll.
        // Whether Unity's PhysX joint measures the swing in the car's frame or the trailer's is not in the game's code
        // (native), so the off-camber run shows from these whether the pitch limit catches the roll at large turns.
        private float _jointYaw, _jointPitch, _jointRoll, _jointPitchMax, _jointRollMax;

        private void JointAngles(NQ car, NQ trailer)
        {
            var relative = NQ.Conjugate(car) * trailer;
            var forward = N3.Transform(Forward, relative);
            var right = N3.Transform(Right, relative);
            var up = N3.Transform(Up, relative);
            _jointYaw = Degrees(Math.Atan2(forward.X, forward.Z));
            _jointPitch = Degrees(Math.Asin(Math.Clamp(forward.Y, -1f, 1f)));
            _jointRoll = Degrees(Math.Atan2(right.Y, up.Y));
            _jointPitchMax = Math.Max(_jointPitchMax, Math.Abs(_jointPitch));
            _jointRollMax = Math.Max(_jointRollMax, Math.Abs(_jointRoll));
        }

        // The roll stop: θ the trailer's turn round its forward axis that would bring its up onto the car's (the car's up
        // seen across that axis); past ±25° a torque along that axis turns the trailer back toward the car and the same
        // the other way turns the car toward the trailer, damped on their relative roll rate, capped
        private void RollStop(NQ carRot, NQ rotation)
        {
            var forward = N3.Transform(Forward, rotation);
            var up = N3.Transform(Up, rotation);
            var carUp = N3.Transform(Up, carRot);
            carUp -= forward * N3.Dot(carUp, forward);
            if (carUp.LengthSquared() < 1e-6f)
                return;
            carUp = N3.Normalize(carUp);
            float theta = (float)Math.Atan2(N3.Dot(N3.Cross(up, carUp), forward), N3.Dot(up, carUp));
            float free = RollFree * (float)Math.PI / 180f;
            float excess = Math.Abs(theta) - free;
            if (excess <= 0f)
                return;
            float rate = N3.Dot(N(Body.angularVelocity) - N(_carBody.angularVelocity), forward);
            // the spring capped on its own, so the damper still works when it is; the total at most 1.5 × the cap, and
            // never pulling the trailer outward on its way back in
            float spring = Math.Clamp(_rollSpring * Math.Sign(theta) * excess, -_rollMaxTorque, _rollMaxTorque);
            float torque = Math.Clamp(spring - _rollDamper * rate, -1.5f * _rollMaxTorque, 1.5f * _rollMaxTorque);
            if (Math.Sign(torque) != Math.Sign(theta))
                torque = 0f;
            Body.AddTorque(U(forward * torque));
            _carBody.AddTorque(U(forward * -torque));
            _rollTorqueMax = Math.Max(_rollTorqueMax, Math.Abs(torque));
            _rollStopSteps++;
        }

        private const float TeleportDistance = 2f, TeleportAngle = 30f;
        private bool _carSeen;
        private N3 _carLastPos;
        private NQ _carLastRot = NQ.Identity;

        // Called by the add-on right after it stepped the scene: the poses that step left
        private void After()
        {
            if (!_subscribed || _stepFailure != null)
                return;
            try
            {
                if (Body == null || _go == null || _carBody == null)
                    return;
                Record(N(Body.position), Q(Body.rotation), N(_carBody.position), Q(_carBody.rotation));
            }
            catch (Exception e)
            {
                _stepFailure = $"recording the poses after the step failed: {e.GetType().Name}: {e.Message}";
            }
        }

        private void Record(N3 trailerPos, NQ trailerRot, N3 carPos, NQ carRot)
        {
            _trailerPrevPos = _trailerCurPos;
            _trailerPrevRot = _trailerCurRot;
            _carPrevPos = _carCurPos;
            _carPrevRot = _carCurRot;
            _trailerCurPos = trailerPos;
            _trailerCurRot = trailerRot;
            _carCurPos = carPos;
            _carCurRot = carRot;
            _posesRecorded++;
            _lastStepTicks = Stopwatch.GetTimestamp();
        }

        // The joint's force (Joint.currentForce: a call the game never makes, restored by the interop). If it fails once,
        // it is not read again: the overload check and the hitch numbers then go without it, and the log says so.
        private bool _forceFailed;

        private bool Force(out Vector3 force)
        {
            force = new Vector3(0f, 0f, 0f);
            if (_forceFailed || _joint == null)
                return false;
            try
            {
                force = _joint.currentForce;
                return true;
            }
            catch (Exception e)
            {
                _forceFailed = true;
                MelonLogger.Warning($"Test Trailer: the hitch force unread (Joint.currentForce: {e.Message}); no overload check and no hitch numbers from now");
                return false;
            }
        }

        // --- every frame ------------------------------------------------------------------------------------------------
        // Why the trailer must go now, or null
        public string Check()
        {
            if (!Car.Alive)
                return "the car is gone";
            if (_go == null || Body == null)
                return "the trailer was destroyed";
            if (_stepFailure != null)
                return _stepFailure;
            if (_stepsTotal == 0 && _clock.Elapsed.TotalSeconds > FirstStepSeconds)
                return $"Fusion's physics step never called the wheels in {FirstStepSeconds} s (OnBeforeSimulate silent)";
            if (_overload >= OverloadSeconds)
                return $"the hitch force stayed over {OverloadTimes} times the trailer's weight ({OverloadTimes * Preset.Mass * _gravity / 1000f:0.0} kN) for {OverloadSeconds} s";
            if (_noGround >= NoGroundSeconds)
                return $"no wheel on the ground for {NoGroundSeconds} s";
            var car = Q(_carRoot.rotation);
            var trailer = Q(_go.transform.rotation);
            float yaw = Math.Abs(Yaw(car, trailer));
            if (yaw > Jackknife)
                return $"jackknifed ({yaw:0}° between car and trailer, past the soft stop at {YawSoftFrom:0}°)";
            float trailerTilt = Tilt(trailer), carTilt = Tilt(car);
            if (trailerTilt > MaxTilt)
                return $"the trailer turned over ({trailerTilt:0}° off upright)";
            if (carTilt > MaxTilt)
                return $"the car turned over ({carTilt:0}° off upright)";
            float drop = _carRoot.TransformPoint(U(_hitchCar)).y - _go.transform.position.y;
            if (drop > MaxDrop)
                return $"the trailer's coupling fell {drop:0.0} m below the car's hitch";
            return null;
        }

        private static float Yaw(NQ car, NQ trailer)
        {
            var a = N3.Transform(Forward, car);
            var b = N3.Transform(Forward, trailer);
            return Degrees(Math.Atan2(a.X * b.Z - a.Z * b.X, a.X * b.X + a.Z * b.Z));
        }

        private static float Roll(NQ q) => Degrees(Math.Asin(Math.Clamp(N3.Transform(Right, q).Y, -1f, 1f)));

        private static float Pitch(NQ q) => Degrees(Math.Asin(Math.Clamp(N3.Transform(Forward, q).Y, -1f, 1f)));

        // Once a second: the line of numbers, and the run's largest for the summary
        public void Telemetry()
        {
            double now = _clock.Elapsed.TotalSeconds;
            if (now < _nextTelemetry)
                return;
            _nextTelemetry = now + 1.0;
            var car = Q(_carRoot.rotation);
            var trailer = Q(_go.transform.rotation);
            float carSpeed = _carBody.linearVelocity.magnitude, trailerSpeed = Body.linearVelocity.magnitude;
            float yaw = Yaw(car, trailer), trailerRoll = Roll(trailer), carRoll = Roll(car), carPitch = Pitch(car);
            bool read = Force(out var force);
            float kn = (float)Math.Sqrt(force.x * force.x + force.y * force.y + force.z * force.z) / 1000f, verticalKg = force.y / _gravity;
            // The hitch force's spread over the second's steps, and its largest jump from one step to the next
            double mean = _forceN > 0 ? _forceSum / _forceN : 0.0;
            float spread = _forceN > 1 ? (float)Math.Sqrt(Math.Max(0.0, _forceSq / _forceN - mean * mean)) / 1000f : 0f;
            float jump = (float)_forceJump / 1000f;
            _forceSum = _forceSq = _forceJump = 0.0;
            _forceN = 0;
            int steps = _steps;
            double stepMs = steps > 0 ? _stepTicks * 1000.0 / Stopwatch.Frequency / steps : 0.0;
            double maxMs = _stepMaxTicks * 1000.0 / Stopwatch.Frequency;
            _steps = 0;
            _stepTicks = 0;
            _stepMaxTicks = 0;
            var wheels = new StringBuilder();
            foreach (var wheel in _wheels)
                wheels.Append($"; {wheel.Name} ").Append(wheel.Grounded
                    ? $"{GroundName(wheel.GroundId)} {wheel.Compression * 100f:0.0} cm{(wheel.AtBumpStop ? " AT BUMP STOP" : "")} {wheel.Force:0} N, {wheel.Surface} grip {wheel.Grip:0.00} μ {(wheel.Sliding ? wheel.SlideMu : wheel.PeakMu):0.00} " +
                      $"{(wheel.Sliding ? "sliding" : "sticking")} {wheel.Offset * 100f:+0.0;-0.0;0.0} cm, slip {wheel.LateralSlip:+0.00;-0.00;0.00} m/s"
                    : "air");
            MelonLogger.Msg($"Test Trailer telemetry: {Preset.Name}, car {carSpeed * 3.6f:0.0} km/h, trailer {trailerSpeed * 3.6f:0.0} km/h, angle {yaw:+0.0;-0.0;0.0}°, " +
                            $"trailer roll {trailerRoll:+0.0;-0.0;0.0}°, car roll {carRoll:+0.0;-0.0;0.0}° pitch {carPitch:+0.0;-0.0;0.0}°, " +
                            $"hitch {(read ? $"{kn:0.00} kN (vertical {verticalKg:0} kg), spread {spread:0.00} kN, largest step-to-step {jump:0.00} kN" : "unread")}" +
                            $"; joint yaw {_jointYaw:+0.0;-0.0;0.0}° pitch {_jointPitch:+0.0;-0.0;0.0}° roll {_jointRoll:+0.0;-0.0;0.0}° to the car " +
                            $"(this second's largest pitch {_jointPitchMax:0.0}°, roll {_jointRollMax:0.0}°; pitch limit ±{PitchLimit:0}°)" +
                            $"{(_rollStopSteps > 0 ? $"; roll stop {_rollTorqueMax:0} N·m at most, {_rollStopSteps} steps" : "")}" +
                            $"{wheels}; {steps} steps, {stepMs:0.000} ms a step (at most {maxMs:0.000})");
            _jointPitchMax = 0f;
            _jointRollMax = 0f;
            _rollTorqueMax = 0f;
            _rollStopSteps = 0;
            _max.CarSpeed = Math.Max(_max.CarSpeed, carSpeed);
            _max.TrailerSpeed = Math.Max(_max.TrailerSpeed, trailerSpeed);
            _max.Yaw = Math.Max(_max.Yaw, Math.Abs(yaw));
            _max.TrailerRoll = Math.Max(_max.TrailerRoll, Math.Abs(trailerRoll));
            _max.CarRoll = Math.Max(_max.CarRoll, Math.Abs(carRoll));
            _max.CarPitch = Math.Max(_max.CarPitch, Math.Abs(carPitch));
            _max.HitchKn = Math.Max(_max.HitchKn, kn);
            _max.Spread = Math.Max(_max.Spread, spread);
            _max.StepMs = Math.Max(_max.StepMs, (float)maxMs);
            if (now > 1.5)
                _max.MinSteps = Math.Min(_max.MinSteps, steps);
        }

        private bool _telemetryTold;

        public void TelemetryFailed(Exception e)
        {
            if (_telemetryTold)
                return;
            _telemetryTold = true;
            MelonLogger.Warning($"Test Trailer: the telemetry or the ignores failed (said once; the checks go on): {e}");
        }

        public void Summary(string why)
        {
            MelonLogger.Msg($"Test Trailer: size {Preset.Name} on '{Car.Name}' ended after {_clock.Elapsed.TotalSeconds:0} s – {why}. Largest: car {_max.CarSpeed * 3.6f:0.0} km/h, " +
                            $"trailer {_max.TrailerSpeed * 3.6f:0.0} km/h, angle {_max.Yaw:0.0}°, trailer roll {_max.TrailerRoll:0.0}°, car roll {_max.CarRoll:0.0}° " +
                            $"pitch {_max.CarPitch:0.0}°, hitch {_max.HitchKn:0.00} kN (spread {_max.Spread:0.00}), step {_max.StepMs:0.000} ms; fewest steps in a second " +
                            $"{(_max.MinSteps == int.MaxValue ? "-" : _max.MinSteps.ToString())}; {_stepsTotal} steps in all");
        }

        // Where Fusion draws the car: its NetworkRigidbody3D's InterpolationTarget when it has one other than the root
        // (Fusion then moves that child, the root staying at the body's pose), else the root
        private void CarDrawnSource()
        {
            try
            {
                var rigidbody = _carRoot.GetComponent<NetworkRigidbody3D>();
                if (rigidbody == null)
                {
                    _carDrawnHow = "the car's root transform (no NetworkRigidbody3D on it)";
                    return;
                }
                var target = rigidbody.InterpolationTarget;
                string interpolate;
                try
                {
                    interpolate = rigidbody._doNotInterpolate ? ", its _doNotInterpolate set" : "";
                }
                catch (Exception)
                {
                    interpolate = "";
                }
                if (target == null || target.Pointer == _carRoot.Pointer)
                {
                    _carDrawnHow = $"the car's root transform (its NetworkRigidbody3D has no interpolation target of its own{interpolate})";
                    return;
                }
                _carTarget = target;
                var rootRot = Q(_carRoot.rotation);
                _targetRestRot = NQ.Conjugate(rootRot) * Q(target.rotation);
                _targetRestPos = N3.Transform(N(target.position) - N(_carRoot.position), NQ.Conjugate(rootRot));
                _carDrawnHow = $"the car's NetworkRigidbody3D interpolation target '{target.name}' (at {Measure.F(_targetRestPos)} from the root at hitching{interpolate})";
            }
            catch (Exception e)
            {
                _carTarget = null;
                _carDrawnHow = $"the car's root transform (its NetworkRigidbody3D unread: {e.Message})";
            }
        }

        // The car's drawn pose, as its root would stand
        private void CarDrawn(out N3 pos, out NQ rot)
        {
            if (_carTarget != null)
            {
                var targetRot = Q(_carTarget.rotation);
                rot = NQ.Normalize(targetRot * NQ.Conjugate(_targetRestRot));
                pos = N(_carTarget.position) - N3.Transform(_targetRestPos, rot);
                return;
            }
            pos = N(_carRoot.position);
            rot = Q(_carRoot.rotation);
        }

        // How far between the last two physics steps the frame is drawn: the runner's LocalAlpha, else the time since the
        // last step over its dt
        private float Alpha()
        {
            if (!_alphaFailed)
            {
                try
                {
                    float alpha = _runner.LocalAlpha;
                    if (alpha >= 0f && alpha <= 1.0001f)
                        return Math.Min(alpha, 1f);
                    _alphaFailed = true;
                    _alphaHow = $"the time since the last step over its dt (LocalAlpha read {alpha})";
                    if (_draw == Draw.OwnInterpolation)
                        MelonLogger.Msg($"Test Trailer: drawing: alpha from {_alphaHow}");
                }
                catch (Exception e)
                {
                    _alphaFailed = true;
                    _alphaHow = $"the time since the last step over its dt (LocalAlpha unread: {e.Message})";
                    if (_draw == Draw.OwnInterpolation)
                        MelonLogger.Msg($"Test Trailer: drawing: alpha from {_alphaHow}");
                }
            }
            double since = (Stopwatch.GetTimestamp() - _lastStepTicks) / (double)Stopwatch.Frequency;
            return (float)Math.Clamp(since / Math.Max(1e-3f, _lastDt), 0.0, 1.0);
        }

        // What is seen, every LateUpdate. By the mode chosen after the first 150 frames:
        // - drawn car: carDrawn × inverse(carPhysics) × trailerPhysics – Fusion draws the car away from its body's pose
        //   (interpolated), the trailer follows it, the ball on the drawn tow bar;
        // - own interpolation: the car's drawn pose reads as its body's, but it moves between physics steps (the body's
        //   pose itself reads interpolated): both bodies' last two step poses interpolated by the frame's alpha, and
        //   carDrawn × inverse(carInterpolated) × trailerInterpolated;
        // - raw: the car is drawn at its body's step pose, not interpolated: the trailer the same (the same formula),
        //   interpolating the trailer alone would pull the ball off the car.
        // The wheels at their hubs as the suspension has them, turned as far as they have rolled.
        public void Visuals()
        {
            if (_look == null || Body == null || _carBody == null)
                return;
            CarDrawn(out var carDrawnPos, out var carDrawnRot);
            var carPhysPos = N(_carBody.position);
            var carPhysRot = Q(_carBody.rotation);
            var trailerPos = N(Body.position);
            var trailerRot = Q(Body.rotation);
            if (_draw == Draw.OwnInterpolation && _posesRecorded >= 2)
            {
                float alpha = Alpha();
                carPhysPos = N3.Lerp(_carPrevPos, _carCurPos, alpha);
                carPhysRot = NQ.Slerp(_carPrevRot, _carCurRot, alpha);
                trailerPos = N3.Lerp(_trailerPrevPos, _trailerCurPos, alpha);
                trailerRot = NQ.Slerp(_trailerPrevRot, _trailerCurRot, alpha);
            }
            var offset = carDrawnRot * NQ.Conjugate(carPhysRot);
            var pos = carDrawnPos + N3.Transform(trailerPos - carPhysPos, offset);
            var rot = NQ.Normalize(offset * trailerRot);
            _look.transform.SetPositionAndRotation(U(pos), U(rot));
            if (_draw == Draw.Measuring)
                Sample(carDrawnPos, carDrawnRot, N(_carBody.position), Q(_carBody.rotation), _carBody.linearVelocity.magnitude > MeasureSpeed);
            // The wheels: each turned on by this frame's ground speed at it (smooth at any frame rate), about the axle
            float frame = Time.deltaTime;
            var forward = N3.Transform(Forward, trailerRot);
            foreach (var wheel in _wheels)
            {
                if (wheel.Visual == null)
                    continue;
                var hub = wheel.Hub;
                // On the ground (and for half a second after leaving it) it turns at the ground speed; then it slows
                wheel.AirTime = wheel.Grounded ? 0f : wheel.AirTime + frame;
                if (wheel.AirTime <= SpinInAir)
                {
                    var v = Body.GetPointVelocity(U(trailerPos + N3.Transform(hub, trailerRot)));
                    float speed = v.x * forward.X + v.y * forward.Y + v.z * forward.Z;
                    wheel.SpinSpeed = speed / wheel.Radius * (180f / (float)Math.PI);
                }
                else
                    wheel.SpinSpeed *= (float)Math.Exp(-frame / SpinInAir);
                wheel.Spin = (wheel.Spin + wheel.SpinSpeed * frame) % 360f;
                wheel.Visual.localPosition = U(hub);
                float half = wheel.Spin * (float)Math.PI / 360f;
                wheel.Visual.localRotation = new Quaternion((float)Math.Sin(half), 0f, 0f, (float)Math.Cos(half));
            }
            // The leaf springs: their ends under the hangers, their middles on the axle under each rail, never higher
            // than just under the rails
            var l = Layout;
            float endY = l.AxleY + AxleLook.SpringTop - AxleLook.Leaf / 2f, topY = l.RailBottom - AxleLook.Leaf / 2f - 0.005f;
            foreach (var (index, side, end, leaf) in _leaves)
            {
                if (leaf == null || 2 * index + 1 >= _wheels.Length)
                    continue;
                var a = _wheels[2 * index].Hub;
                var b = _wheels[2 * index + 1].Hub;
                float x = side * l.RailX, t = Math.Abs(b.X - a.X) > 1e-3f ? (x - a.X) / (b.X - a.X) : 0.5f;
                float axleY = a.Y + (b.Y - a.Y) * t, z = l.Axles[index];
                var middle = new N3(x, Math.Min(axleY + AxleLook.SpringTop - AxleLook.Leaf / 2f, topY), z);
                var tip = new N3(x, endY, z + end * AxleLook.SpringHalf);
                var d = middle - tip;
                float length = d.Length();
                float angle = (float)Math.Atan2(-d.Y, d.Z * -end) * -end;
                leaf.localPosition = U((middle + tip) / 2f);
                leaf.localRotation = new Quaternion((float)Math.Sin(angle / 2f), 0f, 0f, (float)Math.Cos(angle / 2f));
                leaf.localScale = new Vector3(AxleLook.LeafWidth, AxleLook.Leaf, Math.Max(0.01f, length + 0.02f));
            }
            // The axles between their wheels' hubs, rolled with them
            for (int i = 0; i < _axleLooks.Count && 2 * i + 1 < _wheels.Length; i++)
            {
                var a = _wheels[2 * i].Hub;
                var b = _wheels[2 * i + 1].Hub;
                var axle = _axleLooks[i];
                axle.localPosition = U((a + b) / 2f);
                float roll = (float)Math.Atan2(b.Y - a.Y, b.X - a.X);
                axle.localRotation = new Quaternion(0f, 0f, (float)Math.Sin(roll / 2f), (float)Math.Cos(roll / 2f));
            }
        }

        // The first 150 frames: how far the drawn car is from its body, and whether it moves in frames without a step
        // Only frames with the car moving faster than 1 m/s count (a standing car is drawn at its body's pose whatever
        // Fusion does), as many as it takes, with no time limit while towing; the drawn-car formula until then
        private const float MeasureSpeed = 1f;

        private void Sample(N3 drawnPos, NQ drawnRot, N3 physPos, NQ physRot, bool carMoving)
        {
            bool noStep = _framesSteps >= 0 && _stepsTotal == _framesSteps;
            bool moved = N3.Distance(drawnPos, _lastDrawnPos) > SamePose;
            _framesSteps = _stepsTotal;
            _lastDrawnPos = drawnPos;
            if (!carMoving)
                return;
            double distance = N3.Distance(drawnPos, physPos);
            double angle = 2.0 * Math.Acos(Math.Min(1.0, Math.Abs(NQ.Dot(NQ.Normalize(drawnRot), NQ.Normalize(physRot))))) * 180.0 / Math.PI;
            _drawSum += distance;
            _drawMax = Math.Max(_drawMax, distance);
            _drawAngleSum += angle;
            if (noStep)
            {
                _noStepFrames++;
                if (moved)
                    _noStepMoves++;
            }
            if (++_drawFrames < DrawSample)
                return;
            double mean = _drawSum / DrawSample;
            float moving = _noStepFrames > 0 ? (float)_noStepMoves / _noStepFrames : 0f;
            string why;
            if (mean >= SamePose)
            {
                _draw = Draw.DrawnCar;
                why = "Fusion draws the car away from its body's pose, so the trailer is drawn from the drawn car";
            }
            else if (_noStepFrames > 0 && moving >= MovesBetweenSteps)
            {
                _draw = Draw.OwnInterpolation;
                Alpha();
                why = $"the drawn car is at its body's pose but moves between physics steps ({_noStepMoves} of {_noStepFrames} frames without a step): " +
                      $"the trailer interpolates its own and the car's last two step poses by {_alphaHow}";
            }
            else
            {
                _draw = Draw.Raw;
                why = _noStepFrames == 0
                    ? "the drawn car is at its body's pose and every frame had a physics step: nothing to interpolate, the trailer drawn at its step pose with the car"
                    : $"the drawn car is at its body's pose and does not move between physics steps ({_noStepMoves} of {_noStepFrames} frames without a step): " +
                      "the car itself is not interpolated, so interpolation cannot help – the trailer drawn at its step pose with the car";
            }
            string sync;
            try
            {
                sync = Physics.autoSyncTransforms.ToString();
            }
            catch (Exception e)
            {
                sync = $"unread ({e.Message})";
            }
            MelonLogger.Msg($"Test Trailer: drawing: the car drawn from {_carDrawnHow}, {mean * 100.0:0.00} cm (at most {_drawMax * 100.0:0.00}) and " +
                            $"{_drawAngleSum / DrawSample:0.00}° from its rigid body's pose on average over {DrawSample} frames with the car faster than {MeasureSpeed} m/s " +
                            $"(after {_clock.Elapsed.TotalSeconds:0} s towing); mode {_draw} – {why}; the poses {_afterHow}; " +
                            $"Physics.autoSyncTransforms {sync}");
        }

        public void Destroy()
        {
            // First of all out of the physics, the body and the joint together (Object.Destroy waits for the end of the
            // frame; a Fusion tick before it must not see a kinematic body still on the car's hitch, nor a size switch
            // this frame find this box in its way)
            try
            {
                if (_go != null)
                    _go.SetActive(false);
                if (_look != null)
                    _look.SetActive(false);
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"Test Trailer: the trailer not taken out of the physics at once: {e.Message}");
            }
            if (_subscribed || _afterSubscribed)
            {
                try
                {
                    if (_subscribed && _addOn != null && _hook != null)
                        _addOn.remove_OnBeforeSimulate(_hook);
                    if (_afterSubscribed && _addOn != null && _afterHook != null)
                        _addOn.remove_OnAfterSimulate(_afterHook);
                }
                catch (Exception e)
                {
                    MelonLogger.Warning($"Test Trailer: leaving Fusion's physics step failed: {e.Message}");
                }
                _subscribed = false;
                _afterSubscribed = false;
            }
            try
            {
                if (_go != null)
                    Object.Destroy(_go);
                if (_look != null)
                    Object.Destroy(_look);
                foreach (var asset in _assets)
                    if (asset != null)
                        Object.Destroy(asset);
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"Test Trailer: the trailer not fully cleaned up: {e.Message}");
            }
            _assets.Clear();
            _go = null;
            _look = null;
            Body = null;
            _joint = null;
            _box = null;
        }
    }
}
