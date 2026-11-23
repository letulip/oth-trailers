using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using N3 = System.Numerics.Vector3;
using Object = UnityEngine.Object;

namespace TestTrailer
{
    // The test trailer's sizes and how it looks (docs/test-trailer.md, sections 3 and 7): a plain box on a drawbar and
    // wheels, built in code, in the trailer's own space – its origin the coupling (the hitch), +z toward the car, +y up,
    // the ground at Ground (the car's, measured) – with a plain copy of the car body's material.
    internal sealed class Preset
    {
        public string Name;
        public float Length, Width, Height, Drawbar, Radius, Mass;
        public int Axles;
    }

    // The wheels the trailer takes from the towing car: its rear wheels' radius, tyre width and track, and its
    // suspension travel capped at 0.35 m
    internal sealed class WheelSpec
    {
        public const float MaxTravel = 0.35f;
        public float Radius, Width, Travel, Track;
        public string From;
    }

    // Where the parts of a size go in the trailer's space (the coupling at the origin) for a ground height under the
    // coupling (spec 04): each axle hangs under the frame on leaf springs as a car's body on its axles – the axle's
    // centre at ground + the wheel's radius (static), the frame rails' underside 0.15 m over it, the rails 0.12 m deep,
    // the drawbar's line at their middle, the floor on them; the body on the frame between the wheels, the wheels beside
    // it under fenders 0.13 m over their static top. The drawbar runs level from the frame and steps to the hitch's
    // height at 45° near the coupling (a Z). The axle (the tandem's middle) where the body's centre of mass puts 11 % of
    // the mass on the ball.
    internal sealed class Layout
    {
        public const float TandemSpacing = 0.9f, TandemGap = 0.12f, TyreGap = 0.04f, Tongue = 0.11f;
        public const float BarHalf = 0.05f, HeadLength = 0.2f, Chamfer = 0.25f;
        public const float Springs = 0.15f, Frame = 0.12f, FenderGap = 0.13f, MaxStep = 0.6f;
        public const float ChassisShare = 0.20f, ShellShare = 0.15f, LoadShare = 0.65f;

        public readonly Preset Preset;
        public readonly WheelSpec Wheels;
        public readonly float Ground, AxleY, RailBottom, LineY, FloorY, Step, StepStart, FenderY, Front, Back, AxleZ, WheelX, HubY, RailX;
        public readonly float ComTarget, LoadHeight, Spacing;
        public readonly float[] Axles;
        public readonly N3 BodyCentre, BodySize, Com;
        public readonly bool Chamfered;

        public Layout(Preset preset, float ground, WheelSpec wheels)
        {
            Preset = preset;
            Wheels = wheels;
            Ground = ground;
            HubY = AxleY = ground + wheels.Radius;
            RailBottom = AxleY + Springs;
            LineY = RailBottom + Frame / 2f;
            FloorY = RailBottom + Frame;
            // The Z: from the drawbar's line up (or down) to the coupling's height (0) at 45°, its run |step|, beginning
            // 0.2 + |step| before the coupling
            Step = 0f - LineY;
            StepStart = -(HeadLength + Math.Abs(Step));
            FenderY = AxleY + wheels.Radius + FenderGap;
            Front = -preset.Drawbar;
            Back = Front - preset.Length;
            BodyCentre = new N3(0f, FloorY + preset.Height / 2f, (Front + Back) / 2f);
            BodySize = new N3(preset.Width, preset.Height, preset.Length);
            // The weight low (spec 10), the mass as it is: the chassis (frame, axles, drawbar, floor) at the rails' middle,
            // the shell (walls, roof) at the box's centre, the load packed on the floor as a box of the body's width and
            // length, as high as puts the whole at the floor + 0.2 of the box's height (S: 0.15)
            ComTarget = (preset.Name == "S" ? 0.15f : 0.2f) * preset.Height;
            float loadMiddle = (ComTarget - ChassisShare * (LineY - FloorY) - ShellShare * preset.Height / 2f) / LoadShare;
            LoadHeight = Math.Clamp(2f * loadMiddle, 0.05f, preset.Height);
            float comY = ChassisShare * LineY + ShellShare * (FloorY + preset.Height / 2f) + LoadShare * (FloorY + LoadHeight / 2f);
            Com = new N3(0f, comY, BodyCentre.Z);
            // tongue share = (com z - axle z) / (0 - axle z)  =>  axle z = com z / (1 - share)
            AxleZ = Com.Z / (1f - Tongue);
            // A tandem's axles at least 0.9 m apart, further for big wheels: the car's wheel's diameter and 0.12 m between
            // the tyres (the Mammoth's 1.04 m wheels overlapped by 0.14 m at 0.9)
            Spacing = Math.Max(TandemSpacing, 2f * wheels.Radius + TandemGap);
            Axles = preset.Axles > 1 ? new[] { AxleZ + Spacing / 2f, AxleZ - Spacing / 2f } : new[] { AxleZ };
            float beside = preset.Width / 2f + TyreGap + wheels.Width / 2f;
            // L: the car's own track, or the body's width and a tyre, whichever is wider
            WheelX = preset.Axles > 1 ? Math.Max(beside, Math.Max(wheels.Track, preset.Width + wheels.Width) / 2f) : beside;
            RailX = preset.Width / 2f - 0.2f;
            Chamfered = preset.Name != "S";
        }

