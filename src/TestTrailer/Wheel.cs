using System;
using UnityEngine;
using N3 = System.Numerics.Vector3;

namespace TestTrailer
{
    // One passive trailer wheel, the towing car's own size (Trailer Lab's PassiveWheel at its root): three rays down from
    // its mount – the tread's middle and two shoulders ahead and behind – find the ground; a spring and a damper hold the
    // trailer up at the contact. Sideways the tyre sticks: a stick point at the contact held by a stiff spring and damper
    // up to the peak grip (the car's tyre's lateral peak friction × the surface's grip, read per wheel from the game's
    // ground materials), past it the tyre slides at the limit grip and drags the stick point along, and sticks again
    // when it slides slower than the tyre's adherent speed – so a trailer standing on a slope does not creep. Forward it
    // rolls freely (no brake), with a little rolling resistance. Stepped once per physics step from Fusion's (Rig.Step).
    internal sealed class Wheel
    {
        // The spring's natural frequency (1.7 Hz) with its rate scaled to the wheel's load: the static compression is
        // g / ω², about 8.6 cm, whatever the size
        public const float Omega = 10.681f, Padding = 0.15f;
        private const float DampingK = 1.5f, RollingResistance = 0.015f, MinNormalUp = 0.45f;
        // The stick point's spring: 3 Hz on the wheel's load, critically damped
        private const float StickOmega = 2f * (float)Math.PI * 3f, StickZeta = 1f, Rebase = 1f;
        private const float SurfaceEvery = 0.25f;

        public readonly string Name;
        public readonly N3 Mount;               // the hub at full compression, in the trailer's space
        public readonly float Radius, Width, Travel, Load, StaticCompression;
        private readonly float _shoulder, _shoulderRadius, _spring, _damper, _maxForce, _stick, _stickDamper;

        public Transform Visual;
        public bool Grounded, Sliding;
        public int GroundId;                    // the ground collider's instance id
        public float Extension, HitDistance, Force, LateralSlip, Offset, Spin;
        public N3 Contact;
        private N3 _normal, _anchor;
        private bool _anchored;
        private int _missed;
        // Steps of missed rays the stick point outlives (a bump, a rut): past them it is dropped
        private const int KeepAnchor = 3;

        // Grip: the surface under the wheel (refreshed when it changes and four times a second) times the car's tyre
        public string Surface = "?";
        public float Grip = 1f, PeakMu, SlideMu;
        private int _surfaceId = int.MinValue;
        private float _surfaceAge;

        public Wheel(string name, N3 hubAtRest, float radius, float width, float travel, float load, float gravity)
        {
            Name = name;
            Radius = radius;
            Width = width;
            Travel = travel;
            Load = load;
            StaticCompression = Math.Min(gravity / (Omega * Omega), travel * 0.5f);
            Mount = hubAtRest + new N3(0f, Travel - StaticCompression, 0f);
            Extension = Travel - StaticCompression;
            _shoulder = radius * 0.5f;
            _shoulderRadius = (float)Math.Sqrt(radius * radius - _shoulder * _shoulder);
            _spring = load * Omega * Omega;
            _damper = DampingK * load * Omega;
            _maxForce = 4f * load * gravity;
            _stick = load * StickOmega * StickOmega;
            _stickDamper = 2f * StickZeta * load * StickOmega;
        }

        public float Compression => Travel - Extension;
        public N3 Hub => Mount - new N3(0f, Extension, 0f);

        // The ground under the wheel now, from its mount in the world: the shortest acceptable hit of the three rays.
        // `reach` lets the rays look further than the travel (at hitching, on a slope); the wheel then hangs at full travel.
        public bool Find(Rig rig, N3 mount, N3 up, N3 forward, float reach = 0f)
        {
            Grounded = false;
            Extension = Travel + reach;
            var origin = mount + up * Padding;
            Ray(rig, origin, Radius, up, reach);
            Ray(rig, origin + forward * _shoulder, _shoulderRadius, up, reach);
            Ray(rig, origin - forward * _shoulder, _shoulderRadius, up, reach);
            if (Extension < 0f)
                Extension = 0f;
            if (Extension > Travel)
                Extension = Travel;
            return Grounded;
        }

