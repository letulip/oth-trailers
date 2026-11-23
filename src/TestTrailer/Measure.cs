using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppVehiclePhysics;
using UnityEngine;
using N3 = System.Numerics.Vector3;

namespace TestTrailer
{
    // Where a car's trailer hangs, read from the car as the game has it now: the hitch (the winch's rear attach point, or
    // the tow bar's rear end when that part is fitted) and the ground under the car (its wheels' lowest points), both in
    // the car's space (+z ahead, +y up). The probe logs them; the box is built on them.
    internal static class Measure
    {
        public const string RearPath = "Winch/WinchAttachPoints/Rear";
        public const string TowBarPath = "Visual/Rear_Bumper/TowBar";

        public sealed class Hitch
        {
            public bool HasRear;
            public N3 Rear, RearWorld;
            public string TowBarNode;           // the node's path under the root, or null when the car has none
            public bool TowBarActive;
            public N3 TowMin, TowMax;           // the tow bar's drawn bounds in car space
            public float TowRearY;              // the height at its rear-most end
            public string TowRearYHow;
            public bool HasBall;                // LOD0's bounds found: the tow ball estimated from them (Trailer Lab's way)
            public N3 Ball, Lod0Min, Lod0Max;
            public N3 Point;                    // where the coupling goes
            public string Source;               // which of the two it is
        }

        public sealed class Wheel
        {
            public string Name;
            public N3 Centre;                   // the drawn wheel's centre (the suspension as it is now)
            public N3 Collider;                 // the wheel collider's own node
            public float Radius, Travel;
            public bool Visual;
        }

        public sealed class Ground
        {
            public float Y;                     // the ground's height in car space: the median of the wheels' bottoms
            public float Lowest;                // the lowest wheel's bottom
            public string How;
            public readonly List<Wheel> Wheels = new();
        }

        // The car's own rear wheels, which the trailer's copy: radius, tyre width and suspension travel from their
        // colliders, the track, and the drawn wheels (the LODGroup under Axles/WheelMeshes/RL and RR) to copy, each with
        // the game's WheelTransformUpdater over it. Which drawn wheel is the left one comes from the car's own wheel – the
        // updater's m_isLeftWheel, else the collider's x in car space – not from the corner's name or the copy's axes.
        public sealed class CarWheels
        {
            public VPFunselektor3DRaycastWheelCollider Left, Right;
            public float Radius, Width, Travel, Track;
            public Transform LeftLook, RightLook;
            public WheelTransformUpdater LeftUpdater, RightUpdater;
            public string SideHow = "no drawn wheels found";
        }

        public static CarWheels RearWheels(Transform root)
        {
            var wheels = new CarWheels();
            var colliders = root.Find("Axles/WheelColliders");
            VPFunselektor3DRaycastWheelCollider rl = null, rr = null;
            if (colliders != null)
            {
                var nl = colliders.Find("WheelRL");
                var nr = colliders.Find("WheelRR");
                rl = nl != null ? nl.GetComponent<VPFunselektor3DRaycastWheelCollider>() : null;
                rr = nr != null ? nr.GetComponent<VPFunselektor3DRaycastWheelCollider>() : null;
            }
            // the colliders' own sides by their x
            if (rl != null && rr != null && ToCar(root, rl.transform.position).X > ToCar(root, rr.transform.position).X)
                (rl, rr) = (rr, rl);
            wheels.Left = rl;
            wheels.Right = rr;
            var any = wheels.Left ?? wheels.Right;
            if (any != null)
            {
                wheels.Radius = any.radius;
                wheels.Width = any.tireWidth;
                wheels.Travel = any.suspensionTravel;
            }
            if (wheels.Left != null && wheels.Right != null)
                wheels.Track = Math.Abs(ToCar(root, wheels.Right.transform.position).X - ToCar(root, wheels.Left.transform.position).X);
            var a = WheelLook(root, "RL", out var ua);
            var b = WheelLook(root, "RR", out var ub);
            var how = new List<string>();
            bool? aLeft = Side(root, a, ua, b, ub, how);
            if (aLeft == false)
            {
                (a, b) = (b, a);
                (ua, ub) = (ub, ua);
            }
            wheels.LeftLook = a;
            wheels.RightLook = b;
            wheels.LeftUpdater = ua;
            wheels.RightUpdater = ub;
            if (how.Count > 0)
                wheels.SideHow = string.Join(", ", how);
            return wheels;
        }

