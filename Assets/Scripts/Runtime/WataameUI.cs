using System.Collections.Generic;
using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// シンプルな UI。左: ざらめ、下: スイッチ、右: 棒。できあがり時は結果カードだけを出す。
    /// 画面の何もないところでのマウス操作は <see cref="GameInput"/> に渡す。
    /// </summary>
    public sealed class WataameUI : MonoBehaviour
    {
        const float RefHeight = 860f;
        const float MinWidth = 1100f;
        const float PanelWidth = 300f;
        const float Pad = 16f;

        public WataameGame Game;

        UiStyles s;
        float scale = 1f;
        Vector2 view;
        bool showHelp = true;
        readonly List<Rect> panels = new List<Rect>();

        void OnGUI()
        {
            if (Game == null) return;
            if (s == null) s = new UiStyles();

            scale = Mathf.Max(0.4f, Mathf.Min(Screen.height / RefHeight, Screen.width / MinWidth));
            view = new Vector2(Screen.width / scale, Screen.height / scale);

            Event e = Event.current;
            GUI.matrix = Matrix4x4.identity;
            if (e.isMouse || e.isKey || e.type == EventType.ScrollWheel) HandleWorldInput(e);
            if (e.type == EventType.Repaint) panels.Clear();

            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            DrawTitle();
            if (Game.CurrentMode == WataameGame.Mode.Showcase)
            {
                DrawResult();
            }
            else
            {
                DrawSugarPanel();
                DrawSwitchPanel();
                DrawStickPanel();
                DrawHelp();
            }
        }

        void HandleWorldInput(Event e)
        {
            Vector2 virtualMouse = e.mousePosition / scale;
            bool overUi = false;
            foreach (var r in panels)
                if (r.Contains(virtualMouse)) overUi = true;

            // 3D のスイッチを直接クリックしても ON/OFF できる
            if (e.type == EventType.MouseDown && e.button == 0 && !overUi && Game.CurrentMode == WataameGame.Mode.Playing)
            {
                Ray ray = Game.Rig.Camera.ScreenPointToRay(new Vector3(e.mousePosition.x, Screen.height - e.mousePosition.y, 0f));
                if (Game.Machine.HitSwitch(ray))
                {
                    Game.ToggleSwitch();
                    e.Use();
                    return;
                }
            }
            Game.Input.Process(e, overUi);
        }

        Rect BeginPanel(Rect r)
        {
            if (Event.current.type == EventType.Repaint) panels.Add(r);
            GUI.Box(r, GUIContent.none, s.Panel);
            return new Rect(r.x + Pad, r.y + Pad, r.width - Pad * 2f, r.height - Pad * 2f);
        }

        void DrawTitle()
        {
            var r = new Rect(24f, 16f, 600f, 60f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), "わたあめメーカー", s.TitleShadow);
            GUI.Label(r, "わたあめメーカー", s.Title);
        }

        void DrawSugarPanel()
        {
            var machine = Game.Machine;
            Rect c = BeginPanel(new Rect(18f, 86f, PanelWidth, 352f));
            float y = c.y;

            GUI.Label(new Rect(c.x, y, c.width, 26f), "① ざらめを えらぶ", s.Heading);
            y += 38f;

            GUI.Label(new Rect(c.x, y, c.width, 22f), "りょう", s.Label);
            GUI.Label(new Rect(c.x, y, c.width, 22f), $"{Game.SugarAmount:0} g", s.Value);
            y += 28f;
            Bar(new Rect(c.x, y + 7f, c.width, 12f), 1f, UiStyles.Track);
            Bar(new Rect(c.x, y + 7f, c.width, 12f), Mathf.InverseLerp(GameConfig.SugarMin, GameConfig.SugarMax, Game.SugarAmount), new Color(1f, 0.75f, 0.83f));
            Game.SugarAmount = Mathf.Round(GUI.HorizontalSlider(new Rect(c.x, y, c.width, 26f), Game.SugarAmount, GameConfig.SugarMin, GameConfig.SugarMax, s.Slider, s.Thumb));
            y += 40f;

            GUI.Label(new Rect(c.x, y, c.width, 22f), "いろ", s.Label);
            GUI.Label(new Rect(c.x, y, c.width, 22f), CandyPalette.Entries[Game.ColorIndex].Name, s.Value);
            y += 30f;
            const float sw = 32f, gap = 5f;
            for (int i = 0; i < CandyPalette.Count; i++)
            {
                var r = new Rect(c.x + i * (sw + gap), y, sw, sw);
                var old = GUI.color;
                GUI.color = CandyPalette.Evaluate(i, Time.time);
                GUI.DrawTexture(r, s.Circle);
                GUI.color = i == Game.ColorIndex ? UiStyles.Pink : new Color(0.85f, 0.78f, 0.82f);
                GUI.DrawTexture(i == Game.ColorIndex ? Expand(r, 4f) : r, s.Ring);
                GUI.color = old;
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Game.ColorIndex = i;
            }
            y += sw + 18f;

            bool canPour = machine.Hopper.Total < machine.Hopper.Capacity - 0.5f && !machine.IsPouring;
            GUI.enabled = canPour;
            if (GUI.Button(new Rect(c.x, y, c.width, 48f), "ざらめを いれる", s.Button)) Game.PourSugar();
            GUI.enabled = true;
            y += 62f;

            GUI.Label(new Rect(c.x, y, c.width, 22f), "きかいの なか", s.Small);
            GUI.Label(new Rect(c.x, y, c.width, 22f), $"{machine.Hopper.Total:0.0} / {machine.Hopper.Capacity:0} g", s.SmallValue);
            y += 26f;
            DrawHopperBar(new Rect(c.x, y, c.width, 16f));
        }

        void DrawHopperBar(Rect r)
        {
            Bar(r, 1f, UiStyles.Track);
            var hopper = Game.Machine.Hopper;
            float x = r.x;
            // 先に溶けるもの (先頭) を左に
            foreach (var batch in hopper.Batches)
            {
                float w = r.width * batch.Grams / hopper.Capacity;
                if (w < 1f) continue;
                var old = GUI.backgroundColor;
                GUI.backgroundColor = CandyPalette.Evaluate(batch.ColorIndex, Time.time);
                GUI.Box(new Rect(x, r.y, Mathf.Max(w, 16f), r.height), GUIContent.none, s.Fill);
                GUI.backgroundColor = old;
                x += w;
            }
        }

        void DrawSwitchPanel()
        {
            var machine = Game.Machine;
            const float w = 380f;
            Rect c = BeginPanel(new Rect((view.x - w) * 0.5f, view.y - 152f, w, 136f));

            GUI.Label(new Rect(c.x, c.y, c.width, 24f), "② スイッチ", s.Heading);
            GUI.Label(new Rect(c.x, c.y, c.width, 24f), StatusText(), s.Status);

            string label = machine.IsOn ? "スイッチを きる" : "スイッチを いれる";
            if (GUI.Button(new Rect(c.x, c.y + 32f, c.width, 52f), label, machine.IsOn ? s.OffButton : s.OnButton))
                Game.ToggleSwitch();

            GUI.Label(new Rect(c.x, c.y + 88f, 50f, 16f), "ねつ", s.Small);
            Bar(new Rect(c.x + 44f, c.y + 92f, c.width - 44f, 10f), 1f, UiStyles.Track);
            Bar(new Rect(c.x + 44f, c.y + 92f, c.width - 44f, 10f), machine.Heat, Color.Lerp(new Color(1f, 0.8f, 0.4f), new Color(1f, 0.42f, 0.42f), machine.Heat));
        }

        string StatusText()
        {
            var machine = Game.Machine;
            switch (machine.CurrentStatus)
            {
                case CottonCandyMachine.Status.Making: return "わたあめが でてるよ！";
                case CottonCandyMachine.Status.Heating: return "あたためちゅう…";
                case CottonCandyMachine.Status.Empty: return "ざらめが なくなったよ";
                case CottonCandyMachine.Status.Cooling: return "ひえてるところ…";
                default: return machine.Hopper.IsEmpty ? "ざらめを いれてね" : "スイッチを いれよう";
            }
        }

        void DrawStickPanel()
        {
            var candy = Game.Candy;
            Rect c = BeginPanel(new Rect(view.x - PanelWidth - 18f, 86f, PanelWidth, 318f));
            float y = c.y;

            GUI.Label(new Rect(c.x, y, c.width, 26f), "③ 棒で まきとる", s.Heading);
            y += 34f;
            GUI.Label(new Rect(c.x, y, c.width, 40f), $"{candy.Field.TotalGrams:0.0} <size=18>g</size>", s.Big);
            y += 44f;
            GUI.Label(new Rect(c.x, y, c.width, 44f), HintText(), s.Comment);
            y += 54f;

            if (GUI.Button(new Rect(c.x, y, c.width, 42f), Game.Stick.AutoSpin ? "じどう くるくる：ON" : "じどう くるくる：OFF", s.AltButton))
                Game.Stick.AutoSpin = !Game.Stick.AutoSpin;
            y += 52f;

            GUI.enabled = candy.Field.TotalGrams >= 0.3f;
            if (GUI.Button(new Rect(c.x, y, c.width, 48f), "できあがり！", s.Button)) Game.Finish();
            GUI.enabled = true;
            y += 58f;
            if (GUI.Button(new Rect(c.x, y, c.width, 36f), "あたらしい 棒にする", s.AltButton)) Game.NewStick();
        }

        string HintText()
        {
            var machine = Game.Machine;
            float grams = Game.Candy.Field.TotalGrams;
            if (machine.CurrentStatus == CottonCandyMachine.Status.Making)
                return machine.CatchRate < 0.15f ? "棒を ボウルの中へ！ (ホイールで さげる)" : "くるくる まわして まきとろう！";
            if (grams > 0.3f) return "かたちを みたら「できあがり！」";
            return "ドラッグで 棒を うごかせるよ";
        }

        void DrawHelp()
        {
            const float w = 340f;
            if (!showHelp)
            {
                var r = new Rect(18f, view.y - 58f, 150f, 40f);
                if (Event.current.type == EventType.Repaint) panels.Add(r);
                if (GUI.Button(r, "？ あそびかた", s.AltButton)) showHelp = true;
                return;
            }

            string[] lines =
            {
                "左ドラッグ / WASD：棒を うごかす",
                "ホイール / R・F：棒を 上げ下げ",
                "右ドラッグ / Q・E：棒を まわす",
                "右ドラッグ(上下) / Z・C：かたむける",
                "スペース：じどう くるくる",
                "中ドラッグ / Alt+ドラッグ：カメラ",
            };
            float h = Pad * 2f + 28f + lines.Length * 22f;
            Rect c = BeginPanel(new Rect(18f, view.y - h - 18f, w, h));
            GUI.Label(new Rect(c.x, c.y, c.width, 22f), "あそびかた", s.Heading);
            if (GUI.Button(new Rect(c.xMax - 64f, c.y - 2f, 64f, 26f), "とじる", s.SmallButton)) showHelp = false;
            for (int i = 0; i < lines.Length; i++)
                GUI.Label(new Rect(c.x, c.y + 28f + i * 22f, c.width, 22f), lines[i], s.Small);
        }

        void DrawResult()
        {
            var r = Game.Result;
            const float w = 380f;
            Rect c = BeginPanel(new Rect(view.x - w - 24f, 86f, w, 400f));
            float y = c.y;

            GUI.Label(new Rect(c.x, y, c.width, 24f), "できあがり！", s.Heading);
            y += 30f;
            GUI.Label(new Rect(c.x, y, c.width, 40f), r.Name, s.Big);
            y += 44f;
            GUI.Label(new Rect(c.x, y, c.width, 30f), new string('★', r.Stars) + new string('☆', 5 - r.Stars),
                s.Stars);
            y += 40f;

            StatRow(ref y, c, "おもさ", $"{r.Grams:0.0} g");
            StatRow(ref y, c, "おおきさ", $"よこ {r.WidthCm:0} cm × たて {r.HeightCm:0} cm");
            StatRow(ref y, c, "まんまる度", $"{r.Roundness * 100f:0} %");
            y += 8f;
            GUI.Label(new Rect(c.x, y, c.width, 48f), r.Comment, s.Comment);
            y += 56f;

            if (GUI.Button(new Rect(c.x, y, c.width, 48f), "もういちど つくる", s.Button)) Game.NewStick();
            y += 56f;
            if (GUI.Button(new Rect(c.x, y, c.width, 36f), "つづきを まきとる", s.AltButton)) Game.BackToPlay();
        }

        void StatRow(ref float y, Rect c, string label, string value)
        {
            GUI.Label(new Rect(c.x, y, c.width, 24f), label, s.Label);
            GUI.Label(new Rect(c.x, y, c.width, 24f), value, s.Value);
            y += 28f;
        }

        void Bar(Rect r, float fill, Color color)
        {
            if (fill <= 0f) return;
            var old = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUI.Box(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * Mathf.Clamp01(fill)), r.height), GUIContent.none, s.Fill);
            GUI.backgroundColor = old;
        }

        static Rect Expand(Rect r, float amount) => new Rect(r.x - amount, r.y - amount, r.width + amount * 2f, r.height + amount * 2f);
    }
}
