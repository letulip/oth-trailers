using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using N3 = System.Numerics.Vector3;

namespace TestTrailer
{
    // An author's trailer body baked by tools/bake_model.py and embedded in the DLL (Models/<name>.bin): its mesh in the
    // trailer's space with the body's bottom at y 0 and its front at z 0 (back toward -z), one submesh per colour, a
    // collider mesh of the body, and the measures the layout places it by (docs/specs/11-first-model.md). Read once;
    // nothing of Unity's is touched here, the meshes are made at hitching (Look.ModelMesh, Look.ModelHull).
    internal sealed class TrailerModel
    {
        public readonly string Name;
        public float Length, Width, Height, AxleFromFront, WheelX, RailX, ArchFront, ArchBack, ArchTop;
        public N3[] Positions, Normals;
        public readonly List<(float r, float g, float b, float a, string material, int[] indices)> Parts = new();
        public N3[] ColliderPositions;
        public int[] ColliderIndices;
        public int Triangles;

        private static readonly Dictionary<string, TrailerModel> Cache = new();
        private static readonly Dictionary<string, string> Failures = new();

        private TrailerModel(string name) => Name = name;

        // The model, or null with why (logged once by the caller)
        public static TrailerModel Get(string name, out string why)
        {
            why = null;
            if (Cache.TryGetValue(name, out var model))
                return model;
            if (Failures.TryGetValue(name, out why))
                return null;
            try
            {
                string resource = $"TestTrailer.Models.{name}.bin";
                using var stream = typeof(TrailerModel).Assembly.GetManifestResourceStream(resource)
                                   ?? throw new InvalidDataException($"no embedded resource '{resource}'");
                using var reader = new BinaryReader(stream, Encoding.UTF8);
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "OTHM")
                    throw new InvalidDataException("not a baked model (no OTHM header)");
                int version = reader.ReadInt32();
                if (version != 1)
                    throw new InvalidDataException($"version {version}, this build reads 1");
                model = new TrailerModel(name)
                {
                    Length = reader.ReadSingle(), Width = reader.ReadSingle(), Height = reader.ReadSingle(),
                    AxleFromFront = reader.ReadSingle(), WheelX = reader.ReadSingle(), RailX = reader.ReadSingle(),
                    ArchFront = reader.ReadSingle(), ArchBack = reader.ReadSingle(), ArchTop = reader.ReadSingle(),
                };
                int vertices = reader.ReadInt32();
                model.Positions = new N3[vertices];
                model.Normals = new N3[vertices];
                for (int i = 0; i < vertices; i++)
                {
                    model.Positions[i] = new N3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    model.Normals[i] = new N3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                }
                int parts = reader.ReadInt32();
                for (int p = 0; p < parts; p++)
                {
                    float r = reader.ReadSingle(), g = reader.ReadSingle(), b = reader.ReadSingle(), a = reader.ReadSingle();
                    reader.ReadSingle();   // metallic: not used by the car's shader here
                    reader.ReadSingle();   // smoothness
                    string material = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
                    var indices = new int[reader.ReadInt32()];
                    for (int i = 0; i < indices.Length; i++)
                    {
                        indices[i] = reader.ReadInt32();
                        if (indices[i] < 0 || indices[i] >= vertices)
                            throw new InvalidDataException($"index {indices[i]} out of {vertices} vertices in part '{material}'");
                    }
                    model.Parts.Add((r, g, b, a, material, indices));
                    model.Triangles += indices.Length / 3;
                }
                int hull = reader.ReadInt32();
                model.ColliderPositions = new N3[hull];
                for (int i = 0; i < hull; i++)
                    model.ColliderPositions[i] = new N3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                model.ColliderIndices = new int[reader.ReadInt32()];
                for (int i = 0; i < model.ColliderIndices.Length; i++)
                    model.ColliderIndices[i] = reader.ReadInt32();
                Cache[name] = model;
                return model;
            }
            catch (Exception e)
            {
                why = $"{e.GetType().Name}: {e.Message}";
                Failures[name] = why;
                return null;
            }
        }

        public string Describe() =>
            $"model '{Name}' (baked from the author's FBX: {Positions.Length} vertices, {Triangles} triangles, {Parts.Count} colours; " +
            $"body {Length:0.00} x {Width:0.00} x {Height:0.00} m, the axle {AxleFromFront:0.00} m behind its front under the arches " +
            $"(z {ArchFront:0.00}..{ArchBack:0.00} from the front, {ArchTop:0.00} m high), wheels at ±{WheelX:0.000} m, rails at ±{RailX:0.00} m)";
    }
}
