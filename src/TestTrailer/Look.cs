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
    // coupling: the drawbar level at the coupling's height, the body's floor there too (so the clearance under the body
    // is the hitch's height), the wheels beside the body at ground + radius, the axle (the tandem's middle) where the
    // body's centre of mass puts 11 % of the mass on the ball
    internal sealed class Layout
    {
        public const float TandemSpacing = 0.9f, TyreGap = 0.04f, Tongue = 0.11f;
        public const float BarHalf = 0.05f, HeadLength = 0.2f, Chamfer = 0.25f;

        public readonly Preset Preset;
        public readonly WheelSpec Wheels;
        public readonly float Ground, FloorY, Front, Back, AxleZ, WheelX, HubY;
        public readonly float[] Axles;
        public readonly N3 BodyCentre, BodySize, Com;
        public readonly bool Chamfered;

        public Layout(Preset preset, float ground, WheelSpec wheels)
        {
            Preset = preset;
            Wheels = wheels;
            Ground = ground;
            FloorY = 0f;
            Front = -preset.Drawbar;
            Back = Front - preset.Length;
            BodyCentre = new N3(0f, FloorY + preset.Height / 2f, (Front + Back) / 2f);
            BodySize = new N3(preset.Width, preset.Height, preset.Length);
            // The centre of mass low in the box: a third of its height over the floor, a quarter for L
            float comHeight = preset.Axles > 1 ? preset.Height / 4f : preset.Height / 3f;
            Com = new N3(0f, FloorY + comHeight, BodyCentre.Z);
            // tongue share = (com z - axle z) / (0 - axle z)  =>  axle z = com z / (1 - share)
            AxleZ = Com.Z / (1f - Tongue);
            Axles = preset.Axles > 1 ? new[] { AxleZ + TandemSpacing / 2f, AxleZ - TandemSpacing / 2f } : new[] { AxleZ };
            float beside = preset.Width / 2f + TyreGap + wheels.Width / 2f;
            // L: the car's own track, or the body's width and a tyre, whichever is wider
            WheelX = preset.Axles > 1 ? Math.Max(beside, Math.Max(wheels.Track, preset.Width + wheels.Width) / 2f) : beside;
            HubY = ground + wheels.Radius;
            Chamfered = preset.Name != "S";
        }

        public float TonguePercent => 100f * (Com.Z - AxleZ) / (0f - AxleZ);
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
        public static Shape Body(Layout l)
        {
            var shape = new Shape();
            var p = l.Preset;
            float bottom = l.FloorY, top = l.FloorY + p.Height, c = l.Chamfered ? Math.Min(Layout.Chamfer, p.Height / 3f) : 0f;
            // The side profile in (z, y), going round: front top, front low, (chamfer), back low, (chamfer), back top
            var profile = new List<(float z, float y)>
            {
                (l.Front, top), (l.Front, bottom + c), (l.Front - c, bottom), (l.Back + c, bottom), (l.Back, bottom + c), (l.Back, top),
            };
            if (c <= 0f)
                profile = new List<(float z, float y)> { (l.Front, top), (l.Front, bottom), (l.Back, bottom), (l.Back, top) };
            shape.Prism(profile, p.Width / 2f);
            shape.Rod(new N3(0f, -Layout.BarHalf, l.Front + 0.01f), new N3(0f, -Layout.BarHalf, -Layout.HeadLength), Layout.BarHalf);
            shape.Box(new N3(0f, 0f, -Layout.HeadLength / 2f), new N3(0.06f, 0.05f, Layout.HeadLength / 2f));
            var w = l.Wheels;
            float first = l.Axles[0] + w.Radius + 0.05f, last = l.Axles[l.Axles.Length - 1] - w.Radius - 0.05f;
            float fenderY = Math.Max(l.HubY + w.Radius + 0.06f, bottom - 0.02f);
            foreach (float side in new[] { -1f, 1f })
            {
                shape.Box(new N3(side * l.WheelX, fenderY, (first + last) / 2f), new N3(w.Width / 2f + 0.04f, 0.02f, (first - last) / 2f));
                // the fender's inner edge up to the body when it sits under the floor
                if (fenderY < bottom)
                    shape.Box(new N3(side * (p.Width / 2f - 0.02f), (fenderY + bottom) / 2f, (first + last) / 2f), new N3(0.02f, (bottom - fenderY) / 2f, (first - last) / 2f));
            }
            foreach (float z in l.Axles)
            {
                shape.Box(new N3(0f, l.HubY, z), new N3(l.WheelX - w.Width / 2f, 0.035f, 0.035f));
                if (l.HubY < bottom)
                    foreach (float side in new[] { -1f, 1f })
                        shape.Box(new N3(side * (p.Width / 2f - 0.12f), (l.HubY + bottom) / 2f, z), new N3(0.04f, (bottom - l.HubY) / 2f, 0.04f));
            }
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

        // A prism across the trailer: a convex side profile in (z, y), from -halfWidth to +halfWidth in x, with its ends
        public void Prism(List<(float z, float y)> profile, float halfWidth)
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
                Quad(new N3(-halfWidth, y0, z0), new N3(-halfWidth, y1, z1), new N3(halfWidth, y0, z0), new N3(halfWidth, y1, z1), outward);
                foreach (float side in new[] { -1f, 1f })
                    Triangle(new N3(side * halfWidth, cy, cz), new N3(side * halfWidth, y0, z0), new N3(side * halfWidth, y1, z1), new N3(side, 0f, 0f));
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
