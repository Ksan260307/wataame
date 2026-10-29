using System.Collections.Generic;
using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// シンプルな UI。画面の形に合わせて 3 つのレイアウトを切り替える。
    ///   Wide     : PC・タブレット横向き (左: ざらめ / 下: スイッチ / 右: 棒 / 棒の左: 高さスライダー)
    ///   Compact  : スマホ横向き (同じ配置を小さく。棒の細かい設定は「くわしく」に畳む)
    ///   Portrait : 縦向き (下にタブつきの操作パネル、右端に高さスライダー)
    /// 画面の何もないところでのマウス / タッチ操作は <see cref="GameInput"/> に渡す。
    /// </summary>
    public sealed class WataameUI : MonoBehaviour
    {
        enum Layout { Wide, Compact, Portrait }
        enum Tab { Sugar, Stick, More }

        const float Pad = 14f;
        const float HeightTop = 0.62f;

        public WataameGame Game;

        UiStyles s;
        Layout layout;
        float scale = 1f;
        Vector2 view;
        Rect safe;
        bool showSettings, stickExpanded, wasOn, portraitHelp;
        Tab tab = Tab.Sugar;
        readonly List<Rect> panels = new List<Rect>();
        Rect pourRect, switchRect, dipRect, sugarTabRect, stickTabRect;
        float lastGrams, popTime = -10f, spinnerAngle;

        bool TouchUi => layout != Layout.Wide || Game.Input.LastDevice == GameInput.Device.Touch;
        GUIStyle SliderThumb => TouchUi ? s.MidThumb : s.Thumb;

        void Start()
        {
            Game.Input.IsOverUi = IsOverUi;
        }

        void Update()
        {
            float grams = Game.Candy.Field.TotalGrams;
            if (grams < lastGrams) lastGrams = grams;
            if (grams - lastGrams >= 0.5f)
            {
                lastGrams = grams;
                popTime = Time.unscaledTime;
            }
            spinnerAngle += Mathf.Clamp(Game.Stick.SpinSpeed, -720f, 720f) * Time.deltaTime;

            // 縦画面: スイッチを入れたら棒のタブへ
            if (Game.Machine.IsOn && !wasOn && tab == Tab.Sugar) tab = Tab.Stick;
            wasOn = Game.Machine.IsOn;
        }

        /// <summary>スクリーン座標 (左下原点) が UI か 3D のスイッチの上か。</summary>
        bool IsOverUi(Vector2 screen)
        {
            var v = new Vector2(screen.x, Screen.height - screen.y) / scale;
            foreach (var r in panels)
                if (r.Contains(v)) return true;
            return Game.CurrentMode == WataameGame.Mode.Playing && Game.Machine.HitSwitch(Game.Rig.Camera.ScreenPointToRay(screen));
        }

        void OnGUI()
        {
            if (Game == null) return;
            if (s == null) s = new UiStyles();
            ComputeLayout();

            Event e = Event.current;
            GUI.matrix = Matrix4x4.identity;
            if (e.isMouse || e.isKey || e.type == EventType.ScrollWheel) HandleWorldInput(e);
            if (e.type == EventType.Repaint) panels.Clear();

            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            pourRect = switchRect = dipRect = sugarTabRect = stickTabRect = Rect.zero;
            bool showcase = Game.CurrentMode == WataameGame.Mode.Showcase;
            if (layout == Layout.Portrait) DrawPortrait(showcase);
            else DrawLandscape(showcase);

            if (!showcase) DrawNextStepHighlight();
            if (showSettings) DrawSettings();
            DrawHoverHint();
        }

        void ComputeLayout()
        {
            float w = Screen.width, h = Screen.height;
            float dpi = Screen.dpi > 0f ? Screen.dpi : 96f;
            bool touch = Application.isMobilePlatform || Game.Input.LastDevice == GameInput.Device.Touch;
            float shortSide = Mathf.Min(w, h);
            if (w < h)
            {
                layout = Layout.Portrait;
                scale = w / 440f;
            }
            else if (touch && (shortSide / dpi < 4.5f || shortSide < 1000f))
            {
                layout = Layout.Compact;
                scale = h / 560f;
            }
            else
            {
                layout = Layout.Wide;
                scale = Mathf.Max(0.4f, Mathf.Min(h / 860f, w / 1100f));
            }
            view = new Vector2(w / scale, h / scale);
            // ノッチなどを避ける
            Rect sa = Screen.safeArea;
            safe = Rect.MinMaxRect(sa.xMin / scale, (h - sa.yMax) / scale, sa.xMax / scale, (h - sa.yMin) / scale);
            if (layout != Layout.Portrait) Game.Rig.BottomInset = 0f;
        }

        void HandleWorldInput(Event e)
        {
            Vector2 virtualMouse = e.mousePosition / scale;
            bool overUi = false;
            foreach (var r in panels)
                if (r.Contains(virtualMouse)) overUi = true;

            // 3D のスイッチを直接クリック / タップしても ON/OFF できる
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

        // ───────────────────────── レイアウト ─────────────────────────

        void DrawLandscape(bool showcase)
        {
            bool compact = layout == Layout.Compact;
            float top = safe.y + (compact ? 8f : 16f);
            DrawTitle(new Rect(safe.x + 20f, top, 420f, compact ? 36f : 56f), compact);
            float panelWidth = compact ? 250f : 300f;
            DrawTopButtons(new Rect(safe.xMax - 18f - 230f, top, 230f, compact ? 34f : 40f));
            float panelTop = top + (compact ? 44f : 70f);

            if (showcase)
            {
                float w = compact ? 340f : 380f;
                DrawResultCard(new Rect(safe.xMax - w - 18f, panelTop, w, 0f), compact);
                return;
            }

            Rect left = DrawSugarPanel(new Rect(safe.x + 18f, panelTop, panelWidth, 0f), compact);
            Rect right = DrawStickPanel(new Rect(safe.xMax - panelWidth - 18f, panelTop, panelWidth, 0f), compact);

            float switchWidth = compact ? 300f : 380f;
            Rect sw = DrawSwitchPanel(new Rect((view.x - switchWidth) * 0.5f, 0f, switchWidth, 0f), compact, safe.yMax - (compact ? 8f : 14f));

            float sliderBottom = safe.yMax - (compact ? 16f : 30f);
            DrawHeightSlider(new Rect(right.x - 74f, panelTop, 62f, Mathf.Min(sliderBottom - panelTop, compact ? 400f : 520f)));

            if (Game.Settings.ShowHelp)
            {
                float helpWidth = compact ? 300f : 340f;
                float helpBottom = compact ? Mathf.Min(safe.yMax - 8f, view.y) : safe.yMax - 18f;
                bool fits = compact ? left.yMax + 150f < helpBottom : true;
                if (fits && (left.x + helpWidth < sw.x || !compact)) DrawHelp(new Rect(safe.x + 18f, 0f, helpWidth, 0f), helpBottom, compact);
            }
        }

        void DrawPortrait(bool showcase)
        {
            float top = safe.y + 10f;
            DrawTitle(new Rect(safe.x + 14f, top, 260f, 34f), true);
            DrawTopButtons(new Rect(safe.xMax - 12f - 170f, top, 170f, 34f));
            float statusY = top + 42f;

            if (showcase)
            {
                float h = 404f;
                Game.Rig.BottomInset = h / view.y;
                DrawResultCard(new Rect(safe.x + 10f, safe.yMax - h - 10f, safe.width - 20f, h), false);
                return;
            }

            // 状態の表示
            var pill = new Rect(safe.x + 14f, statusY, safe.width - 28f, 30f);
            Register(pill);
            GUI.Box(pill, GUIContent.none, s.Panel);
            GUI.Label(new Rect(pill.x + 12f, pill.y, pill.width - 24f, pill.height), StatusText(), s.Label);
            GUI.Label(new Rect(pill.x + 12f, pill.y, pill.width - 24f, pill.height), $"わたあめ {Game.Candy.Field.TotalGrams:0.0} g", s.Value);

            const float sheetHeight = 400f;
            float sheetTop = safe.yMax - sheetHeight;
            Game.Rig.BottomInset = (view.y - sheetTop) / view.y;
            var sheet = new Rect(safe.x, sheetTop, safe.width, view.y - sheetTop + 40f);
            Register(sheet);
            GUI.Box(sheet, GUIContent.none, s.Sheet);

            float x = sheet.x + Pad, w = sheet.width - Pad * 2f, y = sheetTop + Pad;
            // いつも見えるボタン: スイッチ / できあがり
            var machine = Game.Machine;
            switchRect = new Rect(x, y, w * 0.58f - 4f, 52f);
            if (GUI.Button(switchRect, machine.IsOn ? "スイッチを きる" : "スイッチを いれる", machine.IsOn ? s.OffButton : s.OnButton))
                Game.ToggleSwitch();
            GUI.enabled = Game.Candy.Field.TotalGrams >= 0.3f;
            if (GUI.Button(new Rect(x + w * 0.58f + 4f, y, w * 0.42f - 4f, 52f), "できあがり！", s.Button)) Game.Finish();
            GUI.enabled = true;
            y += 60f;
            Bar(new Rect(x, y, w, 8f), 1f, UiStyles.Track);
            Bar(new Rect(x, y, w, 8f), machine.Heat, HeatColor(machine.Heat));
            y += 16f;

            // タブ
            string[] names = { "ざらめ", "棒", "くわしく" };
            float tw = (w - 12f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(x + i * (tw + 6f), y, tw, 40f);
                if (i == 0) sugarTabRect = r;
                if (i == 1) stickTabRect = r;
                if (GUI.Button(r, names[i], (int)tab == i ? s.ActiveButton : s.AltButton)) tab = (Tab)i;
            }
            y += 50f;

            var content = new Rect(x, y, w, sheetTop + sheetHeight - y - Pad);
            switch (tab)
            {
                case Tab.Sugar: SugarSection(content, false, false); break;
                case Tab.Stick: StickCore(content); break;
                case Tab.More: StickDetails(content, true); break;
            }

            DrawHeightSlider(new Rect(safe.xMax - 72f, statusY + 44f, 62f, sheetTop - statusY - 60f));
            if (portraitHelp) DrawHelp(new Rect(safe.x + 14f, statusY + 40f, safe.width - 28f - 80f, 0f), 0f, true, true);
        }

        // ───────────────────────── パネル ─────────────────────────

        void DrawTitle(Rect r, bool small)
        {
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), "わたあめメーカー", small ? s.TitleSmallShadow : s.TitleShadow);
            GUI.Label(r, "わたあめメーカー", small ? s.TitleSmall : s.Title);
        }

        void DrawTopButtons(Rect r)
        {
            Register(r);
            float settingsW = 64f, helpW = 40f;
            float camW = r.width - settingsW - helpW - 12f;
            string presetName = CameraRig.Presets[Game.Rig.PresetIndex].Name;
            if (GUI.Button(new Rect(r.x, r.y, camW, r.height), camW < 100f ? presetName : "してん：" + presetName, s.SmallButton))
                Game.NextCameraPreset();
            if (GUI.Button(new Rect(r.x + camW + 6f, r.y, settingsW, r.height), "せってい", s.SmallButton)) showSettings = !showSettings;
            if (GUI.Button(new Rect(r.xMax - helpW, r.y, helpW, r.height), "？", s.SmallButton))
            {
                // 縦画面ではあそびかたが 3D を隠してしまうので、押したときだけ出す
                if (layout == Layout.Portrait) portraitHelp = !portraitHelp;
                else Game.Settings.ShowHelp = !Game.Settings.ShowHelp;
            }
        }

        Rect DrawSugarPanel(Rect r, bool compact)
        {
            r.height = SugarHeight(compact, true) + Pad * 2f;
            Rect c = BeginPanel(r);
            SugarSection(c, compact, true);
            return r;
        }

        static float SugarHeight(bool compact, bool heading)
        {
            return (heading ? 34f : 0f) + 26f + 36f + 26f + 40f + (compact ? 50f : 58f) + 22f + 16f;
        }

        void SugarSection(Rect c, bool compact, bool heading)
        {
            var machine = Game.Machine;
            float y = c.y;
            if (heading)
            {
                GUI.Label(new Rect(c.x, y, c.width, 26f), "① ざらめを えらぶ", s.Heading);
                y += 34f;
            }

            GUI.Label(new Rect(c.x, y, c.width, 22f), "りょう", s.Label);
            GUI.Label(new Rect(c.x, y, c.width, 22f), $"{Game.SugarAmount:0} g", s.Value);
            y += 26f;
            Game.SugarAmount = Mathf.Round(Slider(new Rect(c.x, y, c.width, 30f), Game.SugarAmount, GameConfig.SugarMin, GameConfig.SugarMax));
            y += 36f;

            GUI.Label(new Rect(c.x, y, c.width, 22f), "いろ", s.Label);
            GUI.Label(new Rect(c.x, y, c.width, 22f), CandyPalette.Entries[Game.ColorIndex].Name, s.Value);
            y += 26f;
            const float gap = 6f;
            float sw = Mathf.Min(36f, (c.width - gap * (CandyPalette.Count - 1)) / CandyPalette.Count);
            for (int i = 0; i < CandyPalette.Count; i++)
            {
                var r = new Rect(c.x + i * (sw + gap), y, sw, sw);
                var old = GUI.color;
                GUI.color = CandyPalette.Evaluate(i, Time.time);
                GUI.DrawTexture(r, s.Circle);
                GUI.color = i == Game.ColorIndex ? UiStyles.Pink : new Color(0.85f, 0.78f, 0.82f);
                GUI.DrawTexture(i == Game.ColorIndex ? Expand(r, 3f) : r, s.Ring);
                GUI.color = old;
                if (GUI.Button(Expand(r, 2f), GUIContent.none, GUIStyle.none)) Game.ColorIndex = i;
            }
            y += 40f;

            bool canPour = machine.Hopper.Total < machine.Hopper.Capacity - 0.5f && !machine.IsPouring;
            GUI.enabled = canPour;
            pourRect = new Rect(c.x, y, c.width, compact ? 42f : 48f);
            if (GUI.Button(pourRect, "ざらめを いれる", s.Button)) Game.PourSugar();
            GUI.enabled = true;
            y += compact ? 50f : 58f;

            GUI.Label(new Rect(c.x, y, c.width, 20f), "きかいの なか", s.Small);
            GUI.Label(new Rect(c.x, y, c.width, 20f), $"{machine.Hopper.Total:0.0} / {machine.Hopper.Capacity:0} g", s.SmallValue);
            y += 22f;
            DrawHopperBar(new Rect(c.x, y, c.width, 14f));
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
                GUI.Box(new Rect(x, r.y, Mathf.Max(w, r.height), r.height), GUIContent.none, s.Fill);
                GUI.backgroundColor = old;
                x += w;
            }
        }

        Rect DrawSwitchPanel(Rect r, bool compact, float bottom)
        {
            var machine = Game.Machine;
            r.height = (compact ? 92f : 104f) + Pad * 2f;
            r.y = bottom - r.height;
            Rect c = BeginPanel(r);

            GUI.Label(new Rect(c.x, c.y, c.width, 24f), compact ? "② スイッチ" : "② スイッチ", s.Heading);
            GUI.Label(new Rect(c.x, c.y, c.width, 24f), StatusText(), s.Status);

            switchRect = new Rect(c.x, c.y + 30f, c.width, compact ? 44f : 52f);
            if (GUI.Button(switchRect, machine.IsOn ? "スイッチを きる" : "スイッチを いれる", machine.IsOn ? s.OffButton : s.OnButton))
                Game.ToggleSwitch();

            float barY = switchRect.yMax + 8f;
            GUI.Label(new Rect(c.x, barY - 4f, 50f, 18f), "ねつ", s.Small);
            Bar(new Rect(c.x + 44f, barY, c.width - 44f, 10f), 1f, UiStyles.Track);
            Bar(new Rect(c.x + 44f, barY, c.width - 44f, 10f), machine.Heat, HeatColor(machine.Heat));
            return r;
        }

        static Color HeatColor(float heat) => Color.Lerp(new Color(1f, 0.8f, 0.4f), new Color(1f, 0.42f, 0.42f), heat);

        string StatusText()
        {
            var machine = Game.Machine;
            switch (machine.CurrentStatus)
            {
                case CottonCandyMachine.Status.Making: return $"でてるよ！ のこり {machine.Hopper.Total:0.0}g";
                case CottonCandyMachine.Status.Heating: return $"あたためちゅう… {machine.Heat * 200f:0}%";
                case CottonCandyMachine.Status.Empty: return "ざらめが なくなったよ";
                case CottonCandyMachine.Status.Cooling: return "ひえてるところ…";
                default: return machine.Hopper.IsEmpty ? "ざらめを いれてね" : "スイッチを いれよう";
            }
        }

        Rect DrawStickPanel(Rect r, bool compact)
        {
            // スマホ横向きでは「くわしく」でページを切り替える (縦に入りきらないので)
            bool showCore = !compact || !stickExpanded;
            bool showDetails = !compact || stickExpanded;
            r.height = 34f + (showCore ? StickCoreHeight : 0f) + (showDetails ? StickDetailsHeight : 0f) + (compact ? 38f : 0f) + Pad * 2f;
            Rect c = BeginPanel(r);
            GUI.Label(new Rect(c.x, c.y, c.width, 26f), "③ 棒で まきとる", s.Heading);
            DrawSpinner(new Rect(c.xMax - 24f, c.y + 1f, 24f, 24f));

            float y = c.y + 34f;
            if (showCore)
            {
                StickCore(new Rect(c.x, y, c.width, StickCoreHeight), true);
                y += StickCoreHeight;
            }
            if (compact)
            {
                if (GUI.Button(new Rect(c.x, y, c.width, 30f), stickExpanded ? "もどる ▲" : "くわしく ▼", s.SmallButton)) stickExpanded = !stickExpanded;
                y += 38f;
            }
            if (showDetails) StickDetails(new Rect(c.x, y, c.width, StickDetailsHeight), false);
            return r;
        }

        const float StickCoreHeight = 56f + 46f + 52f + 48f + 54f + 40f;
        const float StickDetailsHeight = 56f + 56f + 44f;

        /// <summary>棒の基本操作: 重さ・キャッチ率・ヒント・ボウル/おまかせ/くるくる・できあがり。</summary>
        void StickCore(Rect c, bool withFinish = false)
        {
            var stick = Game.Stick;
            var machine = Game.Machine;
            float y = c.y;

            // 重さ (増えたときにポンとはねる)
            float pop = 1f + 0.22f * Mathf.Exp(-(Time.unscaledTime - popTime) * 7f);
            var gramsRect = new Rect(c.x, y, c.width * 0.55f, 36f);
            Matrix4x4 m = GUI.matrix;
            // ピボットは画面座標で渡す (GUI.matrix の拡大を含める)
            GUIUtility.ScaleAroundPivot(Vector2.one * pop, m.MultiplyPoint3x4(new Vector2(gramsRect.x + 10f, gramsRect.center.y)));
            GUI.Label(gramsRect, $"{Game.Candy.Field.TotalGrams:0.0} <size=17>g</size>", s.Big);
            GUI.matrix = m;

            // キャッチ率
            bool making = machine.CurrentStatus == CottonCandyMachine.Status.Making;
            GUI.Label(new Rect(c.x + c.width * 0.55f, y, c.width * 0.45f, 18f), "まきとり", s.SmallValue);
            var meter = new Rect(c.x + c.width * 0.55f, y + 22f, c.width * 0.45f, 10f);
            Bar(meter, 1f, UiStyles.Track);
            Bar(meter, making ? machine.CatchRate : 0f, Color.Lerp(new Color(1f, 0.7f, 0.4f), UiStyles.Mint, machine.CatchRate * 1.6f));
            y += 46f;

            GUI.Label(new Rect(c.x, y - 6f, c.width, 44f), HintText(), s.Comment);
            y += 46f;

            float half = (c.width - 8f) * 0.5f;
            bool outside = stick.CurrentZone == StickController.Zone.Outside;
            dipRect = new Rect(c.x, y, half, 44f);
            if (GUI.Button(dipRect, outside ? "ボウルに いれる" : "もちあげる", s.AltButton))
            {
                if (outside) stick.DipIntoBowl();
                else stick.LiftOut();
            }
            if (GUI.Button(new Rect(c.x + half + 8f, y, half, 44f), stick.Orbit ? "おまかせ：ON" : "おまかせ", stick.Orbit ? s.ActiveButton : s.AltButton))
            {
                stick.Orbit = !stick.Orbit;
                if (stick.Orbit && outside) stick.DipIntoBowl();
            }
            y += 52f;

            if (GUI.Button(new Rect(c.x, y, half, 40f), stick.AutoSpin ? "くるくる：ON" : "くるくる：OFF", stick.AutoSpin ? s.ActiveButton : s.AltButton))
                stick.AutoSpin = !stick.AutoSpin;
            if (GUI.Button(new Rect(c.x + half + 8f, y, half, 40f), Game.Settings.Clockwise ? "みぎまわり" : "ひだりまわり", s.AltButton))
                Game.Settings.Clockwise = !Game.Settings.Clockwise;
            y += 48f;

            if (withFinish)
            {
                GUI.enabled = Game.Candy.Field.TotalGrams >= 0.3f;
                if (GUI.Button(new Rect(c.x, y, c.width, 46f), "できあがり！", s.Button)) Game.Finish();
                GUI.enabled = true;
            }
            y += 54f;
            GUI.enabled = Game.Candy.Field.TotalGrams > 0.05f;
            if (GUI.Button(new Rect(c.x, withFinish ? y : y - 54f, c.width, 34f), "あたらしい 棒にする", s.SmallButton)) Game.NewStick();
            GUI.enabled = true;
        }

        /// <summary>棒のくわしい設定: 回転の速さ・おまかせの円・傾き (縦画面ではズームも)。</summary>
        void StickDetails(Rect c, bool withZoom)
        {
            var stick = Game.Stick;
            var settings = Game.Settings;
            float y = c.y;

            GUI.Label(new Rect(c.x, y, c.width, 20f), "くるくるの はやさ", s.Small);
            GUI.Label(new Rect(c.x, y, c.width, 20f), settings.SpinSpeed < 180f ? "ゆっくり" : settings.SpinSpeed < 360f ? "ふつう" : "はやい", s.SmallValue);
            settings.SpinSpeed = Slider(new Rect(c.x, y + 20f, c.width, 30f), settings.SpinSpeed, 60f, 540f);
            y += 56f;

            GUI.Label(new Rect(c.x, y, c.width, 20f), "おまかせの わ", s.Small);
            GUI.Label(new Rect(c.x, y, c.width, 20f), $"{settings.OrbitRadius * 100f:0} cm", s.SmallValue);
            settings.OrbitRadius = Slider(new Rect(c.x, y + 20f, c.width, 30f), settings.OrbitRadius, 0.12f, 0.24f);
            y += 56f;

            Vector2 tilt = stick.Tilt;
            GUI.Label(new Rect(c.x, y, 110f, 36f), $"かたむき {tilt.magnitude:0}°", s.Small);
            float bw = (c.width - 110f - 12f) / 3f;
            if (GUI.RepeatButton(new Rect(c.x + 110f, y, bw, 36f), "◀", s.SmallButton)) stick.TiltBy(new Vector2(0f, -40f * Time.deltaTime));
            if (GUI.Button(new Rect(c.x + 116f + bw, y, bw, 36f), "まっすぐ", s.SmallButton)) stick.ResetTilt();
            if (GUI.RepeatButton(new Rect(c.x + 122f + bw * 2f, y, bw, 36f), "▶", s.SmallButton)) stick.TiltBy(new Vector2(0f, 40f * Time.deltaTime));
            y += 44f;

            if (withZoom)
            {
                GUI.Label(new Rect(c.x, y, 110f, 36f), "カメラ", s.Small);
                if (GUI.RepeatButton(new Rect(c.x + 110f, y, bw, 36f), "ちかく", s.SmallButton)) Game.Input.Zoom += Time.deltaTime * 1.2f;
                if (GUI.Button(new Rect(c.x + 116f + bw, y, bw, 36f), "もどす", s.SmallButton)) Game.Rig.ResetView();
                if (GUI.RepeatButton(new Rect(c.x + 122f + bw * 2f, y, bw, 36f), "とおく", s.SmallButton)) Game.Input.Zoom -= Time.deltaTime * 1.2f;
            }
        }

        string HintText()
        {
            var machine = Game.Machine;
            var stick = Game.Stick;
            float grams = Game.Candy.Field.TotalGrams;
            bool touch = Game.Input.LastDevice == GameInput.Device.Touch;
            if (stick.LastContact.BlockedAtRim) return "ボウルの 上へ うごかしてから さげてね";

            var status = machine.CurrentStatus;
            if (status == CottonCandyMachine.Status.Making)
            {
                switch (stick.CurrentZone)
                {
                    case StickController.Zone.Outside: return "棒を ボウルの 中へ！「ボウルに いれる」";
                    case StickController.Zone.Shallow: return "もっと ふかく さげよう";
                    case StickController.Zone.TooClose: return "ヘッドに ちかすぎ！ すこし はなして";
                    case StickController.Zone.TooFar: return "すこし ヘッドに ちかづけて";
                }
                if (!stick.AutoSpin && Mathf.Abs(stick.SpinSpeed) < 30f) return "くるくる まわすと まるくなるよ";
                return stick.CurrentZone == StickController.Zone.Best ? "いいかんじ！ そのまま まきとろう" : "みどりの わの 上が ベストだよ";
            }
            if (status == CottonCandyMachine.Status.Heating) return "あたためちゅう。棒を ボウルへ いれて まとう";
            if (grams > 0.3f) return "かたちを みたら「できあがり！」";
            return touch ? "ゆびで 棒を うごかせるよ" : "ドラッグで 棒を うごかせるよ";
        }

        void DrawSpinner(Rect r)
        {
            if (!Game.Stick.AutoSpin && Mathf.Abs(Game.Stick.SpinSpeed) < 5f) return;
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(spinnerAngle, m.MultiplyPoint3x4(r.center));
            var old = GUI.color;
            GUI.color = UiStyles.Pink;
            GUI.DrawTexture(r, s.Spinner);
            GUI.color = old;
            GUI.matrix = m;
        }

        /// <summary>棒の高さの縦スライダー。ボウルの縁とベストな深さの目盛りつき。</summary>
        void DrawHeightSlider(Rect r)
        {
            if (r.height < 140f) return;
            Rect c = BeginPanel(r);
            GUI.Label(new Rect(c.x - 10f, c.y - 6f, c.width + 20f, 20f), "たかさ", s.Center);

            var track = new Rect(c.x, c.y + 20f, c.width, c.height - 20f);
            float bottom = StickController.MinY;
            float ToY(float v) => track.y + (HeightTop - v) / (HeightTop - bottom) * (track.height - 44f) + 22f;

            var trackBar = new Rect(track.center.x - 6f, track.y + 16f, 12f, track.height - 32f);
            var old = GUI.backgroundColor;
            GUI.backgroundColor = UiStyles.Track;
            GUI.Box(trackBar, GUIContent.none, s.Fill);
            // ベストな深さの帯
            float bestTop = ToY(StickController.ShallowTipY), bestBottom = ToY(StickController.MinY + 0.01f);
            GUI.backgroundColor = new Color(0.45f, 0.85f, 0.6f, 0.8f);
            GUI.Box(new Rect(trackBar.x - 3f, bestTop, trackBar.width + 6f, bestBottom - bestTop), GUIContent.none, s.Fill);
            GUI.backgroundColor = old;
            // 縁の目盛り
            float rimY = ToY(GameConfig.BowlRimY);
            GUI.Box(new Rect(track.x - 2f, rimY - 1.5f, track.width + 4f, 3f), GUIContent.none, s.Fill);
            GUI.Label(new Rect(track.x - 30f, rimY - 22f, 40f, 18f), "ふち", s.Small);

            float value = Mathf.Clamp(Game.Stick.TargetHeight, bottom, HeightTop);
            var sliderRect = new Rect(track.center.x - 22f, track.y, 44f, track.height);
            GUI.changed = false;
            float next = GUI.VerticalSlider(sliderRect, value, HeightTop, bottom, s.Slider, s.BigThumb);
            if (GUI.changed) Game.Stick.TargetHeight = next;
        }

        void DrawHelp(Rect r, float bottom, bool compact, bool fromTop = false)
        {
            bool touch = Game.Input.LastDevice == GameInput.Device.Touch || Application.isMobilePlatform;
            string[] lines = touch
                ? new[]
                {
                    "1本ゆびで ドラッグ：棒を うごかす",
                    "タップ：そこへ 棒を いどう",
                    "右の「たかさ」：棒を 上げ下げ",
                    "2本ゆびで ひねる：棒を まわす",
                    "2本ゆびで ドラッグ：カメラ ／ ピンチ：ズーム",
                    "ダブルタップ：くるくる ON/OFF",
                }
                : new[]
                {
                    "ドラッグ：棒を うごかす (どこでも OK)",
                    "ホイール / R・F：上げ下げ (Shift で ゆっくり)",
                    "右ドラッグ / Q・E：まわす (上下で かたむき)",
                    "B：ボウルに いれる　T：おまかせ　X：まっすぐ",
                    "スペース：くるくる　V：してん　Enter：できあがり",
                    "中ドラッグ / Alt+ドラッグ：カメラ　Ctrl+ホイール：ズーム",
                };
            float lineH = compact ? 20f : 22f;
            r.height = Pad * 2f + 28f + lines.Length * lineH;
            r.y = fromTop ? r.y : bottom - r.height;
            Rect c = BeginPanel(r);
            GUI.Label(new Rect(c.x, c.y, c.width, 22f), "あそびかた", s.Heading);
            if (GUI.Button(new Rect(c.xMax - 64f, c.y - 2f, 64f, 26f), "とじる", s.SmallButton))
            {
                if (layout == Layout.Portrait) portraitHelp = false;
                else Game.Settings.ShowHelp = false;
            }
            for (int i = 0; i < lines.Length; i++)
                GUI.Label(new Rect(c.x, c.y + 28f + i * lineH, c.width, lineH), lines[i], s.Small);
        }

        void DrawSettings()
        {
            var settings = Game.Settings;
            float w = Mathf.Min(360f, view.x - 40f);
            var r = new Rect((view.x - w) * 0.5f, Mathf.Max(safe.y + 60f, view.y * 0.5f - 170f), w, 300f);
            Rect c = BeginPanel(r);
            float y = c.y;
            GUI.Label(new Rect(c.x, y, c.width, 26f), "せってい", s.Heading);
            if (GUI.Button(new Rect(c.xMax - 64f, y - 2f, 64f, 28f), "とじる", s.SmallButton)) showSettings = false;
            y += 40f;

            GUI.Label(new Rect(c.x, y, c.width, 20f), "そうさの かんど", s.Small);
            GUI.Label(new Rect(c.x, y, c.width, 20f), $"{settings.Sensitivity:0.0}", s.SmallValue);
            settings.Sensitivity = Slider(new Rect(c.x, y + 22f, c.width, 30f), settings.Sensitivity, 0.4f, 2f);
            y += 64f;

            if (GUI.Button(new Rect(c.x, y, c.width, 40f), settings.Magnet ? "すいよせ アシスト：ON" : "すいよせ アシスト：OFF", settings.Magnet ? s.ActiveButton : s.AltButton))
                settings.Magnet = !settings.Magnet;
            y += 48f;
            if (GUI.Button(new Rect(c.x, y, c.width, 40f), settings.ShowHelp ? "あそびかた：ひょうじ" : "あそびかた：かくす", s.AltButton))
                settings.ShowHelp = !settings.ShowHelp;
            y += 48f;
            if (GUI.Button(new Rect(c.x, y, c.width, 40f), "カメラを もどす", s.AltButton)) Game.Rig.ResetView();
        }

        void DrawResultCard(Rect r, bool compact)
        {
            var result = Game.Result;
            r.height = compact ? 372f : 404f;
            Rect c = BeginPanel(r);
            float y = c.y;
            float row = compact ? 24f : 28f;

            GUI.Label(new Rect(c.x, y, c.width, 24f), "できあがり！", s.Heading);
            y += 28f;
            GUI.Label(new Rect(c.x, y, c.width, 38f), result.Name, s.Big);
            y += 40f;
            GUI.Label(new Rect(c.x, y, c.width, 30f), new string('★', result.Stars) + new string('☆', 5 - result.Stars), s.Stars);
            y += 34f;

            StatRow(ref y, row, c, "おもさ", $"{result.Grams:0.0} g");
            StatRow(ref y, row, c, "おおきさ", $"よこ {result.WidthCm:0} cm × たて {result.HeightCm:0} cm");
            StatRow(ref y, row, c, "まんまる度", $"{result.Roundness * 100f:0} %");
            GUI.Label(new Rect(c.x, y, c.width, 24f), result.Comment, s.Comment);
            y += 26f;
            GUI.Label(new Rect(c.x, y, c.width, 40f), "コツ：" + result.Advice, s.Small);
            y += compact ? 38f : 46f;

            float half = (c.width - 8f) * 0.5f;
            if (GUI.Button(new Rect(c.x, y, half, 46f), "もういちど", s.Button)) Game.NewStick();
            if (GUI.Button(new Rect(c.x + half + 8f, y, half, 46f), "つづける", s.AltButton)) Game.BackToPlay();
        }

        void StatRow(ref float y, float row, Rect c, string label, string value)
        {
            GUI.Label(new Rect(c.x, y, c.width, row), label, s.Label);
            GUI.Label(new Rect(c.x, y, c.width, row), value, s.Value);
            y += row;
        }

        /// <summary>今やること (ざらめ → スイッチ → ボウルへ) のボタンをふち取りで点滅させる。</summary>
        void DrawNextStepHighlight()
        {
            var machine = Game.Machine;
            float grams = Game.Candy.Field.TotalGrams;
            Rect target = Rect.zero;
            if (machine.Hopper.IsEmpty && !machine.IsOn && grams < 0.1f)
                target = layout == Layout.Portrait && tab != Tab.Sugar ? sugarTabRect : pourRect;
            else if (!machine.IsOn && !machine.Hopper.IsEmpty && grams < 0.3f)
                target = switchRect;
            else if (machine.IsOn && !machine.Hopper.IsEmpty && (Game.Stick.CurrentZone == StickController.Zone.Outside || Game.Stick.CurrentZone == StickController.Zone.Shallow))
                target = layout == Layout.Portrait && tab != Tab.Stick ? stickTabRect : dipRect;
            if (target.width <= 0f) return;

            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.45f, 0.62f, 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f));
            GUI.Box(Expand(target, 5f), GUIContent.none, s.Outline);
            GUI.backgroundColor = old;
        }

        void DrawHoverHint()
        {
            if (!Game.Stick.Hovering || Game.CurrentMode != WataameGame.Mode.Playing) return;
            Vector2 p = Game.Input.HoverScreen;
            var gui = new Vector2(p.x, Screen.height - p.y) / scale;
            GUI.Label(new Rect(gui.x + 18f, gui.y + 14f, 150f, 26f), "ドラッグで うごかす", s.Hint);
        }

        // ───────────────────────── 部品 ─────────────────────────

        Rect BeginPanel(Rect r)
        {
            Register(r);
            GUI.Box(r, GUIContent.none, s.Panel);
            return new Rect(r.x + Pad, r.y + Pad, r.width - Pad * 2f, r.height - Pad * 2f);
        }

        void Register(Rect r)
        {
            if (Event.current.type == EventType.Repaint) panels.Add(r);
        }

        float Slider(Rect r, float value, float min, float max)
        {
            GUIStyle thumb = SliderThumb;
            float trackY = r.y + thumb.fixedHeight * 0.5f - 6f;
            Bar(new Rect(r.x, trackY, r.width, 12f), 1f, UiStyles.Track);
            Bar(new Rect(r.x, trackY, r.width, 12f), Mathf.InverseLerp(min, max, value), new Color(1f, 0.75f, 0.83f));
            return GUI.HorizontalSlider(new Rect(r.x, r.y, r.width, thumb.fixedHeight), value, min, max, s.Slider, thumb);
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
