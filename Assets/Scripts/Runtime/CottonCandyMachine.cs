using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// わたあめ機。スイッチを入れるとヒーターが温まりヘッドが回り、
    /// ざらめが溶けて糸になって飛び出す。糸はボウルの中にある棒 (わたあめ) に引っかかる。
    /// </summary>
    public sealed class CottonCandyMachine : MonoBehaviour
    {
        public enum Status { Off, Empty, Heating, Making, Cooling }

        public SugarHopper Hopper { get; } = new SugarHopper(GameConfig.HopperCapacity);
        public bool IsOn { get; private set; }
        public float Heat { get; private set; }
        public float Spin { get; private set; }
        /// <summary>直近に棒が受け止めた割合 (0..1)。UI のヒント用。</summary>
        public float CatchRate { get; private set; }

        public Status CurrentStatus
        {
            get
            {
                if (!IsOn) return Heat > 0.05f ? Status.Cooling : Status.Off;
                if (Hopper.IsEmpty) return Status.Empty;
                return Heat < 0.5f ? Status.Heating : Status.Making;
            }
        }

        /// <summary>ざらめを入れている最中か。</summary>
        public bool IsPouring => pourRemaining > 0f;

        public CottonCandy Candy { get; set; }

        Transform head, switchLever, lamp;
        Quaternion switchRest;
        Material lampMaterial;
        ParticleSystem floss, pour;
        SoundFx sound;
        float pourRemaining;
        Color pourColor;
        float headAngle;
        float caughtSmoothed, producedSmoothed;

        public void Build(SoundFx soundFx)
        {
            sound = soundFx;
            var parts = ModelLoader.LoadMachine(transform);
            parts.TryGetValue("Head", out head);
            parts.TryGetValue("Switch", out switchLever);
            parts.TryGetValue("Lamp", out lamp);
            if (switchLever != null) switchRest = switchLever.localRotation;
            if (lamp != null)
            {
                lampMaterial = new Material(ModelLoader.MaterialFor("Lamp")) { name = "LampInstance" };
                lamp.GetComponent<MeshRenderer>().sharedMaterial = lampMaterial;
            }

            floss = CreateFlossParticles();
            pour = CreatePourParticles();
            CreateShadow();
        }

        public void ToggleSwitch()
        {
            IsOn = !IsOn;
            sound?.PlayClick();
        }

        /// <summary>ざらめを入れる。実際に入った量を返す。</summary>
        public float AddSugar(float grams, int colorIndex)
        {
            float added = Hopper.Add(grams, colorIndex);
            if (added > 0f)
            {
                pourRemaining = 0.6f;
                pourColor = CandyPalette.Evaluate(colorIndex, Time.time);
                sound?.PlayPour();
            }
            return added;
        }

        /// <summary>スイッチ (3D モデル) がクリックされたか判定する。</summary>
        public bool HitSwitch(Ray ray)
        {
            if (switchLever == null) return false;
            Vector3 center = switchLever.position + switchLever.TransformDirection(new Vector3(0f, -0.02f, 0f));
            Vector3 toCenter = center - ray.origin;
            float along = Vector3.Dot(toCenter, ray.direction);
            return along > 0f && (toCenter - ray.direction * along).magnitude < 0.045f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Heat = Mathf.MoveTowards(Heat, IsOn ? 1f : 0f, dt / (IsOn ? GameConfig.HeatUpSeconds : GameConfig.CoolDownSeconds));
            Spin = Mathf.MoveTowards(Spin, IsOn ? 1f : 0f, dt / GameConfig.SpinUpSeconds);

            Produce(dt);
            UpdateVisuals(dt);
        }

        void Produce(float dt)
        {
            if (Heat < 0.5f || Spin < 0.8f || Hopper.IsEmpty)
            {
                caughtSmoothed = Mathf.Lerp(caughtSmoothed, 0f, dt * 2f);
                producedSmoothed = Mathf.Lerp(producedSmoothed, 0f, dt * 2f);
                CatchRate = producedSmoothed > 1e-3f ? caughtSmoothed / producedSmoothed : 0f;
                return;
            }

            float rate = GameConfig.FlossGramsPerSecond * Mathf.InverseLerp(0.5f, 1f, Heat);
            float grams = Hopper.Consume(rate * dt, out int colorIndex);
            if (grams <= 0f) return;

            Color color = CandyPalette.Evaluate(colorIndex, Time.time);
            int rays = Mathf.Max(1, Mathf.RoundToInt(grams * GameConfig.RaysPerGram));
            float perRay = grams / rays;
            float caught = 0f;
            for (int i = 0; i < rays; i++)
            {
                if (Candy != null && ShootFloss(perRay, color, colorIndex)) caught += perRay;
            }
            caughtSmoothed = Mathf.Lerp(caughtSmoothed, caught / dt, dt * 3f);
            producedSmoothed = Mathf.Lerp(producedSmoothed, grams / dt, dt * 3f);
            CatchRate = caughtSmoothed / Mathf.Max(producedSmoothed, 1e-4f);

            var main = floss.main;
            main.startColor = color;
            floss.Emit(Mathf.Max(1, rays / 2));
        }

        /// <summary>ヘッドから糸を 1 本飛ばし、わたあめの表面か棒に当たればそこに積もらせる。</summary>
        bool ShootFloss(float grams, Color color, int colorIndex)
        {
            Transform stick = Candy.transform;
            if (!FlossPhysics.Aim(transform.position + GameConfig.HeadCenter, stick.position, stick.up,
                    Candy.ApproxRadius, Candy.Field.Size.y, out var shot))
                return false;
            return Candy.TryCatch(shot.Origin, shot.Direction, shot.MaxDistance, grams, color, colorIndex);
        }

        void UpdateVisuals(float dt)
        {
            headAngle += Spin * 1440f * dt;
            if (head != null) head.localRotation = Quaternion.AngleAxis(headAngle, Vector3.up);

            if (switchLever != null)
            {
                Quaternion target = switchRest * Quaternion.AngleAxis(IsOn ? -28f : 28f, Vector3.right);
                switchLever.localRotation = Quaternion.Slerp(switchLever.localRotation, target, 1f - Mathf.Exp(-dt * 20f));
            }

            if (lampMaterial != null)
            {
                float glow = IsOn ? 0.35f + 0.65f * Heat : 0f;
                lampMaterial.SetColor("_Emission", new Color(1f, 0.25f, 0.3f) * glow);
            }

            if (pourRemaining > 0f)
            {
                pourRemaining -= dt;
                var main = pour.main;
                main.startColor = pourColor;
                pour.Emit(Mathf.CeilToInt(220f * dt));
            }

            sound?.SetMotor(Spin, Heat);
        }

        ParticleSystem CreateFlossParticles()
        {
            var go = new GameObject("FlossParticles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = GameConfig.HeadCenter;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.006f);
            main.gravityModifier = -0.03f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = GameConfig.HeadRadius;
            shape.radiusThickness = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 2f;
            renderer.sharedMaterial = WataameMaterials.Particle("FlossParticle");
            ps.Play();
            return ps;
        }

        ParticleSystem CreatePourParticles()
        {
            var go = new GameObject("PourParticles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = GameConfig.HeadCenter + Vector3.up * 0.42f;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = 0.29f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.007f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1000;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.015f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = WataameMaterials.Particle("SugarParticle");
            ps.Play();
            return ps;
        }

        void CreateShadow()
        {
            var go = new GameObject("Shadow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = MeshBuilder.Quad("Shadow", 1.0f);
            var mat = new Material(WataameMaterials.Particle("Shadow")) { name = "ShadowInstance" };
            mat.SetColor("_Tint", new Color(0.45f, 0.25f, 0.35f, 0.45f));
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