        // Whether the RL corner's drawn wheel is the left one: the updaters' m_isLeftWheel only when the two corners'
        // flags disagree (one left, one right), else the two nodes' x in car space
        private static bool? Side(Transform root, Transform a, WheelTransformUpdater ua, Transform b, WheelTransformUpdater ub, List<string> how)
        {
            if (a == null && b == null)
                return null;
            try
            {
                if (ua != null && ub != null && ua.m_isLeftWheel != ub.m_isLeftWheel)
                {
                    how.Add($"by the updaters' m_isLeftWheel (RL {(ua.m_isLeftWheel ? "left" : "right")}, RR {(ub.m_isLeftWheel ? "left" : "right")})");
                    return ua.m_isLeftWheel;
                }
                if (ua != null && ub != null)
                    how.Add($"the updaters' m_isLeftWheel agree ({ua.m_isLeftWheel}), so");
            }
            catch (Exception)
            {
                how.Add("m_isLeftWheel unread, so");
            }
            if (a != null && b != null)
            {
                bool aLeft = ToCar(root, a.position).X < ToCar(root, b.position).X;
                how.Add($"by the nodes' x in car space (RL {(aLeft ? "left" : "right")})");
                return aLeft;
            }
            var one = a ?? b;
            bool left = ToCar(root, one.position).X < 0f;
            how.Add($"by the one node's x ({(left ? "left" : "right")})");
            return a != null ? left : !left;
        }

        // The drawn wheel of a corner as the car wears it now: the LODGroup node under Axles/WheelMeshes/<corner>, and the
        // game's WheelTransformUpdater over it (which stays out of the copy)
        private static Transform WheelLook(Transform root, string corner, out WheelTransformUpdater updater)
        {
            updater = null;
            var node = root.Find("Axles/WheelMeshes/" + corner);
            if (node == null)
                return null;
            updater = node.GetComponentInChildren<WheelTransformUpdater>(true);
            var group = node.GetComponentInChildren<LODGroup>(true);
            return group != null ? group.transform : null;
        }

        public static N3 ToCar(Transform root, Vector3 world) => N(root.InverseTransformPoint(world));

        public static N3 N(Vector3 v) => new(v.x, v.y, v.z);

        public static string F(N3 v) => $"({v.X:0.000}, {v.Y:0.000}, {v.Z:0.000})";

