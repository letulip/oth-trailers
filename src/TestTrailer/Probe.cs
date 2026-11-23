using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Il2Cpp;
using Il2CppFusion;
using Il2CppFusion.Addons.Physics;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using N3 = System.Numerics.Vector3;

namespace TestTrailer
{
    // Stage 0 (docs/test-trailer.md): what the game's physics and Fusion run, and this player's car as the trailer would
    // meet it – logs only. The session's part (physics, Fusion and its physics add-on, the keys) when this player's first
    // car of a session is probed; the car's part (its body, hitch, wheels' ground, layer row, whether its jointed
    // accessories move) for every car of this player, once it has been found for 2 s and stands (below 0.5 m/s; after
    // 30 s of waiting it is probed anyway, and says so); all of it again by the probe key while driving, standing only.
    // The accessories are read twice: for a second at the probe (standing), and for a second the first time the car then
    // drives faster than 3 m/s (within 3 minutes).
    internal static class Probe
    {
        private const string P = "Test Trailer probe: ";
        private const double FoundFor = 2.0, GiveUp = 30.0, ArmedFor = 180.0;
        private const float Driving = 3f;
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static (IntPtr pointer, int instance) _probed;
        private static IntPtr _runner;
        private static Car _waiting, _armed;
        private static double _foundAt, _armedAt;
        private static int _frame;
        private static Sampler _sampler;

        private static double Now => Clock.Elapsed.TotalSeconds;

        public static void Tick()
        {
            _frame++;
            if (_sampler != null && _sampler.Tick())
            {
                // The standing read done: the moving one waits for the car to drive
                if (!_sampler.WhileDriving)
                {
                    _armed = _sampler.Car;
                    _armedAt = Now;
                }
                _sampler = null;
            }
            if (_frame % 20 != 0)
                return;
            WatchDriving();
            // This player's car, looked for three times a second (none when no car is spawned: no game call then)
            var car = Cars.Mine();
            if (car == null)
            {
                _waiting = null;
                return;
            }
            if (car.Id == _probed)
                return;
            if (_waiting == null || _waiting.Id != car.Id)
            {
                _waiting = car;
                _foundAt = Now;
                car.Speed(out _);
                return;
            }
            double waited = Now - _foundAt;
            if (waited < FoundFor)
                return;
            bool stands = car.Stands(out float speed, out string how);
            if (!stands && waited < GiveUp)
                return;
            _probed = car.Id;
            _waiting = null;
            MelonLogger.Msg(P + (stands
                ? $"'{car.Name}' found {waited:0.0} s ago, standing ({speed:0.00} m/s from {how})"
                : $"'{car.Name}' never stood still in {GiveUp:0} s ({Speed(speed)} from {how}): probed anyway, the wheels' ground may be off"));
            var runner = Runner();
            var pointer = runner != null ? runner.Pointer : IntPtr.Zero;
            Run(car, pointer != _runner);
            _runner = pointer;
        }

        private static string Speed(float speed) => float.IsNaN(speed) ? "speed unknown" : $"{speed:0.00} m/s";

        // After the standing read: the first second the car drives faster than 3 m/s
        private static void WatchDriving()
        {
            if (_armed == null || _sampler != null)
                return;
            if (!_armed.Alive)
            {
                _armed = null;
                return;
            }
            if (Now - _armedAt > ArmedFor)
            {
                MelonLogger.Msg(P + $"accessory bodies move while driving: not read ('{_armed.Name}' did not drive faster than {Driving:0} m/s within {ArmedFor / 60:0} minutes)");
                _armed = null;
                return;
            }
            float speed = _armed.Speed(out _);
            if (float.IsNaN(speed) || speed <= Driving)
                return;
            _sampler = Sampler.Start(_armed, true);
            _armed = null;
        }

        // The probe key: standing only, as the wheels' ground is measured at rest
        public static void OnDemand()
        {
            var car = Cars.Mine();
            if (car != null && !car.Stands(out float speed, out string how))
            {
                MelonLogger.Msg(P + $"not by the key: '{car.Name}' is moving ({Speed(speed)} from {how}); stop the car and press again");
                Look.Say("Stop the car for the probe");
                return;
            }
            MelonLogger.Msg(P + "by the key" + (car == null ? ", no car of this player found" : $" on '{car.Name}'"));
            _armed = null;
            if (car != null)
            {
                // The key's probe counts as this car's: the automatic one does not follow it
                _probed = car.Id;
                _waiting = null;
            }
            Run(car, true);
        }

