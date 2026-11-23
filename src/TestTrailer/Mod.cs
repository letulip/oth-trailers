using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using Il2Cpp;
using Il2CppVehiclePhysics;

[assembly: MelonInfo(typeof(TestTrailer.Mod), "TestTrailer", "0.0.4", "letulip")]
[assembly: MelonGame]

namespace TestTrailer
{
    // A test bench for trailers (docs/test-trailer.md). 0.0.4 is its stages 0 to 4: the probe (what the game's physics
    // and Fusion run, this player's car's body, hitch point, wheels' ground, layer row, keys – logs only) and a box
    // on wheels towed behind this player's car as a body of its own (solo or host). Nothing is sent and nothing goes
    // into the save.
    public class Mod : MelonMod
    {
        private static MelonPreferences_Category _prefs;
        internal static MelonPreferences_Entry<string> Key, ProbeKey;
        internal static MelonPreferences_Entry<bool> LitShader;

        public override void OnInitializeMelon()
        {
            _prefs = MelonPreferences.CreateCategory("TestTrailer");
            Key = _prefs.CreateEntry("Key", "N",
                description: "The key that hitches and unhitches the test trailer while driving; with Shift it picks the next size (S, M, L)");
            ProbeKey = _prefs.CreateEntry("ProbeKey", "F11",
                description: "The key that writes the probe into the log again while driving");
            LitShader = _prefs.CreateEntry("LitShader", false,
                description: "Draw the box with Unity's plain URP Lit shader instead of a copy of the car body's material");
            LoggerInstance.Msg($"TestTrailer loaded: {Key.Value} hitches the test trailer (solo or host, standing), Shift+{Key.Value} the next size, " +
                               $"{ProbeKey.Value} the probe again");
        }

        public override void OnUpdate()
        {
            Once.Guard("tracking the cars", Cars.Tick);
            Once.Guard("the probe", Probe.Tick);
            Once.Guard("the keys", Keys.Tick);
            Once.Guard("the trailer", Trailer.Tick);
            Once.Guard("the scene watch", SceneWatch.Tick);
        }

        public override void OnLateUpdate()
        {
            Once.Guard("the trailer's wheels", Trailer.LateTick);
        }
    }

    // The trailer goes when the active scene changes (a new world, the main menu), not at every additive unload: the active
    // scene's handle, polled twice a second
    internal static class SceneWatch
    {
        private static int _frame, _handle;
        private static bool _known;

        public static void Tick()
        {
            if (++_frame % 30 != 0)
                return;
            int handle = SceneManager.GetActiveScene().handle;
            if (_known && handle != _handle)
                Trailer.Remove("the active scene changed");
            _handle = handle;
            _known = true;
        }
    }

    // Every car the game spawns is noted (Cars); this player's is found among them by its input authority
    [HarmonyPatch(typeof(VehicleBase), nameof(VehicleBase.Spawned))]
    internal static class VehicleSpawnedPatch
    {
        private static bool IsWreck(VehicleBase vehicle, Transform root) =>
            root.name.StartsWith("Broken_Car_", StringComparison.Ordinal) || vehicle.GetComponent<BrokenCarBehaviour>() != null;

        private static void Prefix(VehicleBase __instance)
        {
            // An exception escaping into the game's spawn path could break the car, so swallow everything
            try
            {
                var root = __instance.transform;
                // A wreck in the world (the game's broken-car prefab) is nobody's car and gets no trailer
                if (IsWreck(__instance, root))
                    return;
                Cars.Track(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Test Trailer: tracking a car failed: {e}");
            }
        }
    }

    // A per-frame step that throws is skipped for 5 s, and after 5 failures stopped for the session, each pause and the
    // stop logged once, so the log never fills with the same error and a broken step never runs every frame
    internal static class Once
    {
        private const double PauseSeconds = 5.0;
        private const int MaxFailures = 5;

        private sealed class State
        {
            public int Failures;
            public double Until;
            public bool Stopped;
        }

        private static readonly Dictionary<string, State> Steps = new();
        private static readonly HashSet<string> Told = new();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        public static void Guard(string what, Action step)
        {
            if (!Steps.TryGetValue(what, out var state))
                Steps[what] = state = new State();
            if (state.Stopped || Clock.Elapsed.TotalSeconds < state.Until)
                return;
            try
            {
                step();
            }
            catch (Exception e)
            {
                state.Failures++;
                if (state.Failures >= MaxFailures)
                {
                    state.Stopped = true;
                    MelonLogger.Error($"Test Trailer: {what} failed {state.Failures} times, stopped for this session: {e.Message}");
                }
                else
                {
                    state.Until = Clock.Elapsed.TotalSeconds + PauseSeconds;
                    MelonLogger.Error($"Test Trailer: {what} failed ({state.Failures} of {MaxFailures}), paused for {PauseSeconds:0} s: " +
                                      (state.Failures == 1 ? e.ToString() : e.Message));
                }
            }
        }

        public static void Warn(string what, string text)
        {
            if (Told.Add(what))
                MelonLogger.Warning($"Test Trailer: {text}");
        }
    }
}