        public static string F(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";

        public static Transform CosmeticParts(Transform root)
        {
            var parts = root.Find("Parts");
            if (parts == null)
                return null;
            for (int i = 0; i < parts.childCount; i++)
            {
                var child = parts.GetChild(i);
                if (child.name.EndsWith("_CosmeticParts", StringComparison.Ordinal))
                    return child;
            }
            return null;
        }

        public static Hitch HitchOf(Transform root)
        {
            var hitch = new Hitch();
            var rear = root.Find(RearPath);
            if (rear != null)
            {
                hitch.HasRear = true;
                hitch.RearWorld = N(rear.position);
                hitch.Rear = ToCar(root, rear.position);
            }
            var cosmetic = CosmeticParts(root);
            var towBar = cosmetic != null ? cosmetic.Find(TowBarPath) : null;
            if (towBar != null)
            {
                hitch.TowBarNode = $"Parts/{cosmetic.name}/{TowBarPath}";
                hitch.TowBarActive = towBar.gameObject.activeInHierarchy;
                if (hitch.TowBarActive)
                    TowBarBounds(root, towBar, hitch);
            }
            if (hitch.TowBarActive && hitch.HasBall)
            {
                hitch.Point = hitch.Ball;
                hitch.Source = $"the tow ball estimated from the tow bar's LOD0 bounds (their top less {BallTop * 100f:0.0} cm, their rear plus {BallRear * 100f:0.0} cm)";
            }
            else if (hitch.TowBarActive && hitch.TowMin.Z < hitch.TowMax.Z)
            {
                float x = hitch.HasRear ? hitch.Rear.X : (hitch.TowMin.X + hitch.TowMax.X) / 2f;
                hitch.Point = new N3(x, hitch.TowRearY, hitch.TowMin.Z);
                hitch.Source = $"the tow bar's rear end (no LOD0 mesh; its height {hitch.TowRearYHow})";
            }
            else if (hitch.HasRear)
            {
                hitch.Point = hitch.Rear;
                hitch.Source = "the winch's rear attach point";
            }
            else
            {
                // No winch point found: behind the car's drawn body at a usual hitch height
                hitch.Point = new N3(0f, 0.5f, -2.2f);
                hitch.Source = "a guess (0, 0.5, -2.2): the car has no " + RearPath;
            }
            return hitch;
        }

        // Trailer Lab's tow ball: the top-rear corner of the tow bar's LOD0 bounds is the top of the ball; its centre is
        // the ball's radius (25 mm) lower and a little ahead of the rear-most point of its round surface
        private const float BallTop = 0.025f, BallRear = 0.035f;

        // The tow bar's drawn meshes (its LODs) in car space: the bounds of all of them, and the height at the rear-most end
        // from LOD0's vertices when the mesh is readable, else the bounds' middle height
        private static void TowBarBounds(Transform root, Transform towBar, Hitch hitch)
        {
            var min = new N3(float.MaxValue);
            var max = new N3(float.MinValue);
            var toCar = root.worldToLocalMatrix;
            MeshFilter best = null;
            int bestCount = -1;
            var lodMin = new N3(float.MaxValue);
            var lodMax = new N3(float.MinValue);
            foreach (var filter in towBar.GetComponentsInChildren<MeshFilter>(false))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;
                var b = mesh.bounds;
                var c = N(b.center);
                var e = N(b.extents);
                var toWorld = filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = c + new N3(i % 2 == 0 ? -e.X : e.X, (i / 2) % 2 == 0 ? -e.Y : e.Y, i / 4 == 0 ? -e.Z : e.Z);
                    var p = Apply(toCar, Apply(toWorld, corner));
                    min = N3.Min(min, p);
                    max = N3.Max(max, p);
                    if (filter.name.IndexOf("LOD0", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        lodMin = N3.Min(lodMin, p);
                        lodMax = N3.Max(lodMax, p);
                    }
                }
                int count = mesh.vertexCount;
                if (filter.name.Contains("LOD0") || count > bestCount)
                {
                    best = filter;
                    bestCount = filter.name.Contains("LOD0") ? int.MaxValue : count;
                }
            }
            hitch.TowMin = min;
            hitch.TowMax = max;
            if (lodMin.X <= lodMax.X)
            {
                hitch.HasBall = true;
                hitch.Lod0Min = lodMin;
                hitch.Lod0Max = lodMax;
                hitch.Ball = new N3(0f, lodMax.Y - BallTop, lodMin.Z + BallRear);
            }
            hitch.TowRearY = (min.Y + max.Y) / 2f;
            hitch.TowRearYHow = "the bounds' middle";
            if (best == null || min.X > max.X)
                return;
            try
            {
                var mesh = best.sharedMesh;
                if (!mesh.isReadable)
                {
                    hitch.TowRearYHow = "the bounds' middle: the mesh is not readable";
                    return;
                }
                var toWorld = best.transform.localToWorldMatrix;
                var vertices = mesh.vertices;
                var points = new List<N3>(vertices.Length);
                float rearmost = float.MaxValue;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var v = vertices[i];
                    var p = Apply(toCar, Apply(toWorld, new N3(v.x, v.y, v.z)));
                    points.Add(p);
                    rearmost = Math.Min(rearmost, p.Z);
                }
                float sum = 0f;
                int n = 0;
                foreach (var p in points)
                    if (p.Z <= rearmost + 0.03f)
                    {
                        sum += p.Y;
                        n++;
                    }
                if (n > 0)
                {
                    hitch.TowRearY = sum / n;
                    hitch.TowRearYHow = $"from {n} vertices of {best.name} within 3 cm of its rear end";
                }
            }
            catch (Exception e)
            {
                hitch.TowRearYHow = $"the bounds' middle: the vertices unread ({e.Message})";
            }
        }