        // The lower edges' chamfer (M and L), and where the frame's rails end: square, as long as the body's flat bottom
        // (where the chamfers meet the floor); S's rails run full length. The ends stay square for lamps or shackles later.
        public float ChamferSize => Chamfered ? Math.Min(Chamfer, Preset.Height / 3f) : 0f;
        public float RailFront => Front - ChamferSize;
        public float RailBack => Back + ChamferSize;

        public float TonguePercent => 100f * (Com.Z - AxleZ) / (0f - AxleZ);

        // The step the Z must make, and whether it is within what the trailer can take (±0.6 m)
        public bool StepFits => Math.Abs(Step) <= MaxStep;
    }

    // The axle's look: its leaf springs (0.7 m long) and their top over the axle's centre at rest. Each spring hangs
    // from its two hangers by its ends and rests on the axle by its middle: the ends stay under the hangers, the middle
    // moves with the axle (its arch shortening as the axle rises), never higher than just under the frame's rails.
    internal static class AxleLook
    {
        public const float SpringHalf = 0.35f, SpringTop = 0.095f, Leaf = 0.03f, LeafWidth = 0.07f;
    }

    internal static class Look
    {
        public static readonly Preset[] Presets =
        {
            new() { Name = "S", Length = 2.4f, Width = 1.4f, Height = 0.9f, Drawbar = 1.2f, Axles = 1, Radius = 0.35f, Mass = 350f },
            new() { Name = "M", Length = 3.6f, Width = 1.8f, Height = 1.6f, Drawbar = 1.35f, Axles = 1, Radius = 0.38f, Mass = 800f },
            new() { Name = "L", Length = 4.6f, Width = 2.0f, Height = 2.2f, Drawbar = 1.5f, Axles = 2, Radius = 0.40f, Mass = 1400f },
        };

        private static readonly int[] Textures =
        {
            Shader.PropertyToID("_Main_Texture"), Shader.PropertyToID("_Livery_Texture"), Shader.PropertyToID("_AO_Texture"),
            Shader.PropertyToID("_BaseMap"), Shader.PropertyToID("_MainTex"),
        };
        private static readonly string[] TextureNames = { "_Main_Texture", "_Livery_Texture", "_AO_Texture", "_BaseMap", "_MainTex" };
        private static readonly int Mask = Shader.PropertyToID("_Mask_Texture");
        private static readonly int[] Colours = { Shader.PropertyToID("_BaseColor"), Shader.PropertyToID("_Color") };
        private static readonly string[] ColourNames = { "_BaseColor", "_Color" };
        private static readonly Color Grey = new(0.75f, 0.75f, 0.75f, 1f);

