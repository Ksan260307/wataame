using UnityEngine;

namespace Wataame
{
    /// <summary>機械のまわりを回るカメラ。できあがり表示ではわたあめに寄って自動で回る。</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        static readonly Vector3 PlayTarget = new Vector3(0f, 0.33f, 0f);
        const float PlayDistance = 1.3f;
        const float DefaultPitch = 30f;

        public Camera Camera { get; private set; }

        float yaw, pitch = DefaultPitch, distance = PlayDistance;
        Vector3 target = PlayTarget;
        float showcaseYaw;

        void Awake()
        {
            Camera = gameObject.AddComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = WataameMaterials.SkyColor;
            Camera.fieldOfView = 38f;
            Camera.nearClipPlane = 0.02f;
            Camera.farClipPlane = 30f;
            gameObject.AddComponent<AudioListener>();
            gameObject.tag = "MainCamera";
            Apply();
        }

        public void Drive(GameInput input, bool showcase, Vector3 focus, float focusSize, float dt)
        {
            float k = 1f - Mathf.Exp(-dt * 5f);
            if (showcase)
            {
                showcaseYaw += 18f * dt + input.OrbitDrag.x * 0.3f;
                yaw = Mathf.LerpAngle(yaw, showcaseYaw, k);
                pitch = Mathf.Lerp(pitch, 15f, k);
                distance = Mathf.Lerp(distance, Mathf.Max(0.6f, focusSize * 5f), k);
                target = Vector3.Lerp(target, focus, k);
            }
            else
            {
                yaw += input.OrbitDrag.x * 0.3f;
                pitch = Mathf.Clamp(pitch - input.OrbitDrag.y * 0.3f, 5f, 75f);
                showcaseYaw = yaw;
                distance = Mathf.Lerp(distance, PlayDistance, k);
                target = Vector3.Lerp(target, PlayTarget, k);
            }
            Apply();
        }

        void Apply()
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
        }
    }
}
