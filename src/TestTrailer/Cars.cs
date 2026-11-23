using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppFusion;
using Il2CppVehiclePhysics;
using UnityEngine;

namespace TestTrailer
{
    // The cars the game has spawned (Mod.cs: VehicleBase.Spawned, wrecks left out), and among them the one this player
    // drives: its NetworkObject's input authority is this player's (OverlandNetworkRunner.LocalPlayerRef)
    internal static class Cars
    {
        private static readonly List<Car> All = new();
        private static int _frame;

        public static void Track(VehicleBase vehicle)
        {
            All.RemoveAll(car => !car.Alive);
            var fresh = new Car(vehicle);
            // The same car again stays; a new car at a pointer an old one had replaces it
            if (All.Exists(car => car.Id == fresh.Id))
                return;
            All.RemoveAll(car => car.Id.pointer == fresh.Id.pointer);
            All.Add(fresh);
        }

        public static void Tick()
        {
            if (++_frame % 30 == 0)
                All.RemoveAll(car => !car.Alive);
        }

        // Whether any car is spawned (none in the menus and while loading)
        public static bool Any => All.Count > 0;

        public static int Local
        {
            get
            {
                try
                {
                    var player = OverlandNetworkRunner.LocalPlayerRef;
                    return player.IsNone ? -1 : player.Raw;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }

        // The car this player drives now, or null
        public static Car Mine()
        {
            // No car spawned (the menus, loading): no game call at all
            if (All.Count == 0)
                return null;
            int local = Local;
            if (local < 0)
                return null;
            foreach (var car in All)
            {
                try
                {
                    if (car.Alive && car.Driver() == local)
                        return car;
                }
                catch (Exception)
                {
                    // a car on its way out
                }
            }
            return null;
        }
    }

    internal sealed class Car
    {
        public readonly Transform Root;
        public readonly string Name;
        private readonly VehicleBase _vehicle;

        public Car(VehicleBase vehicle)
        {
            _vehicle = vehicle;
            Root = vehicle.transform;
            Name = Root.name;
            Id = (Root.Pointer, Root.GetInstanceID());
        }

        public bool Alive
        {
            get
            {
                try
                {
                    return Root != null && _vehicle != null;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public NetworkObject Network => _vehicle.Object;

        public VehicleBase Vehicle => _vehicle;

        // Which car this is, also when a new car comes at a pointer an old one had: the root's pointer and instance id
        public readonly (IntPtr pointer, int instance) Id;

        // Below this the car stands: the ground is measured at rest
        public const float Standing = 0.5f;

        // Whether the car stands (below 0.5 m/s); an unknown speed is not standing
        public bool Stands(out float speed, out string how)
        {
            speed = Speed(out how);
            return !float.IsNaN(speed) && speed < Standing;
        }

        private Vector3 _lastAt;
        private float _lastTime = -1f;
        private bool _velocityFailed;

        // The car's speed in m/s: its root Rigidbody's velocity, or (if that read fails) its position against the last
        // call's, NaN when there is no earlier call within two seconds to compare with
        public float Speed(out string how)
        {
            if (!_velocityFailed)
            {
                try
                {
                    var body = Root.GetComponent<Rigidbody>();
                    if (body != null)
                    {
                        var v = body.linearVelocity;
                        how = "the rigid body's velocity";
                        return (float)Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
                    }
                }
                catch (Exception e)
                {
                    _velocityFailed = true;
                    Once.Warn("velocity", $"the car's velocity unread, its speed taken from its moves: {e.Message}");
                }
            }
            how = "the car's moves";
            var at = Root.position;
            float now = Time.unscaledTime;
            float speed = float.NaN;
            if (_lastTime >= 0f && now - _lastTime > 0.01f && now - _lastTime < 2f)
            {
                float dx = at.x - _lastAt.x, dy = at.y - _lastAt.y, dz = at.z - _lastAt.z;
                speed = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz) / (now - _lastTime);
            }
            _lastAt = at;
            _lastTime = now;
            return speed;
        }

        // Who drives it: the input authority's PlayerRef.Raw, -1 for nobody
        public int Driver()
        {
            var network = _vehicle.Object;
            if (network == null)
                return -1;
            var input = network.InputAuthority;
            return input.IsNone ? -1 : input.Raw;
        }
    }
}