        // The body, the drawbar from the body's front to the coupling head (its tip on the origin) and the axle beams
        // The body (its lower front and rear edges chamfered on M and L), the level drawbar from the body's front to the
        // coupling head (its tip on the origin), a fender over each side's wheels, and the hangers from the floor down to
        // the axles
        // The body (its lower front and rear edges chamfered on M and L) on its frame rails, the Z drawbar from the
        // frame to the coupling head (its tip on the origin), the spring hangers under the rails, a fender over each
        // side's wheels. The axles and their springs are drawn with the wheels (AxleLook), as they move with them.
        // The body's side profile in (z, y), going round: front top, front low, (chamfer), back low, (chamfer), back top
        public static List<(float z, float y)> Profile(Layout l)
        {
            float bottom = l.FloorY, top = l.FloorY + l.Preset.Height, c = l.ChamferSize;
            if (c <= 0f)
                return new List<(float z, float y)> { (l.Front, top), (l.Front, bottom), (l.Back, bottom), (l.Back, top) };
            return new List<(float z, float y)>
            {
                (l.Front, top), (l.Front, bottom + c), (l.Front - c, bottom), (l.Back + c, bottom), (l.Back, bottom + c), (l.Back, top),
            };
        }

        // The collider's mesh: the chamfered body exactly (a convex prism), for a convex MeshCollider (spec 10)
        public static Mesh Hull(Layout l)
        {
            var shape = new Shape();
            shape.Prism(Profile(l), l.Preset.Width / 2f);
            return ToMesh(shape, "TestTrailer hull");
        }

        public static Shape Body(Layout l)
        {
            var shape = new Shape();
            var p = l.Preset;
            shape.Prism(Profile(l), p.Width / 2f);
            // The frame: two rails under the body's flat bottom (spec 10: as long as it, square ends), 0.12 m deep, a cross
            // member at each end
            float railY = l.RailBottom + Layout.Frame / 2f;
            foreach (float side in new[] { -1f, 1f })
                shape.Box(new N3(side * l.RailX, railY, (l.RailFront + l.RailBack) / 2f), new N3(0.04f, Layout.Frame / 2f, (l.RailFront - l.RailBack) / 2f));
            foreach (float z in new[] { l.RailFront - 0.04f, l.RailBack + 0.04f })
                shape.Box(new N3(0f, railY, z), new N3(l.RailX, Layout.Frame / 2f - 0.01f, 0.04f));
            // The Z drawbar: level at the drawbar's line from the frame's front, a 45° step to the coupling's height,
            // level again for the last 0.2 m into the coupling head
            var low = new N3(0f, l.LineY, l.StepStart);
            var high = new N3(0f, 0f, -Layout.HeadLength);
            if (Math.Abs(l.Step) > 0.01f)
            {
                // the two rods overlap by the bar's half width at the bend, so it shows no notch
                var along = N3.Normalize(high - low);
                shape.Rod(new N3(0f, l.LineY, l.RailFront - 0.04f), low + new N3(0f, 0f, Layout.BarHalf), Layout.BarHalf);
                shape.Rod(low - along * Layout.BarHalf, high, Layout.BarHalf);
            }
            else
                shape.Rod(new N3(0f, l.LineY, l.RailFront - 0.04f), high, Layout.BarHalf);
            shape.Box(new N3(0f, 0f, -Layout.HeadLength / 2f), new N3(0.06f, 0.05f, Layout.HeadLength / 2f));
            // The spring hangers: under the rails at each spring's ends, from the rails down to the springs' top at rest
            foreach (float z in l.Axles)
                foreach (float side in new[] { -1f, 1f })
                    foreach (float end in new[] { -AxleLook.SpringHalf, AxleLook.SpringHalf })
                        shape.Box(new N3(side * l.RailX, (l.RailBottom + l.AxleY + AxleLook.SpringTop) / 2f, z + end),
                            new N3(0.025f, Math.Max(0.005f, (l.RailBottom - l.AxleY - AxleLook.SpringTop) / 2f), 0.025f));
            // Fenders 0.13 m over the wheels' static top, joined to the body
            var w = l.Wheels;
            float first = l.Axles[0] + w.Radius + 0.05f, last = l.Axles[l.Axles.Length - 1] - w.Radius - 0.05f;
            foreach (float side in new[] { -1f, 1f })
            {
                shape.Box(new N3(side * l.WheelX, l.FenderY, (first + last) / 2f), new N3(w.Width / 2f + 0.04f, 0.02f, (first - last) / 2f));
                float inner = p.Width / 2f, outer = l.WheelX - w.Width / 2f - 0.04f;
                if (outer > inner + 0.01f)
                    shape.Box(new N3(side * (inner + outer) / 2f, l.FenderY, (first + last) / 2f), new N3((outer - inner) / 2f, 0.02f, (first - last) / 2f));
            }
            return shape;
        }

