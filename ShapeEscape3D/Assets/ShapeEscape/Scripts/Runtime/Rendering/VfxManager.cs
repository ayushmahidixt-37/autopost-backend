using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Lightweight particle effects built in code: removal puffs, completion confetti and a sparkle ring.
    /// Each effect is a single reused ParticleSystem (no per-effect allocations); counts scale with the
    /// quality profile so low-end devices stay smooth.
    /// </summary>
    public sealed class VfxManager : MonoBehaviour
    {
        ParticleSystem _puff;
        ParticleSystem _confetti;
        ParticleSystem _sparkle;
        float _countScale = 1f;

        static readonly string[] ConfettiColors = { "#FF6B6B", "#FFD166", "#06D6A0", "#4ECDC4", "#7B61FF", "#FF8FAB", "#FF9F1C" };

        public void Initialize()
        {
            _puff = CreateSystem("Puff", TextureFactory.SoftCircle, 64);
            var main = _puff.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.32f);
            main.gravityModifier = 0f;
            var shape = _puff.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            FadeOut(_puff);
            var size = _puff.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));

            _confetti = CreateSystem("Confetti", TextureFactory.SoftSquare, 160);
            main = _confetti.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
            main.gravityModifier = 1.1f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var grad = new Gradient();
            var keys = new GradientColorKey[ConfettiColors.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = new GradientColorKey(ColorUtil.Hex(ConfettiColors[i]), i / (float)(keys.Length - 1));
            grad.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };
            shape = _confetti.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var rot = _confetti.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            FadeOut(_confetti);

            _sparkle = CreateSystem("Sparkle", TextureFactory.Sparkle, 48);
            main = _sparkle.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(ColorUtil.Hex("#FFF3B0"), Color.white);
            shape = _sparkle.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f;
            shape.rotation = new Vector3(0f, 0f, 0f);
            FadeOut(_sparkle);
        }

        /// <summary>0.4..1 — fraction of particles to emit (quality setting).</summary>
        public void SetQuality(float countScale)
        {
            _countScale = Mathf.Clamp(countScale, 0.25f, 1f);
        }

        static ParticleSystem CreateSystem(string name, Texture2D tex, int maxParticles)
        {
            var go = new GameObject("VFX " + name);
            go.SetActive(false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialLibrary.Fx(tex);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        static void FadeOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        int Scaled(int count) { return Mathf.Max(1, Mathf.RoundToInt(count * _countScale)); }

        public void Puff(Vector3 position, Color color)
        {
            if (_puff == null) return;
            var p = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true, startColor = ColorUtil.Brighten(color, 0.3f) };
            _puff.Emit(p, Scaled(7));
        }

        public void Confetti(Vector3 center, float radius)
        {
            if (_confetti == null) return;
            _confetti.transform.position = center + Vector3.down * radius * 0.6f;
            _confetti.Emit(Scaled(110));
        }

        public void SparkleRing(Vector3 center, float radius)
        {
            if (_sparkle == null) return;
            _sparkle.transform.position = center;
            var shape = _sparkle.shape;
            shape.radius = radius * 1.1f;
            _sparkle.Emit(Scaled(28));
        }
    }
}
