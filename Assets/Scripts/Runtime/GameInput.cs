using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// マウス・キーボード・タッチの入力をまとめる。
    /// マウスとキーは IMGUI のイベントから (Input Manager / Input System どちらでも動く)、
    /// タッチは毎フレーム Input.touches から読む。座標はすべてスクリーン座標 (左下原点、ピクセル)。
    /// </summary>
    public sealed class GameInput
    {
        public enum Device { Mouse, Touch }

        const float TapMaxSeconds = 0.35f;
        const float DoubleTapSeconds = 0.4f;
        const float TouchGraceSeconds = 0.5f;

        // 棒をつかむポインタ (左ボタン / 指 1 本)
        public bool PointerHeld { get; private set; }
        public bool PointerDown { get; private set; }
        public bool PointerUp { get; private set; }
        public Vector2 PointerScreen { get; private set; }
        public Vector2 PointerDelta { get; private set; }
        public bool Tapped { get; private set; }
        public bool DoubleTapped { get; private set; }
        public Vector2 TapScreen { get; private set; }

        /// <summary>マウスカーソルの位置 (タッチ中は無効)。</summary>
        public bool HasHover { get; private set; }
        public Vector2 HoverScreen { get; private set; }

        public Vector2 TwistDrag;   // 右ドラッグ (px)
        public float TwistDegrees;  // 2 本指のひねり (度)
        public Vector2 OrbitDrag;   // 中ドラッグ / Alt + 左ドラッグ / 2 本指ドラッグ (px)
        public float Zoom;          // + で寄る (ピンチ / Ctrl + ホイール)
        public float Scroll;        // ホイールのノッチ (上が +)
        public bool ToggleAutoSpin; // スペース / ダブルタップ

        public Device LastDevice { get; private set; } = Device.Mouse;
        public bool TouchActive => Time.unscaledTime - lastTouchTime < TouchGraceSeconds;
        public bool Precise => Held(KeyCode.LeftShift) || Held(KeyCode.RightShift);

        /// <summary>UI の上かどうか (スクリーン座標)。UI 側が設定する。</summary>
        public Func<Vector2, bool> IsOverUi = _ => false;

        readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
        readonly HashSet<KeyCode> pressed = new HashSet<KeyCode>();
        int mouseButton = -1;
        bool mouseOrbit;
        Vector2 lastMouse;
        float pointerStartTime, lastTapTime = -10f, lastTouchTime = -10f, lastScrollTime = -10f;
        Vector2 pointerStartScreen, lastTapScreen;
        bool pointerMoved, pointerCanceled;

        // 2 本指ジェスチャ
        bool multiTouch, multiIgnored;
        Vector2 prevMid;
        float prevDistance, prevAngle;

        public bool Held(KeyCode key) => held.Contains(key);
        public bool Pressed(KeyCode key) => pressed.Contains(key);

        public float Axis(KeyCode negative, KeyCode positive, KeyCode negative2 = KeyCode.None, KeyCode positive2 = KeyCode.None)
        {
            float v = 0f;
            if (Held(negative) || (negative2 != KeyCode.None && Held(negative2))) v -= 1f;
            if (Held(positive) || (positive2 != KeyCode.None && Held(positive2))) v += 1f;
            return v;
        }

        /// <summary>タップ判定に使う移動量のしきい値 (画面の密度に合わせる)。</summary>
        static float TapSlop => 10f * Mathf.Max(1f, (Screen.dpi > 0f ? Screen.dpi : 96f) / 96f);

        /// <summary>OnGUI から毎イベント呼ぶ (マウスとキーボード)。</summary>
        public void Process(Event e, bool overUi)
        {
            if (e.isKey)
            {
                ProcessKey(e);
                return;
            }
            // タッチ由来の疑似マウスイベントは二重入力になるので無視する
            if (TouchActive) return;

            Vector2 screen = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (overUi || mouseButton >= 0) return;
                    LastDevice = Device.Mouse;
                    mouseButton = e.button;
                    mouseOrbit = e.button == 2 || (e.button == 0 && e.alt);
                    lastMouse = screen;
                    if (e.button == 0 && !mouseOrbit) BeginPointer(screen);
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (mouseButton < 0) return;
                    Vector2 delta = screen - lastMouse; // WebGL では e.delta が入らないことがあるので位置の差から求める
                    lastMouse = screen;
                    if (mouseOrbit) OrbitDrag += delta;
                    else if (mouseButton == 0) MovePointer(screen);
                    else if (mouseButton == 1) TwistDrag += delta;
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (e.button != mouseButton) return;
                    if (mouseButton == 0 && !mouseOrbit) EndPointer();
                    mouseButton = -1;
                    break;
                case EventType.ScrollWheel:
                    if (overUi) return;
                    // 1 ノッチの量は環境 (Windows / ブラウザ / トラックパッド) で大きく違うので 1 で頭打ち
                    float notch = -Mathf.Clamp(e.delta.y, -1f, 1f);
                    if (e.control || e.command)
                    {
                        Zoom += notch * 0.12f;
                    }
                    else
                    {
                        // 続けて回すと少し速くなる
                        float accel = Time.unscaledTime - lastScrollTime < 0.08f ? 1.6f : 1f;
                        lastScrollTime = Time.unscaledTime;
                        Scroll += notch * accel;
                    }
                    e.Use();
                    break;
            }
        }

        void ProcessKey(Event e)
        {
            if (e.keyCode == KeyCode.None) return;
            if (e.type == EventType.KeyDown)
            {
                if (!held.Contains(e.keyCode))
                {
                    pressed.Add(e.keyCode);
                    if (e.keyCode == KeyCode.Space) ToggleAutoSpin = true;
                }
                held.Add(e.keyCode);
            }
            else if (e.type == EventType.KeyUp)
            {
                held.Remove(e.keyCode);
            }
        }

        /// <summary>Update から毎フレーム呼ぶ。タッチとマウスカーソルを読む。</summary>
        public void Poll()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            PollTouches();
            if (!TouchActive && Input.mousePresent)
            {
                HasHover = true;
                HoverScreen = Input.mousePosition;
                // ウィンドウの外でボタンを離した場合など、MouseUp を取りこぼしたら離したことにする
                if (mouseButton == 0 && !Input.GetMouseButton(0))
                {
                    if (!mouseOrbit) EndPointer();
                    mouseButton = -1;
                }
            }
            else
            {
                HasHover = false;
            }