        // One axle as it moves with its wheels, round its own centre: the beam across from hub to hub
        public static Shape Axle(Layout l)
        {
            var shape = new Shape();
            shape.Box(new N3(0f, 0f, 0f), new N3(l.WheelX - l.Wheels.Width / 2f, 0.035f, 0.035f));
            return shape;
        }

        // A unit box (1 m each way round its centre): a leaf spring's half, scaled and turned every frame
        public static Shape Unit()
        {
            var shape = new Shape();
            shape.Box(new N3(0f, 0f, 0f), new N3(0.5f, 0.5f, 0.5f));
            return shape;
        }

        // One wheel round its own centre, its axis along x
        public static Shape Wheel(float radius, float width)
        {
            var shape = new Shape();
            shape.Wheel(new N3(0f, 0f, 0f), radius, width / 2f, 12);
            return shape;
        }

        public static Mesh ToMesh(Shape shape, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var points = new Vector3[shape.Positions.Count];
            var uvs = new Vector2[shape.Positions.Count];
            for (int i = 0; i < points.Length; i++)
            {
                var p = shape.Positions[i];
                points[i] = new Vector3(p.X, p.Y, p.Z);
                var uv = shape.Uvs[i];
                uvs[i] = new Vector2(uv.u, uv.v);
            }
            mesh.vertices = points;
            mesh.uv = uvs;
            mesh.SetTriangles(shape.Triangles.ToArray(), 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A copy of the car body's material (its largest renderer under Parts) with the textures plain white and the
        // colour light grey; or Unity's URP Lit when the copy has none of those properties or the preferences ask for it
        public static Material MakeMaterial(Transform root, out string how)
        {
            string why = "the preferences ask for it (LitShader)";
            Material copy = null;
            if (!Mod.LitShader.Value)
            {
                var body = BodyRenderer(root);
                var source = body != null ? body.sharedMaterial : null;
                if (source == null)
                    why = "no body renderer under Parts";
                else
                {
                    var material = new Material(source) { name = "TestTrailer" };
                    try
                    {
                        if (Plain(material, out var set))
                        {
                            how = $"a copy of '{body.name}''s material '{source.name}' (shader '{(material.shader != null ? material.shader.name : "?")}'): {string.Join(", ", set)}";
                            return material;
                        }
                        copy = material;
                        why = $"'{body.name}''s material '{source.name}' has none of {string.Join(", ", TextureNames)}, {string.Join(", ", ColourNames)}";
                    }
                    catch (Exception e)
                    {
                        // Nothing half-made left behind: the copy goes, URP Lit instead
                        Object.Destroy(material);
                        why = $"the copy of '{body.name}''s material failed ({e.Message})";
                    }
                }
            }
            Material lit = null;
            try
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    if (copy != null)
                    {
                        how = $"the body material's copy as it is: {why}, and Shader.Find(\"Universal Render Pipeline/Lit\") found nothing";
                        return copy;
                    }
                    shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
                    if (shader == null)
                        throw new InvalidOperationException($"no material for the box: {why}, and no URP Lit, Simple Lit or Standard shader found");
                    why += ", and Shader.Find(\"Universal Render Pipeline/Lit\") found nothing";
                }
                lit = new Material(shader) { name = "TestTrailer" };
                if (lit.HasProperty(Colours[0]))
                    lit.SetColor(Colours[0], Grey);
                how = $"{shader.name}, light grey: {why}";
            }
            catch (Exception)
            {
                if (copy != null)
                    Object.Destroy(copy);
                if (lit != null)
                    Object.Destroy(lit);
                throw;
            }
            if (copy != null)
                Object.Destroy(copy);
            return lit;
        }