        private void Ray(Rig rig, N3 origin, float tread, N3 up, float reach)
        {
            int count = rig.Scene.Raycast(Rig.U(origin), Rig.U(-up), rig.Hits, Padding + Travel + reach + tread, rig.GroundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && i < rig.Hits.Length; i++)
            {
                var hit = rig.Hits[i];
                float candidate = hit.m_Distance - Padding - tread;
                if (candidate >= Extension || hit.m_Normal.y < MinNormalUp)
                    continue;
                if (!rig.IsGround(hit))
                    continue;
                Grounded = true;
                GroundId = hit.m_Collider;
                HitDistance = hit.m_Distance;
                Contact = new N3(hit.m_Point.x, hit.m_Point.y, hit.m_Point.z);
                Extension = candidate;
                _normal = new N3(hit.m_Normal.x, hit.m_Normal.y, hit.m_Normal.z);
            }
        }

        // The surface's grip under the wheel and the friction it gives with the car's tyre
        public void Refresh(Rig rig)
        {
            if (!Grounded)
                return;
            rig.Surface(this);
            _surfaceId = GroundId;
            _surfaceAge = 0f;
            PeakMu = rig.TirePeak * Grip;
            SlideMu = rig.TireSlide * Grip;
        }

        // One physics step: the ground, then the spring, damper, stick-slip grip and rolling forces at the contact
        public void Step(Rig rig, N3 position, System.Numerics.Quaternion rotation, N3 up, N3 forward, float dt)
        {
            Force = 0f;
            LateralSlip = 0f;
            var mount = position + N3.Transform(Mount, rotation);
            if (!Find(rig, mount, up, forward))
            {
                if (++_missed >= KeepAnchor)
                {
                    _anchored = false;
                    Sliding = false;
                    Offset = 0f;
                }
                return;
            }
            _missed = 0;
            _surfaceAge += dt;
            if (GroundId != _surfaceId || _surfaceAge >= SurfaceEvery)
                Refresh(rig);
            var hub = mount - up * Extension;
            var v = rig.Body.GetPointVelocity(Rig.U(hub));
            var velocity = new N3(v.x, v.y, v.z);
            var normal = _normal;
            float normalForce = Math.Clamp(_spring * Compression - _damper * N3.Dot(velocity, normal), 0f, _maxForce);
            var tangent = forward - normal * N3.Dot(forward, normal);
            if (tangent.LengthSquared() < 1e-6f)
                return;
            tangent = N3.Normalize(tangent);
            var lateral = N3.Cross(normal, tangent);
            float forwardSpeed = N3.Dot(velocity, tangent);
            // The tyre's sideways speed where it meets the ground, not at the hub
            var c = rig.Body.GetPointVelocity(Rig.U(Contact));
            LateralSlip = N3.Dot(new N3(c.x, c.y, c.z), lateral);

            // The stick point: only its sideways offset counts (the wheel rolls on); a sideways jump drops it
            if (!_anchored)
            {
                _anchor = Contact;
                _anchored = true;
                Sliding = false;
            }
            var away = Contact - _anchor;
            _anchor += tangent * N3.Dot(away, tangent) + normal * N3.Dot(away, normal);
            Offset = N3.Dot(Contact - _anchor, lateral);
            if (Math.Abs(Offset) > Rebase)
            {
                _anchor = Contact;
                Offset = 0f;
                Sliding = false;
            }
            float spring = -_stick * Offset;
            float sideways = spring - _stickDamper * LateralSlip;
            float peak = PeakMu * normalForce, slide = SlideMu * normalForce;
            // Sticking again: slow enough, and the spring alone within the grip
            if (Sliding && Math.Abs(LateralSlip) < rig.AdherentSpeed && Math.Abs(spring) <= peak)
                Sliding = false;
            if (Sliding || Math.Abs(sideways) > peak)
            {
                Sliding = true;
                float push = Math.Abs(LateralSlip) > 1e-3f ? -Math.Sign(LateralSlip) : -Math.Sign(Offset);
                sideways = push * slide;
                // The stick point dragged along, so the spring alone would give the sliding force
                Offset = -sideways / _stick;
                _anchor = Contact - lateral * Offset;
            }
            float rolling = -Math.Clamp(forwardSpeed / 0.5f, -1f, 1f) * RollingResistance * normalForce;
            var force = normal * normalForce + lateral * sideways + tangent * rolling;
            rig.Body.AddForceAtPosition(Rig.U(force), Rig.U(Contact), ForceMode.Force);
            Force = force.Y;
            Spin = (Spin + forwardSpeed / Radius * (180f / (float)Math.PI) * dt) % 360f;
        }
    }
}