        private static void Run(Car car, bool session)
        {
            if (session)
            {
                Section("physics", PhysicsPart);
                Section("Fusion", FusionPart);
                Section("keys", KeysPart);
            }
            if (car == null)
                return;
            MelonLogger.Msg(P + $"this player's car '{car.Name}' (player {Cars.Local})");
            Section("the car's body", () => BodyPart(car));
            Section("the hitch", () => HitchPart(car));
            Section("the ground", () => GroundPart(car));
            Section("the layer row", () => LayerPart(car));
            Section("the accessory bodies", () => _sampler = Sampler.Start(car, false));
        }

        private static void Section(string what, Action part)
        {
            try
            {
                part();
            }
            catch (Exception e)
            {
                MelonLogger.Warning(P + $"{what} unread: {e.Message}");
            }
        }

        // A value, or why it could not be read (a Unity call the game build stripped throws here)
        private static string V(Func<object> read)
        {
            try
            {
                var value = read();
                return value switch
                {
                    null => "null",
                    float f => f.ToString("0.#####"),
                    Vector3 v => Measure.F(v),
                    _ => value.ToString(),
                };
            }
            catch (Exception e)
            {
                return $"unread ({e.GetType().Name}: {e.Message})";
            }
        }

        internal static NetworkRunner Runner()
        {
            try
            {
                var overland = OverlandNetworkRunner.Instance;
                return overland != null ? overland.NetworkRunner : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void PhysicsPart()
        {
            MelonLogger.Msg(P + $"physics: simulationMode {V(() => Physics.simulationMode)}, fixedDeltaTime {V(() => Time.fixedDeltaTime)} s, " +
                            $"defaultSolverIterations {V(() => Physics.defaultSolverIterations)}, " +
                            $"defaultSolverVelocityIterations {V(() => Physics.defaultSolverVelocityIterations)}, gravity {V(() => Physics.gravity)}");
        }

        private static void FusionPart()
        {
            var runner = Runner();
            if (runner == null)
            {
                MelonLogger.Msg(P + "Fusion: no runner (OverlandNetworkRunner.Instance.NetworkRunner is null)");
                return;
            }
            MelonLogger.Msg(P + $"Fusion: IsServer {V(() => runner.IsServer)}, IsClient {V(() => runner.IsClient)}, " +
                            $"IsSinglePlayer {V(() => runner.IsSinglePlayer)}, GameMode {V(() => runner.GameMode)}, Mode {V(() => runner.Mode)}, " +
                            $"TickRate {V(() => runner.TickRate)}/s, DeltaTime {V(() => runner.DeltaTime)} s");
            string where = null;
            RunnerSimulatePhysicsBase addOn = null;
            try
            {
                addOn = AddOn(runner, out where);
            }
            catch (Exception e)
            {
                MelonLogger.Warning(P + $"Fusion: the physics add-on not looked up: {e.Message}");
            }
            if (addOn == null)
            {
                MelonLogger.Msg(P + "Fusion physics add-on: none found on the runner's object or under OverlandNetworkRunner's");
                return;
            }
            var a = addOn;
            MelonLogger.Msg(P + $"Fusion physics add-on: {V(() => a.GetIl2CppType().Name)} ({where}), " +
                            $"ClientPhysicsSimulation {V(() => a.ClientPhysicsSimulation)}, PhysicsAuthority {V(() => a.PhysicsAuthority)}, " +
                            $"PhysicsTiming {V(() => a.PhysicsTiming)}, SetUnityFixedTimestep {V(() => a.SetUnityFixedTimestep)}, " +
                            $"DeltaTimeMultiplier {V(() => a.DeltaTimeMultiplier)}, enabled {V(() => a.enabled)}");
        }

        // Fusion's physics add-on, which steps the scene's physics in Fusion's tick (Physics.simulationMode Script)
        internal static RunnerSimulatePhysicsBase AddOn(NetworkRunner runner, out string where)
        {
            RunnerSimulatePhysicsBase addOn = runner.GetComponent<RunnerSimulatePhysics3D>();
            where = "RunnerSimulatePhysics3D on the runner's object";
            if (addOn == null)
            {
                addOn = runner.GetComponentInChildren<RunnerSimulatePhysicsBase>(true);
                where = "a RunnerSimulatePhysicsBase under the runner's object";
            }
            if (addOn == null)
            {
                var overland = OverlandNetworkRunner.Instance;
                addOn = overland != null ? overland.GetComponentInChildren<RunnerSimulatePhysicsBase>(true) : null;
                where = "a RunnerSimulatePhysicsBase under OverlandNetworkRunner's object";
            }
            return addOn;
        }

        private static void BodyPart(Car car)
        {
            var root = car.Root;
            var body = root.GetComponent<Rigidbody>();
            if (body == null)
            {
                MelonLogger.Msg(P + $"'{car.Name}' has no Rigidbody on its root");
                return;
            }
            int layer = root.gameObject.layer;
            MelonLogger.Msg(P + $"body: mass {V(() => body.mass)} kg, centerOfMass {V(() => body.centerOfMass)}, inertiaTensor {V(() => body.inertiaTensor)}, " +
                            $"inertiaTensorRotation {V(() => body.inertiaTensorRotation.eulerAngles)}, interpolation {V(() => body.interpolation)}, " +
                            $"collisionDetectionMode {V(() => body.collisionDetectionMode)}");
            MelonLogger.Msg(P + $"body: solverIterations {V(() => body.solverIterations)}, solverVelocityIterations {V(() => body.solverVelocityIterations)}, " +
                            $"maxDepenetrationVelocity {V(() => body.maxDepenetrationVelocity)}, maxAngularVelocity {V(() => body.maxAngularVelocity)}, " +
                            $"isKinematic {V(() => body.isKinematic)}, layer {layer} '{V(() => LayerMask.LayerToName(layer))}'");
        }

        private static void HitchPart(Car car)
        {
            var hitch = Measure.HitchOf(car.Root);
            MelonLogger.Msg(P + (hitch.HasRear
                ? $"{Measure.RearPath}: car space {Measure.F(hitch.Rear)}, world {Measure.F(hitch.RearWorld)}"
                : $"{Measure.RearPath}: not found"));
            if (hitch.TowBarNode == null)
                MelonLogger.Msg(P + $"tow bar: no Parts/<X>_CosmeticParts/{Measure.TowBarPath} on this car");
            else if (!hitch.TowBarActive)
                MelonLogger.Msg(P + $"tow bar: {hitch.TowBarNode} exists, not fitted (inactive)");
            else
                MelonLogger.Msg(P + $"tow bar: {hitch.TowBarNode} fitted, drawn bounds in car space {Measure.F(hitch.TowMin)} to {Measure.F(hitch.TowMax)}, " +
                                $"rear-most z {hitch.TowMin.Z:0.000} at height {hitch.TowRearY:0.000} ({hitch.TowRearYHow})" +
                                (hitch.HasBall ? $"; LOD0 bounds {Measure.F(hitch.Lod0Min)} to {Measure.F(hitch.Lod0Max)}, tow ball {Measure.F(hitch.Ball)}" : "; no LOD0 mesh"));
            MelonLogger.Msg(P + $"hitch: {Measure.F(hitch.Point)} car space – {hitch.Source}");
        }

        private static void GroundPart(Car car)
        {
            var ground = Measure.GroundOf(car.Root);
            foreach (var w in ground.Wheels)
                MelonLogger.Msg(P + $"wheel {w.Name}: centre {Measure.F(w.Centre)} ({(w.Visual ? "the drawn wheel" : "the collider's node")}), " +
                                $"collider node {Measure.F(w.Collider)}, radius {w.Radius:0.000}, suspension travel {w.Travel:0.000}, " +
                                $"bottom at {w.Centre.Y - w.Radius:0.000}");
            MelonLogger.Msg(P + $"ground: y {ground.Y:0.000} in car space (median), lowest wheel's bottom {ground.Lowest:0.000} – {ground.How}");
        }

        private static void LayerPart(Car car)
        {
            int layer = car.Root.gameObject.layer;
            var meets = new StringBuilder();
            var ignores = new StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(name))
                    continue;
                var list = Physics.GetIgnoreLayerCollision(layer, i) ? ignores : meets;
                list.Append(list.Length > 0 ? ", " : "").Append(i).Append(' ').Append(name);
            }
            MelonLogger.Msg(P + $"layer {layer} '{LayerMask.LayerToName(layer)}' collides with: {meets}");
            MelonLogger.Msg(P + $"layer {layer} ignores: {(ignores.Length > 0 ? ignores.ToString() : "none")}");
        }