        // The copy's textures plain white, its colour light grey, its glass and paint mask black; whether any of the
        // texture or colour properties was there
        private static bool Plain(Material material, out List<string> set)
        {
            set = new List<string>();
            for (int i = 0; i < Textures.Length; i++)
                if (material.HasProperty(Textures[i]))
                {
                    material.SetTexture(Textures[i], Texture2D.whiteTexture);
                    set.Add(TextureNames[i] + " white");
                }
            bool painted = set.Count > 0;
            for (int i = 0; i < Colours.Length; i++)
                if (material.HasProperty(Colours[i]))
                {
                    material.SetColor(Colours[i], Grey);
                    set.Add(ColourNames[i] + " grey");
                    painted = true;
                }
            if (material.HasProperty(Mask))
            {
                // The car shader's mask: G glass, B paint – neither on the box
                material.SetTexture(Mask, Texture2D.blackTexture);
                set.Add("_Mask_Texture black");
            }
            return painted;
        }

        // The car's largest drawn mesh under Parts: its body
        private static MeshRenderer BodyRenderer(Transform root)
        {
            var parts = root.Find("Parts");
            if (parts == null)
                return null;
            MeshRenderer best = null;
            float most = -1f;
            foreach (var renderer in parts.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (renderer == null || !renderer.enabled)
                    continue;
                var e = renderer.bounds.extents;
                float volume = e.x * e.y * e.z;
                if (volume > most)
                {
                    most = volume;
                    best = renderer;
                }
            }
            return best;
        }

        // A line on the screen, the game's own notification
        public static void Say(string text)
        {
            try
            {
                var hud = HUDManager.Instance;
                if (hud != null)
                    hud.ShowNotification(new Color(0.85f, 0.7f, 0.45f), text);
            }
            catch (Exception e)
            {
                Once.Warn("notification", $"the notification not shown: {e.Message}");
            }
        }
    }

    // A mesh made of flat faces, each face its own vertices
    internal sealed class Shape
    {
        public readonly List<N3> Positions = new();
        public readonly List<(float u, float v)> Uvs = new();
        public readonly List<int> Triangles = new();

        // A quad from its four corners laid out as c-e1-e2, c+e1-e2, c-e1+e2, c+e1+e2, facing outward. Its UVs span a
        // hair round the texture's middle: the plain textures read one colour, and the tangents still come out right.
        public void Quad(N3 a, N3 b, N3 c, N3 d, N3 outward)
        {
            int first = Positions.Count;
            Positions.Add(a);
            Positions.Add(b);
            Positions.Add(c);
            Positions.Add(d);
            Uvs.Add((0.5f, 0.5f));
            Uvs.Add((0.501f, 0.5f));
            Uvs.Add((0.5f, 0.501f));
            Uvs.Add((0.501f, 0.501f));
            bool flip = N3.Dot(N3.Cross(b - a, c - a), outward) < 0f;
            if (!flip)
            {
                Triangles.Add(first); Triangles.Add(first + 1); Triangles.Add(first + 2);
                Triangles.Add(first + 1); Triangles.Add(first + 3); Triangles.Add(first + 2);
            }
            else
            {
                Triangles.Add(first); Triangles.Add(first + 2); Triangles.Add(first + 1);
                Triangles.Add(first + 1); Triangles.Add(first + 2); Triangles.Add(first + 3);
            }
        }

