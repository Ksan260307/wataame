using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// ゲームの入口。シーンに置かなくても起動時に自動で生成され、
    /// カメラ・わたあめ機・棒・UI をすべてコードで組み立てる。
    /// </summary>
    public sealed class WataameGame : MonoBehaviour
    {
        public enum Mode { Playing, Showcase }

        public Mode CurrentMode { get; private set; } = Mode.Playing;
        public GameInput Input { get; } = new GameInput();
        public CottonCandyMachine Machine { get; private set; }
        public StickController Stick { get; private set; }
        public CottonCandy Candy { get; private set; }
        public CameraRig Rig { get; private set; }
        public SoundFx Sound { get; private set; }
        public CandyStats Result { get; private set; }

        public float SugarAmount { get; set; } = GameConfig.SugarDefault;
        public int ColorIndex { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (FindAnyObjectByType<WataameGame>() == null)
                new GameObject("WataameGame").AddComponent<WataameGame>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            WataameMaterials.ApplyLighting();

            Sound = new GameObject("Sound").AddComponent<SoundFx>();
            Sound.transform.SetParent(transform, false);

            Rig = new GameObject("Camera").AddComponent<CameraRig>();
            Rig.transform.SetParent(transform, false);

            BuildEnvironment();

            Machine = new GameObject("WataameMachine").AddComponent<CottonCandyMachine>();
            Machine.transform.SetParent(transform, false);
            Machine.Build(Sound);

            Stick = new GameObject("Stick").AddComponent<StickController>();
            Stick.transform.SetParent(transform, false);
            Stick.transform.position = StickController.HomePosition;
            ModelLoader.LoadStick(Stick.transform);
            Candy = Stick.gameObject.AddComponent<CottonCandy>();
            Machine.Candy = Candy;

            gameObject.AddComponent<WataameUI>().Game = this;
            Autopilot.AttachIfRequested(this);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool showcase = CurrentMode == Mode.Showcase;
            Stick.Showcase = showcase;
            Stick.Drive(Input, Rig.transform, dt);
            Rig.Drive(Input, showcase, Candy.WorldCenter, Mathf.Max(0.08f, Candy.ApproxRadius), dt);
        }

        void LateUpdate() => Input.EndFrame();

        void OnApplicationFocus(bool focus)
        {
            if (!focus) Input.ReleaseAll();
        }

        public void PourSugar() => Machine.AddSugar(SugarAmount, ColorIndex);

        public void ToggleSwitch() => Machine.ToggleSwitch();

        public void NewStick()
        {
            Candy.ResetCandy();
            Stick.ResetPose();
            CurrentMode = Mode.Playing;
        }

        public void Finish()
        {
            Result = Candy.Evaluate();
            CurrentMode = Mode.Showcase;
            Sound.PlayDone();
        }

        public void BackToPlay() => CurrentMode = Mode.Playing;

        void BuildEnvironment()
        {
            var env = new GameObject("Environment").transform;
            env.SetParent(transform, false);

            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(env, false);
            backdrop.AddComponent<MeshFilter>().sharedMesh = MeshBuilder.InvertedSphere("Backdrop", 12f);
            backdrop.AddComponent<MeshRenderer>().sharedMaterial = WataameMaterials.Backdrop();

            // まるいテーブル
            var table = new GameObject("Table");
            table.transform.SetParent(env, false);
            table.AddComponent<MeshFilter>().sharedMesh = MeshBuilder.Lathe("Table", new[]
            {
                new Vector2(0f, -0.06f), new Vector2(1.18f, -0.06f), new Vector2(1.2f, -0.05f),
                new Vector2(1.2f, -0.01f), new Vector2(1.18f, 0f), new Vector2(0f, 0f),
            }, 96);
            table.AddComponent<MeshRenderer>().sharedMaterial = WataameMaterials.Lit("Table", new Color(1f, 0.96f, 0.90f), 0.2f, 0f, 0.15f);
        }
    }
}