        // The game's keys: every binding of every action map of the game's input actions (the player's own rebinding
        // first), looking for N, B, X and F11. Each read is named, so a null on the way says where it was; if the
        // bindings' fields cannot be read, the asset's JSON is searched instead (its paths, not the player's rebinding).
        private static readonly string[] Wanted = { "n", "b", "x", "f11" };

        private static void KeysPart()
        {
            string step = "GameInput.IsReady";
            var used = new Dictionary<string, List<string>>();
            foreach (var key in Wanted)
                used[key] = new List<string>();
            InputActionAsset asset = null;
            try
            {
                if (!GameInput.IsReady)
                {
                    MelonLogger.Msg(P + "keys: the game's input actions are not ready (GameInput.IsReady false)");
                    return;
                }
                step = "GameInput.PlayerInputActions";
                var actions = GameInput.PlayerInputActions;
                if (actions == null)
                {
                    MelonLogger.Msg(P + "keys: GameInput.PlayerInputActions is null");
                    return;
                }
                step = "PlayerInputActions.asset";
                asset = actions.asset;
                if (asset == null)
                {
                    MelonLogger.Msg(P + "keys: PlayerInputActions.asset is null");
                    return;
                }
                step = "InputActionAsset.m_ActionMaps";
                var maps = asset.m_ActionMaps;
                if (maps == null)
                {
                    MelonLogger.Msg(P + "keys: InputActionAsset.m_ActionMaps is null");
                    return;
                }
                int count = 0, nullMaps = 0, nullBindings = 0, failed = 0;
                string firstFailure = null;
                for (int m = 0; m < maps.Length; m++)
                {
                    step = $"m_ActionMaps[{m}]";
                    var map = maps[m];
                    if (map == null)
                    {
                        nullMaps++;
                        continue;
                    }
                    step = $"map {m} m_Name";
                    string mapName = map.m_Name;
                    step = $"map '{mapName}' m_Bindings";
                    var bindings = map.m_Bindings;
                    if (bindings == null)
                    {
                        nullBindings++;
                        continue;
                    }
                    for (int b = 0; b < bindings.Length; b++)
                    {
                        try
                        {
                            var binding = bindings[b];
                            if (binding == null)
                            {
                                nullBindings++;
                                continue;
                            }
                            count++;
                            string over = binding.m_OverridePath;
                            string path = !string.IsNullOrEmpty(over) ? over : binding.m_Path;
                            if (string.IsNullOrEmpty(path))
                                continue;
                            foreach (var key in Wanted)
                                if (string.Equals(path, "<Keyboard>/" + key, StringComparison.OrdinalIgnoreCase))
                                    used[key].Add($"{mapName}/{binding.m_Action}{(!string.IsNullOrEmpty(over) ? " (rebound)" : "")}");
                        }
                        catch (Exception e)
                        {
                            failed++;
                            firstFailure ??= $"map '{mapName}' binding {b}: {e.GetType().Name}: {e.Message}";
                        }
                    }
                }
                if (failed > 0 && count == 0)
                    throw new InvalidOperationException($"no binding readable ({failed} failed, first {firstFailure})");
                Tell(used, $"{count} bindings in {maps.Length} maps read ({nullMaps} null maps, {nullBindings} null bindings or binding arrays" +
                           $"{(failed > 0 ? $", {failed} unreadable, first {firstFailure}" : "")})");
            }
            catch (Exception e)
            {
                MelonLogger.Msg(P + $"keys: the bindings unread at {step}: {e.GetType().Name}: {e.Message}; reading the asset's JSON instead");
                FromJson(asset);
            }
        }