        public void Triangle(N3 a, N3 b, N3 c, N3 outward)
        {
            int first = Positions.Count;
            Positions.Add(a);
            Positions.Add(b);
            Positions.Add(c);
            Uvs.Add((0.5f, 0.5f));
            Uvs.Add((0.501f, 0.5f));
            Uvs.Add((0.5f, 0.501f));
            bool flip = N3.Dot(N3.Cross(b - a, c - a), outward) < 0f;
            Triangles.Add(first);
            Triangles.Add(flip ? first + 2 : first + 1);
            Triangles.Add(flip ? first + 1 : first + 2);
        }

        // An axis-aligned box
        public void Box(N3 centre, N3 half) =>
            Box(centre, new N3(1f, 0f, 0f), new N3(0f, 1f, 0f), new N3(0f, 0f, 1f), half);

        // A box around centre on three axes (unit, square to each other) with half sizes along them
        public void Box(N3 centre, N3 ax, N3 ay, N3 az, N3 half)
        {
            var axes = new[] { ax * half.X, ay * half.Y, az * half.Z };
            for (int k = 0; k < 3; k++)
                foreach (float sign in new[] { -1f, 1f })
                {
                    var n = axes[k] * sign;
                    var e1 = axes[(k + 1) % 3];
                    var e2 = axes[(k + 2) % 3];
                    var c = centre + n;
                    Quad(c - e1 - e2, c + e1 - e2, c - e1 + e2, c + e1 + e2, n);
                }
        }

        // A prism across the trailer: a convex side profile in (z, y), from x - halfWidth to x + halfWidth, with its ends
        public void Prism(List<(float z, float y)> profile, float halfWidth, float x = 0f)
        {
            float cz = 0f, cy = 0f;
            foreach (var (z, y) in profile)
            {
                cz += z;
                cy += y;
            }
            cz /= profile.Count;
            cy /= profile.Count;
            for (int i = 0; i < profile.Count; i++)
            {
                var (z0, y0) = profile[i];
                var (z1, y1) = profile[(i + 1) % profile.Count];
                var outward = new N3(0f, (y0 + y1) / 2f - cy, (z0 + z1) / 2f - cz);
                Quad(new N3(x - halfWidth, y0, z0), new N3(x - halfWidth, y1, z1), new N3(x + halfWidth, y0, z0), new N3(x + halfWidth, y1, z1), outward);
                foreach (float side in new[] { -1f, 1f })
                    Triangle(new N3(x + side * halfWidth, cy, cz), new N3(x + side * halfWidth, y0, z0), new N3(x + side * halfWidth, y1, z1), new N3(side, 0f, 0f));
            }
        }

        // A square bar from a to b
        public void Rod(N3 a, N3 b, float half)
        {
            var d = b - a;
            float length = d.Length();
            if (length < 1e-4f)
                return;
            var z = d / length;
            var x = N3.Cross(z, new N3(0f, 1f, 0f));
            if (x.Length() < 1e-3f)
                x = N3.Cross(z, new N3(1f, 0f, 0f));
            x = N3.Normalize(x);
            var y = N3.Normalize(N3.Cross(z, x));
            Box((a + b) * 0.5f, x, y, z, new N3(half, half, length / 2f));
        }

        // A wheel: a cylinder across the car (its axis along x) with closed sides
        public void Wheel(N3 centre, float radius, float halfWidth, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                double t0 = 2.0 * Math.PI * i / sides, t1 = 2.0 * Math.PI * (i + 1) / sides, tm = (t0 + t1) / 2.0;
                N3 At(double t, float x) => new(centre.X + x, centre.Y + radius * (float)Math.Cos(t), centre.Z + radius * (float)Math.Sin(t));
                Quad(At(t0, -halfWidth), At(t1, -halfWidth), At(t0, halfWidth), At(t1, halfWidth),
                    new N3(0f, (float)Math.Cos(tm), (float)Math.Sin(tm)));
                foreach (float side in new[] { -1f, 1f })
                    Triangle(new N3(centre.X + side * halfWidth, centre.Y, centre.Z), At(t0, side * halfWidth), At(t1, side * halfWidth), new N3(side, 0f, 0f));
            }
        }
    }
}
