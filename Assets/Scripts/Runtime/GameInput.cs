using System.Collections.Generic;
using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// マウスとキーボードの入力。IMGUI のイベントから集めるので、
    /// Input Manager / Input System どちらの設定でも動く。
    /// </summary>
    public sealed class GameInput
    {
        public Vector2 MoveDrag;    // 左ドラッグ (画面ピクセル、上が +)
        public Vector2 TwistDrag;   // 右ドラッグ
        public Vector2 OrbitDrag;   // 中ドラッグ / Alt + 左ドラッグ
        public float Scroll;        // ホイール (上が +)
        public bool ToggleAutoSpin; // スペースキー

        readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
        int dragButton = -1;
        bool dragOrbit;
        Vector2 lastMouse;

        public bool IsDragging => dragButton >= 0;

        public bool Held(KeyCode key) => held.Contains(key);

        public float Axis(KeyCode negative, KeyCode positive, KeyCode negative2 = KeyCode.None, KeyCode positive2 = KeyCode.None)
        {
            float v = 0f;
            if (Held(negative) || (negative2 != KeyCode.None && Held(negative2))) v -= 1f;
            if (Held(positive) || (positive2 != KeyCode.None && Held(positive2))) v += 1f;
            return v;
        }

        /// <summary>OnGUI から毎イベント呼ぶ。overUi のときはドラッグを始めない。</summary>
        public void Process(Event e, bool overUi)
        {
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (overUi || dragButton >= 0) return;
                    dragButton = e.button;
                    dragOrbit = e.button == 2 || (e.button == 0 && e.alt);
                    lastMouse = e.mousePosition;
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (e.button == dragButton) dragButton = -1;
                    break;
                case EventType.MouseDrag:
                case EventType.MouseMove:
                    if (dragButton < 0) return;
                    // WebGL では e.delta が入らないことがあるので、位置の差から求める
                    Vector2 delta = e.mousePosition - lastMouse;
                    lastMouse = e.mousePosition;
                    delta.y = -delta.y;
                    if (dragOrbit) OrbitDrag += delta;
                    else if (dragButton == 0) MoveDrag += delta;
                    else if (dragButton == 1) TwistDrag += delta;
                    e.Use();
                    break;
                case EventType.ScrollWheel:
                    if (overUi) return;
                    // 1 ノッチの量が環境 (Windows / ブラウザ / トラックパッド) で大きく違うので 1 で頭打ちにする
                    Scroll -= Mathf.Clamp(e.delta.y, -1f, 1f);
                    e.Use();
                    break;
                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.None) return;
                    if (e.keyCode == KeyCode.Space && !held.Contains(KeyCode.Space)) ToggleAutoSpin = true;
                    held.Add(e.keyCode);
                    break;
                case EventType.KeyUp:
                    held.Remove(e.keyCode);
                    break;
            }
        }

        /// <summary>フレームの終わりに 1 フレーム分の入力をリセットする。</summary>
        public void EndFrame()
        {
            MoveDrag = TwistDrag = OrbitDrag = Vector2.zero;
            Scroll = 0f;
            ToggleAutoSpin = false;
        }

        public void ReleaseAll()
        {
            held.Clear();
            dragButton = -1;
            EndFrame();
        }
    }
}