        // The asset's own JSON: every binding object with a keyboard path we look for, and its action
        private static void FromJson(InputActionAsset asset)
        {
            if (asset == null)
            {
                MelonLogger.Msg(P + "keys: no asset to read the JSON of");
                return;
            }
            string json = asset.ToJson();
            var used = new Dictionary<string, List<string>>();
            foreach (var key in Wanted)
                used[key] = new List<string>();
            var binding = new System.Text.RegularExpressions.Regex("\\{[^{}]*\"path\"\\s*:\\s*\"<Keyboard>/([A-Za-z0-9]+)\"[^{}]*\\}");
            var action = new System.Text.RegularExpressions.Regex("\"action\"\\s*:\\s*\"([^\"]*)\"");
            int count = 0;
            foreach (System.Text.RegularExpressions.Match match in binding.Matches(json ?? ""))
            {
                count++;
                string key = match.Groups[1].Value.ToLowerInvariant();
                if (!used.ContainsKey(key))
                    continue;
                var a = action.Match(match.Value);
                used[key].Add(a.Success ? a.Groups[1].Value : "?");
            }
            Tell(used, $"{count} keyboard bindings found in the asset's JSON ({json?.Length ?? 0} characters; the player's rebinding not in it)");
        }

        private static void Tell(Dictionary<string, List<string>> used, string what)
        {
            foreach (var key in Wanted)
                if (used[key].Count > 0)
                    MelonLogger.Msg(P + $"keys: <Keyboard>/{key} is bound to {string.Join(", ", used[key])}");
            MelonLogger.Msg(P + $"keys: {what}; " +
                            string.Join(", ", Array.ConvertAll(Wanted, k => $"{k.ToUpperInvariant()} {(used[k].Count == 0 ? "free" : "used")}")));
        }

