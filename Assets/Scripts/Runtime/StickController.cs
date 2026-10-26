using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// 棒の操作。左ドラッグ/WASD で水平移動、ホイール/R・F で上下、
    /// 右ドラッグ/Q・E で回転 (上下ドラッグで傾き)、スペースで自動回転。
    /// </summary>
    public sealed class StickController : MonoBehaviour
    {
        const float DragSpeed = 0.0011f;     // m / px
        const float KeyMoveSpeed = 0.35f;    // m / s
        const float ScrollSpeed = 0.012f;    // m / ノッチ
        const float KeyLiftSpeed = 0.3f;     // m / s
        const float TwistSpeed = 0.7f;       // 度 / px
        const float KeySpinSpeed = 220f;     // 度 / s
        const float AutoSpinSpeed = 260f;    // 度 / s
        const float MaxTilt = 35f;
        const float MinY = GameConfig.BowlBottomY + 0.035f;
        const float MaxY = 0.75f;
        const float MaxRadius = 0.65f;

        public static readonly Vector3 HomePosition = new Vector3(0.18f, 0.44f, -0.04f);
        static readonly Vector3 ShowcasePosition = new Vector3(0f, 0.55f, -0.12f);

        public bool AutoSpin { get; set; }
        public bool Showcase { get; set; }
        public float SpinSpeed { get; private set; }

        Vector3 tip = HomePosition;
        float spin;
        Vector2 tilt;
        Vector3 smoothedTip = HomePosition;

        public void Drive(GameInput input, Transform cameraTransform, float dt)
        {
            if (input.ToggleAutoSpin && !Showcase) AutoSpin = !AutoSpin;

            float spinDelta;
            if (Showcase)
            {
                // できあがり表示: 目の前に持ち上げてゆっくり回す
                tip = Vector3.Lerp(tip, ShowcasePosition, 1f - Mathf.Exp(-dt * 4f));
                tilt = Vector2.Lerp(tilt, Vector2.zero, 1f - Mathf.Exp(-dt * 4f));
                spinDelta = 40f * dt;
            }
            else
            {
                Vector3 right = cameraTransform.right;
                right.y = 0f;
                right.Normalize();
                Vector3 forward = Vector3.Cross(right, Vector3.up);

                Vector2 move = input.MoveDrag * DragSpeed;
                move += new Vector2(
                    input.Axis(KeyCode.A, KeyCode.D, KeyCode.LeftArrow, KeyCode.RightArrow),
                    input.Axis(KeyCode.S, KeyCode.W, KeyCode.DownArrow, KeyCode.UpArrow)) * (KeyMoveSpeed * dt);
                Vector3 previous = tip;
                tip += right * move.x + forward * move.y;
                tip.y += input.Scroll * ScrollSpeed + input.Axis(KeyCode.F, KeyCode.R) * KeyLiftSpeed * dt;

                spinDelta = input.TwistDrag.x * TwistSpeed + input.Axis(KeyCode.Q, KeyCode.E) * KeySpinSpeed * dt;
                if (AutoSpin) spinDelta += AutoSpinSpeed * dt;
                tilt.x = Mathf.Clamp(tilt.x - input.TwistDrag.y * 0.25f, -MaxTilt, MaxTilt);
                tilt.y = Mathf.Clamp(tilt.y + input.Axis(KeyCode.Z, KeyCode.C) * 60f * dt, -MaxTilt, MaxTilt);
                tip = Constrain(tip, previous);
            }

            spin = Mathf.Repeat(spin + spinDelta, 360f);
            SpinSpeed = dt > 0f ? spinDelta / dt : 0f;

            smoothedTip = Vector3.Lerp(smoothedTip, tip, 1f - Mathf.Exp(-dt * 18f));
            transform.position = smoothedTip;
            // 傾きはカメラから見た左右 (z) と奥行き (x) 方向
            Vector3 camRight = cameraTransform.right;
            camRight.y = 0f;
            camRight.Normalize();
            Vector3 camForward = Vector3.Cross(camRight, Vector3.up);
            transform.rotation = Quaternion.AngleAxis(tilt.x, camRight) * Quaternion.AngleAxis(-tilt.y, camForward)
                                 * Quaternion.AngleAxis(spin, Vector3.up);
        }

        /// <summary>先端の目標位置を直接指定する (自動操縦用)。</summary>
        public void SetTarget(Vector3 position) => tip = Constrain(position, tip);

        public void ResetPose()
        {
            tip = smoothedTip = HomePosition;
            tilt = Vector2.zero;
            AutoSpin = false;
        }

        /// <summary>ボウルやヘッドを突き抜けないように先端の位置を制限する。</summary>
        public static Vector3 Constrain(Vector3 p, Vector3 previous)
        {
            p.y = Mathf.Clamp(p.y, MinY, MaxY);
            var flat = new Vector2(p.x, p.z);
            float r = flat.magnitude;
            float maxR = MaxRadius;
            float minR = 0f;

            if (p.y < GameConfig.BowlRimY)
            {
                float bowlR = GameConfig.BowlRadiusAt(p.y) - 0.02f;
                bool wasInside = previous.y < GameConfig.BowlRimY + 0.01f
                                 && new Vector2(previous.x, previous.z).magnitude <= GameConfig.BowlRadiusAt(previous.y);
                if (wasInside || r <= bowlR)
                    maxR = bowlR;
                else
                    p.y = GameConfig.BowlRimY; // ボウルの外から下げようとしたときは縁の上で止める
            }
            if (p.y < GameConfig.HeadCenter.y + 0.05f) minR = GameConfig.HeadRadius + 0.02f;

            if (r > maxR) flat *= maxR / r;
            else if (r < minR) flat = (r > 1e-4f ? flat / r : Vector2.right) * minR;
            return new Vector3(flat.x, p.y, flat.y);
        }
    }
}
