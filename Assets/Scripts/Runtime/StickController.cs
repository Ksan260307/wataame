using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// 棒の操作。画面をドラッグすると棒の先端が指 (カーソル) の下についてくる。
    /// 高さはスライダー / ホイール / キー、回転は自動くるくる + 右ドラッグ / 2 本指ひねり / Q・E。
    /// おまかせ (自動で円を描く) とベストゾーンへの吸い寄せアシストもある。
    /// </summary>
    public sealed class StickController : MonoBehaviour
    {
        public enum Zone { Outside, Shallow, TooClose, TooFar, Good, Best }

        public struct Contact
        {
            public bool Wall, Head, Bottom, BlockedAtRim;
            public bool Any => Wall || Head || Bottom || BlockedAtRim;
        }

        const float KeyMoveSpeed = 0.32f;     // m / s
        const float RelativeDragSpeed = 0.0011f; // m / px (平面に投影できないとき)
        const float ScrollLift = 0.012f;      // m / ノッチ
        const float KeyLiftSpeed = 0.28f;     // m / s
        const float TwistSpeed = 0.7f;        // 度 / px
        const float KeySpinSpeed = 200f;      // 度 / s
        const float MaxTilt = 35f;
        const float MaxPointerSpeed = 3f;     // m / s
        const float PreciseFactor = 0.35f;
        const float GrabRadius = 40f;         // px (96 dpi 換算)
        const float MaxY = 0.75f;
        const float MaxRadius = 0.65f;

        public const float MinY = GameConfig.BowlBottomY + 0.035f;
        public const float BestTipY = 0.27f;
        public const float ShallowTipY = 0.34f;
        public const float BestRadiusMin = 0.13f;
        public const float BestRadiusMax = 0.22f;

        public static readonly Vector3 HomePosition = new Vector3(0.18f, 0.46f, -0.06f);
        static readonly Vector3 ShowcasePosition = new Vector3(0f, 0.55f, -0.12f);

        public bool AutoSpin { get; set; } = true;
        public bool Orbit { get; set; }
        public bool Showcase { get; set; }
        public bool Grabbing { get; private set; }
        public bool Hovering { get; private set; }
        public float SpinSpeed { get; private set; }
        public Vector2 Tilt => SoftTilt(rawTilt);
        public Vector3 TargetTip => tip;
        public Zone CurrentZone { get; private set; } = Zone.Outside;
        public Contact LastContact { get; private set; }
        /// <summary>壁などに当たった瞬間に呼ばれる。</summary>
        public System.Action<Vector3> Bumped;

        GameSettings settings;
        CottonCandy candy;
        Vector3 tip = HomePosition;
        Vector3 smoothedTip = HomePosition;
        Vector3 bumpOffset;
        Vector2 rawTilt;
        Vector2 lastPointer;
        float spin, spinVelocity, keySpinHeld;
        bool wasTouching;

        public void Init(GameSettings gameSettings, CottonCandy cottonCandy)
        {
            settings = gameSettings;
            candy = cottonCandy;
        }

        float CandyRadius => candy != null ? candy.ApproxRadius : GameConfig.StickRadius;
        static float DpiScale => Mathf.Max(1f, (Screen.dpi > 0f ? Screen.dpi : 96f) / 96f);

        public void Drive(GameInput input, Camera cam, float dt, bool machineMaking)
        {
            if (input.ToggleAutoSpin && !Showcase) AutoSpin = !AutoSpin;

            float manualSpin = 0f;
            Vector3 previous = tip;
            Hovering = !Showcase && input.HasHover && !input.PointerHeld && ScreenDistanceToStick(cam, input.HoverScreen) < GrabRadius * DpiScale;

            if (Showcase)
            {
                // できあがり表示: 目の前に持ち上げてゆっくり回す
                Grabbing = false;
                Orbit = false;
                tip = Vector3.Lerp(tip, ShowcasePosition, 1f - Mathf.Exp(-dt * 4f));
                rawTilt = Vector2.Lerp(rawTilt, Vector2.zero, 1f - Mathf.Exp(-dt * 4f));
                manualSpin = 40f * dt;
                previous = tip;
            }
            else
            {
                DrivePointer(input, cam, dt);
                DriveKeys(input, cam, dt);
                manualSpin = DriveSpin(input, dt);
                DriveTilt(input, dt);
                if (Orbit) DriveOrbit(dt);
                else if (settings.Magnet && machineMaking && !Grabbing) DriveMagnet(dt);
            }

            if (!Showcase)
            {
                tip = Constrain(tip, previous, CandyRadius, out var contact);
                LastContact = contact;
                bool touching = contact.Wall || contact.Head || contact.Bottom;
                if (touching && !wasTouching)
                {
                    // 当たった方向と逆に少しはね返る
                    Vector3 push = (previous - tip);
                    push.y = 0f;
                    bumpOffset = (push.sqrMagnitude > 1e-8f ? -push.normalized : Vector3.zero) * 0.006f;
                    Bumped?.Invoke(tip);
                }
                wasTouching = touching;
            }

            float autoSpin = AutoSpin && !Showcase ? settings.SpinSpeed * (settings.Clockwise ? 1f : -1f) * dt : 0f;
            spin = Mathf.Repeat(spin + manualSpin + autoSpin, 360f);
            SpinSpeed = dt > 0f ? (manualSpin + autoSpin) / dt : 0f;
            CurrentZone = Classify(tip);

            // 速く動かしたときはしっかり追従し、止まっているときはなめらかに
            float gap = (tip - smoothedTip).magnitude;
            float rate = Mathf.Lerp(12f, 28f, Mathf.Clamp01(gap / 0.1f));
            smoothedTip = Vector3.Lerp(smoothedTip, tip, 1f - Mathf.Exp(-dt * rate));
            bumpOffset = Vector3.Lerp(bumpOffset, Vector3.zero, 1f - Mathf.Exp(-dt * 14f));
            transform.position = smoothedTip + bumpOffset;

            // 傾きはカメラから見た左右と奥行き方向
            Vector3 camRight = FlatRight(cam.transform);
            Vector3 camForward = Vector3.Cross(camRight, Vector3.up);
            Vector2 tilt = Tilt;
            transform.rotation = Quaternion.AngleAxis(tilt.x, camRight) * Quaternion.AngleAxis(-tilt.y, camForward)
                                 * Quaternion.AngleAxis(spin, Vector3.up);
        }

        void DrivePointer(GameInput input, Camera cam, float dt)
        {
            if (input.PointerDown)
            {
                Grabbing = true;
                lastPointer = input.PointerScreen;
                // 棒の近くを押したらつかむ (ずれを保つ)。離れたところを押したら棒がそこへ移動する
                bool nearStick = ScreenDistanceToStick(cam, input.PointerScreen) < GrabRadius * DpiScale;
                if (!nearStick && ProjectToHeight(cam, input.PointerScreen, tip.y, out Vector3 p))
                {
                    tip.x = p.x;
                    tip.z = p.z;
                }
                if (Orbit) Orbit = false; // 手で動かしたらおまかせは止める
            }

            if (Grabbing && input.PointerHeld && input.PointerScreen != lastPointer)
            {
                Vector3 delta;
                if (ProjectToHeight(cam, lastPointer, tip.y, out Vector3 from) && ProjectToHeight(cam, input.PointerScreen, tip.y, out Vector3 to))
                {
                    delta = to - from;
                }
                else
                {
                    // 地平線より上を指しているときは画面上の移動量で動かす
                    Vector2 px = (input.PointerScreen - lastPointer) / DpiScale * RelativeDragSpeed * settings.Sensitivity;
                    delta = FlatRight(cam.transform) * px.x + Vector3.Cross(FlatRight(cam.transform), Vector3.up) * px.y;
                }
                delta.y = 0f;
                if (input.Precise) delta *= PreciseFactor;
                delta = Vector3.ClampMagnitude(delta, MaxPointerSpeed * Mathf.Max(dt, 1f / 30f));
                tip += delta;
                lastPointer = input.PointerScreen;
            }

            if (!input.PointerHeld) Grabbing = false;
        }

        void DriveKeys(GameInput input, Camera cam, float dt)
        {
            Vector3 right = FlatRight(cam.transform);
            Vector3 forward = Vector3.Cross(right, Vector3.up);
            var move = new Vector2(
                input.Axis(KeyCode.A, KeyCode.D, KeyCode.LeftArrow, KeyCode.RightArrow),
                input.Axis(KeyCode.S, KeyCode.W, KeyCode.DownArrow, KeyCode.UpArrow));
            if (move.sqrMagnitude > 1f) move.Normalize(); // 斜めでも同じ速さ
            float speed = KeyMoveSpeed * settings.Sensitivity * (input.Precise ? PreciseFactor : 1f) * dt;
            if (move != Vector2.zero)
            {
                tip += (right * move.x + forward * move.y) * speed;
                Orbit = false;
            }

            float lift = input.Axis(KeyCode.F, KeyCode.R, KeyCode.PageDown, KeyCode.PageUp) * KeyLiftSpeed * (input.Precise ? PreciseFactor : 1f) * dt;
            tip.y += lift + input.Scroll * ScrollLift * (input.Precise ? PreciseFactor : 1f);
        }

        float DriveSpin(GameInput input, float dt)
        {
            float keyAxis = input.Axis(KeyCode.Q, KeyCode.E);
            keySpinHeld = keyAxis != 0f ? keySpinHeld + dt : 0f;
            // 押し続けると加速する
            float keySpin = keyAxis * KeySpinSpeed * (1f + Mathf.Min(keySpinHeld, 1.5f)) * dt;
            // 画面上で時計回りにひねると、上から見て時計回りに回る
            float gesture = input.TwistDrag.x * TwistSpeed - input.TwistDegrees * 1.5f;

            if (gesture != 0f && dt > 0f)
                spinVelocity = Mathf.Lerp(spinVelocity, gesture / dt, 0.5f);
            else
                spinVelocity *= Mathf.Exp(-dt * 3f); // 離しても少しだけ回り続ける

            return keySpin + (gesture != 0f ? gesture : spinVelocity * dt);
        }

        void DriveTilt(GameInput input, float dt)
        {
            rawTilt.x -= input.TwistDrag.y * 0.25f;
            rawTilt.y += input.Axis(KeyCode.Z, KeyCode.C) * 60f * dt;
            rawTilt = Vector2.ClampMagnitude(rawTilt, MaxTilt * 2f);
        }

        void DriveOrbit(float dt)
        {
            // ヘッドのまわりをゆっくり円を描いて回る
            float angle = Mathf.Atan2(tip.z, tip.x);
            float r = new Vector2(tip.x, tip.z).magnitude;
            angle += (settings.Clockwise ? -1f : 1f) * 0.55f * dt;
            r = Mathf.Lerp(r, settings.OrbitRadius, 1f - Mathf.Exp(-dt * 2f));
            if (tip.y > ShallowTipY || CurrentZone == Zone.Outside)
                tip.y = Mathf.Lerp(tip.y, BestTipY, 1f - Mathf.Exp(-dt * 2.5f));
            tip.x = Mathf.Cos(angle) * r;
            tip.z = Mathf.Sin(angle) * r;
        }

        void DriveMagnet(float dt)
        {
            if (CurrentZone != Zone.Good && CurrentZone != Zone.TooFar && CurrentZone != Zone.TooClose) return;
            var flat = new Vector2(tip.x, tip.z);
            float r = flat.magnitude;
            if (r < 1e-4f) return;
            float target = Mathf.Clamp(r, BestRadiusMin + 0.01f, BestRadiusMax - 0.01f);
            flat *= Mathf.MoveTowards(r, target, 0.06f * dt) / r;
            tip.x = flat.x;
            tip.z = flat.y;
        }

        /// <summary>高さを直接指定する (UI のスライダー)。</summary>
        public float TargetHeight
        {
            get => tip.y;
            set
            {
                Vector3 previous = tip;
                tip.y = value;
                tip = Constrain(tip, previous, CandyRadius, out var contact);
                LastContact = contact;
            }
        }

        /// <summary>ボウルの中のちょうどいい位置へ棒を入れる。</summary>
        public void DipIntoBowl()
        {
            var flat = new Vector2(tip.x, tip.z);
            if (flat.sqrMagnitude < 1e-4f) flat = new Vector2(1f, -0.3f);
            flat = flat.normalized * settings.OrbitRadius;
            // まず縁の上に移動してから下げる (壁を突き抜けないように)
            tip = new Vector3(flat.x, Mathf.Max(tip.y, GameConfig.BowlRimY + 0.02f), flat.y);
            Vector3 above = tip;
            tip.y = BestTipY;
            tip = Constrain(tip, above, CandyRadius, out _);
        }

        public void LiftOut()
        {
            Orbit = false;
            tip.y = GameConfig.BowlRimY + 0.08f;
        }

        public void TiltBy(Vector2 degrees) => rawTilt = Vector2.ClampMagnitude(rawTilt + degrees, MaxTilt * 2f);

        public void ResetTilt() => rawTilt = Vector2.zero;

        /// <summary>先端の目標位置を直接指定する (自動操縦用)。</summary>
        public void SetTarget(Vector3 position) => tip = Constrain(position, tip, CandyRadius, out _);

        public void ResetPose()
        {
            tip = smoothedTip = HomePosition;
            rawTilt = Vector2.zero;
            spinVelocity = 0f;
            AutoSpin = true;
            Orbit = false;
        }

        /// <summary>傾きの上限に近づくほどゆっくりになる。</summary>
        static Vector2 SoftTilt(Vector2 raw)
        {
            float m = raw.magnitude;
            if (m < 1e-4f) return Vector2.zero;
            float soft = MaxTilt * (float)System.Math.Tanh(m / MaxTilt);
            return raw * (soft / m);
        }

        static Vector3 FlatRight(Transform cam)
        {
            Vector3 right = cam.right;
            right.y = 0f;
            return right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
        }

        /// <summary>スクリーン座標を高さ y の水平面に投影する。</summary>
        public static bool ProjectToHeight(Camera cam, Vector2 screen, float y, out Vector3 point)
        {
            Ray ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            point = default;
            if (Mathf.Abs(ray.direction.y) < 0.05f) return false;
            float t = (y - ray.origin.y) / ray.direction.y;
            if (t <= 0f || t > 20f) return false;
            point = ray.origin + ray.direction * t;
            return true;
        }

        /// <summary>スクリーン上で、棒 (先端〜持ち手) までの距離 (px)。</summary>
        public float ScreenDistanceToStick(Camera cam, Vector2 screen)
        {
            Vector3 a = cam.WorldToScreenPoint(transform.position);
            Vector3 b = cam.WorldToScreenPoint(transform.position + transform.up * GameConfig.StickLength);
            if (a.z <= 0f || b.z <= 0f) return float.MaxValue;
            Vector2 a2 = a, b2 = b;
            Vector2 ab = b2 - a2;
            float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(screen - a2, ab) / ab.sqrMagnitude) : 0f;
            float stickDistance = (screen - (a2 + ab * t)).magnitude;
            // わたあめの部分もつかめるようにする
            Vector3 c = cam.WorldToScreenPoint(candy != null ? candy.WorldCenter : transform.position);
            float candyPx = candy != null && candy.Field.TotalGrams > 0.2f
                ? Mathf.Abs(cam.WorldToScreenPoint(candy.WorldCenter + cam.transform.right * candy.ApproxRadius).x - c.x)
                : 0f;
            float candyDistance = c.z > 0f ? (screen - (Vector2)c).magnitude - candyPx : float.MaxValue;
            return Mathf.Min(stickDistance, candyDistance);
        }

        /// <summary>棒の先端の位置が、わたあめ作りにどれくらい向いているか。</summary>
        public static Zone Classify(Vector3 p)
        {
            float r = new Vector2(p.x, p.z).magnitude;
            if (p.y > GameConfig.BowlRimY + 0.03f || r > GameConfig.BowlRadiusAt(Mathf.Min(p.y, GameConfig.BowlRimY))) return Zone.Outside;
            if (p.y > ShallowTipY) return Zone.Shallow;
            if (r < GameConfig.HeadRadius + 0.05f) return Zone.TooClose;
            if (r > 0.245f) return Zone.TooFar;
            return r >= BestRadiusMin && r <= BestRadiusMax ? Zone.Best : Zone.Good;
        }

        /// <summary>
        /// ボウルやヘッドを突き抜けないように先端の位置を制限する。
        /// わたあめが大きいほど壁やヘッドから離す。ボウルの中から外へ大きく引くと縁を越えて持ち上がる。
        /// </summary>
        public static Vector3 Constrain(Vector3 p, Vector3 previous, float candyRadius, out Contact contact)
        {
            contact = default;
            if (p.y < MinY)
            {
                contact.Bottom = p.y < MinY - 1e-4f; // 底より下へ押し込もうとした
                p.y = MinY;
            }
            p.y = Mathf.Min(p.y, MaxY);

            var flat = new Vector2(p.x, p.z);
            float r = flat.magnitude;
            float maxR = MaxRadius;
            float minR = 0f;
            float wallMargin = 0.02f + Mathf.Max(0f, candyRadius - 0.03f) * 0.6f;

            if (p.y < GameConfig.BowlRimY)
            {
                float bowlR = GameConfig.BowlRadiusAt(p.y) - wallMargin;
                bool wasInside = previous.y < GameConfig.BowlRimY + 0.01f
                                 && new Vector2(previous.x, previous.z).magnitude <= GameConfig.BowlRadiusAt(Mathf.Min(previous.y, GameConfig.BowlRimY)) + 0.01f;
                if (wasInside)
                {
                    if (r > bowlR + 0.06f)
                        p.y = GameConfig.BowlRimY + 0.02f; // 大きく外へ引いたら縁を越えて持ち上げる
                    else if (r > bowlR)
                    {
                        maxR = bowlR;
                        contact.Wall = true;
                    }
                    else
                        maxR = bowlR;
                }
                else if (r > GameConfig.BowlRadiusAt(p.y) - 0.02f)
                {
                    p.y = GameConfig.BowlRimY; // ボウルの外から下げようとしたときは縁の上で止める
                    contact.BlockedAtRim = true;
                }
            }
            if (p.y < GameConfig.HeadCenter.y + 0.05f + candyRadius * 0.5f)
                minR = GameConfig.HeadRadius + 0.02f + candyRadius * 0.5f;
            maxR = Mathf.Max(maxR, minR + 0.01f);

            if (r > maxR)
            {
                flat *= maxR / r;
            }
            else if (r < minR)
            {
                flat = (r > 1e-4f ? flat / r : Vector2.right) * minR;
                contact.Head = true;
            }
            return new Vector3(flat.x, p.y, flat.y);
        }
    }
}