        private static N3 Apply(Matrix4x4 m, N3 p) => new(
            m.m00 * p.X + m.m01 * p.Y + m.m02 * p.Z + m.m03,
            m.m10 * p.X + m.m11 * p.Y + m.m12 * p.Z + m.m13,
            m.m20 * p.X + m.m21 * p.Y + m.m22 * p.Z + m.m23);

        // The ground under the car in car space: the lowest of its wheels' bottoms (each drawn wheel's centre less its
        // radius); without wheels found, a ray down from the car's middle
        public static Ground GroundOf(Transform root)
        {
            var ground = new Ground { Y = float.MaxValue, Lowest = float.MaxValue };
            var bottoms = new List<float>();
            var axles = root.Find("Axles/WheelColliders");
            var source = axles != null ? axles : root;
            foreach (var collider in source.GetComponentsInChildren<VPFunselektor3DRaycastWheelCollider>(false))
            {
                var wheel = new Wheel { Name = collider.name, Radius = collider.radius, Travel = collider.suspensionTravel };
                wheel.Collider = ToCar(root, collider.transform.position);
                var drawn = collider.wheelTransform;
                wheel.Visual = drawn != null;
                wheel.Centre = drawn != null ? ToCar(root, drawn.position) : wheel.Collider;
                ground.Wheels.Add(wheel);
                bottoms.Add(wheel.Centre.Y - wheel.Radius);
            }
            if (bottoms.Count > 0)
            {
                // The median, so one drooped wheel (in the air, its suspension let out) does not set the ground
                bottoms.Sort();
                int n = bottoms.Count;
                ground.Y = n % 2 == 1 ? bottoms[n / 2] : (bottoms[n / 2 - 1] + bottoms[n / 2]) / 2f;
                ground.Lowest = bottoms[0];
                ground.How = $"the median of {n} wheels' bottoms, the lowest {ground.Lowest:0.000} (VPFunselektor3DRaycastWheelCollider: drawn wheel's centre less radius)";
                return ground;
            }
            ground.Y = ground.Lowest = -0.5f;
            ground.How = "a guess (-0.5): no wheel colliders found";
            try
            {
                float best = float.MaxValue;
                var from = root.position;
                foreach (var hit in Physics.RaycastAll(new Vector3(from.x, from.y + 1f, from.z), new Vector3(0f, -1f, 0f), 6f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var c = hit.collider;
                    if (c == null || c.transform.IsChildOf(root))
                        continue;
                    best = Math.Min(best, hit.distance);
                }
                if (best < float.MaxValue)
                {
                    var p = root.position;
                    ground.Y = ground.Lowest = ToCar(root, new Vector3(p.x, p.y + 1f - best, p.z)).Y;
                    ground.How = "a ray down from the car's middle: no wheel colliders found";
                }
            }
            catch (Exception e)
            {
                ground.How += $"; the ray failed ({e.Message})";
            }
            return ground;
        }
    }
}