#endif
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        void PollTouches()
        {
            int count = Input.touchCount;
            if (count == 0)
            {
                multiTouch = false;
                return;
            }

            lastTouchTime = Time.unscaledTime;
            LastDevice = Device.Touch;
            HasHover = false;

            if (count == 1 && !multiTouch)
            {
                Touch t = Input.GetTouch(0);
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        if (!IsOverUi(t.position)) BeginPointer(t.position);
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (PointerHeld) MovePointer(t.position);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (PointerHeld) EndPointer();
                        break;
                }
                return;
            }

            // 2 本指: ドラッグでカメラ、ピンチでズーム、ひねりで棒を回す
            Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
            Vector2 mid = (a.position + b.position) * 0.5f;
            Vector2 diff = b.position - a.position;
            float distance = Mathf.Max(1f, diff.magnitude);
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            if (!multiTouch || a.phase == TouchPhase.Began || b.phase == TouchPhase.Began)
            {
                if (!multiTouch)
                {
                    multiIgnored = IsOverUi(mid);
                    if (PointerHeld)
                    {
                        // 1 本指の操作を取り消す
                        pointerCanceled = true;
                        EndPointer();
                    }
                }
                multiTouch = true;
            }
            else if (!multiIgnored)
            {
                OrbitDrag += (mid - prevMid) * 0.6f;
                Zoom += Mathf.Log(distance / prevDistance) * 1.2f;
                TwistDegrees += Mathf.DeltaAngle(prevAngle, angle);
            }
            prevMid = mid;
            prevDistance = distance;
            prevAngle = angle;
        }
#endif

        void BeginPointer(Vector2 screen)
        {
            PointerHeld = true;
            PointerDown = true;
            PointerScreen = screen;
            PointerDelta = Vector2.zero;
            pointerStartScreen = screen;
            pointerStartTime = Time.unscaledTime;
            pointerMoved = false;
            pointerCanceled = false;
        }

        void MovePointer(Vector2 screen)
        {
            PointerDelta += screen - PointerScreen;
            PointerScreen = screen;
            if ((screen - pointerStartScreen).magnitude > TapSlop) pointerMoved = true;
        }

        void EndPointer()
        {
            PointerHeld = false;
            PointerUp = true;
            if (pointerCanceled || pointerMoved || Time.unscaledTime - pointerStartTime > TapMaxSeconds) return;

            Tapped = true;
            TapScreen = PointerScreen;
            if (Time.unscaledTime - lastTapTime < DoubleTapSeconds && (PointerScreen - lastTapScreen).magnitude < TapSlop * 4f)
            {
                DoubleTapped = true;
                ToggleAutoSpin = true;
                lastTapTime = -10f;
            }
            else
            {
                lastTapTime = Time.unscaledTime;
                lastTapScreen = PointerScreen;
            }
        }

        /// <summary>フレームの終わりに 1 フレーム分の入力をリセットする。</summary>
        public void EndFrame()
        {
            PointerDown = PointerUp = Tapped = DoubleTapped = false;
            PointerDelta = TwistDrag = OrbitDrag = Vector2.zero;
            TwistDegrees = Zoom = Scroll = 0f;
            ToggleAutoSpin = false;
            pressed.Clear();
        }

        public void ReleaseAll()
        {
            held.Clear();
            if (PointerHeld)
            {
                pointerCanceled = true;
                EndPointer();
            }
            mouseButton = -1;
            multiTouch = false;
            EndFrame();
        }
    }
}