        // For a second after the probe: whether the car's jointed accessory bodies (the lantern, the pan, the whip lights
        // on ConfigurableJoints) move relative to the car. They swing only where this game steps physics.
        private sealed class Sampler
        {
            private const float Seconds = 1f, Moved = 0.001f;
            public readonly Car Car;
            public readonly bool WhileDriving;
            private readonly Car _car;
            private readonly List<(Transform t, N3 start)> _bodies = new();
            private readonly N3 _from;
            private readonly int _kinematic;
            private float _t, _most;

            private Sampler(Car car, List<Rigidbody> bodies, bool whileDriving)
            {
                _car = Car = car;
                WhileDriving = whileDriving;
                _from = Measure.N(car.Root.position);
                foreach (var body in bodies)
                {
                    _bodies.Add((body.transform, Measure.ToCar(car.Root, body.transform.position)));
                    try
                    {
                        if (body.isKinematic)
                            _kinematic++;
                    }
                    catch (Exception)
                    {
                        // unread: counted as not kinematic
                    }
                }
            }

            public static Sampler Start(Car car, bool whileDriving)
            {
                var bodies = new List<Rigidbody>();
                var own = car.Root.GetComponent<Rigidbody>();
                foreach (var body in car.Root.GetComponentsInChildren<Rigidbody>(false))
                    if (body != null && (own == null || body.Pointer != own.Pointer))
                        bodies.Add(body);
                if (bodies.Count == 0)
                {
                    MelonLogger.Msg(P + $"accessory bodies move{(whileDriving ? " while driving" : "")}: unknown (no active rigid bodies under the car besides its own)");
                    return null;
                }
                return new Sampler(car, bodies, whileDriving);
            }

            // True when done
            public bool Tick()
            {
                if (!_car.Alive)
                    return true;
                foreach (var (t, start) in _bodies)
                {
                    if (t == null)
                        continue;
                    _most = Math.Max(_most, N3.Distance(Measure.ToCar(_car.Root, t.position), start));
                }
                _t += Time.unscaledDeltaTime;
                if (_t < Seconds)
                    return false;
                float travelled = N3.Distance(Measure.N(_car.Root.position), _from);
                MelonLogger.Msg(P + $"accessory bodies move{(WhileDriving ? " while driving" : " (standing)")}: {(_most > Moved ? "yes" : "no")} ({_bodies.Count} bodies, {_kinematic} kinematic; " +
                                $"most moved {_most * 1000f:0.0} mm relative to the car in {_t:0.00} s, the car went {travelled:0.00} m meanwhile)");
                return true;
            }
        }
    }
}
