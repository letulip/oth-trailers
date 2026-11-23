using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine.InputSystem;

namespace TestTrailer
{
    // The keys while driving – only when the game's driving controls are on, so not in its menus, on the map or in photo
    // mode: N hitches or unhitches the test trailer (or moves it to this player's current car), Shift+N picks the next
    // size, F11 writes the probe again (names from the preferences); hitching and the probe only with the car standing
    internal static class Keys
    {
        private static readonly Slot TrailerKey = new(), ProbeKey = new();

        public static void Tick()
        {
            // No car spawned (the menus, loading): no game call at all
            if (!Cars.Any || !Driving())
                return;
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (ProbeKey.Pressed(keyboard, Mod.ProbeKey.Value))
                Probe.OnDemand();
            if (TrailerKey.Pressed(keyboard, Mod.Key.Value))
            {
                if (keyboard.shiftKey.isPressed)
                    Trailer.NextSize();
                else
                    Trailer.Toggle();
            }
        }

        private static bool Driving()
        {
            if (!GameInput.IsReady)
                return false;
            var actions = GameInput.PlayerInputActions;
            var driving = actions != null ? actions.m_Driving : null;
            return driving != null && driving.enabled;
        }

        // A key named in the preferences (like N, F11, Numpad5), parsed when the name changes
        private sealed class Slot
        {
            private string _name;
            private Key _key = Key.None;

            public bool Pressed(Keyboard keyboard, string name)
            {
                if (_name != name)
                {
                    _name = name;
                    if (string.IsNullOrWhiteSpace(name))
                        _key = Key.None;
                    else if (!Enum.TryParse(name.Trim(), true, out _key) || _key == Key.None)
                    {
                        _key = Key.None;
                        MelonLogger.Warning($"Test Trailer: no key called '{name}' (names like N, F11, Numpad5)");
                    }
                }
                return _key != Key.None && keyboard[_key].wasPressedThisFrame;
            }
        }
    }
}
