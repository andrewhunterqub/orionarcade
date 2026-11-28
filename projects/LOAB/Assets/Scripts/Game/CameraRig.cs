using UnityEngine;

namespace Loab.Game
{
    /// <summary>
    /// Diorama camera (visual brief 4.1): narrow-FOV perspective from a distance, so the bar reads like a macro
    /// photo of a scale model. Snaps between four corner views; drag to pan, wheel/pinch to zoom.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public const float Fov = 22f;
        public const float MinDist = 16f, MaxDist = 62f;

        public Camera Cam;
        public Vector3 Target;
        public float Zoom = 46f;      // distance from target
        public float Pitch = 40f;
        public int YawStep;           // 0..3
        float _yaw = 45f, _dist, _yawVel;
        Vector3 _min, _max;

        public static CameraRig Create(Vector3 roomCentre, Vector3 min, Vector3 max)
        {
            var go = new GameObject("CameraRig");
            var rig = go.AddComponent<CameraRig>();
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(go.transform, false);
            rig.Cam = camGo.AddComponent<Camera>();
            rig.Cam.orthographic = false;
            rig.Cam.fieldOfView = Fov;
            rig.Cam.nearClipPlane = 1f;
            rig.Cam.farClipPlane = 300f;
            rig.Cam.clearFlags = CameraClearFlags.SolidColor;
            rig.Cam.backgroundColor = new Color(0.82f, 0.86f, 0.9f);
            camGo.AddComponent<AudioListener>();
            rig.Target = roomCentre + new Vector3(0, 0, -1.6f);
            rig._min = min; rig._max = max;
            rig._dist = rig.Zoom;
            rig.Apply(true);
            return rig;
        }

        public void SetBounds(Vector3 min, Vector3 max) { _min = min; _max = max; }

        public void Rotate(int dir) => YawStep = (YawStep + dir + 4) % 4;

        public void ZoomBy(float delta) => Zoom = Mathf.Clamp(Zoom * (1f + delta), MinDist, MaxDist);

        /// <summary>0 at full-plinth view, 1 at table-top view.</summary>
        public float ZoomT => Mathf.InverseLerp(MaxDist, MinDist, _dist);

        /// <summary>World units visible vertically at the target distance.</summary>
        public float VisibleHeight => 2f * _dist * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);

        public void Pan(Vector2 screenDelta)
        {
            float unitsPerPixel = VisibleHeight / Screen.height;
            var right = Cam.transform.right; right.y = 0; right.Normalize();
            var fwd = Cam.transform.forward; fwd.y = 0; fwd.Normalize();
            Target -= (right * screenDelta.x + fwd * screenDelta.y / Mathf.Sin(Pitch * Mathf.Deg2Rad)) * unitsPerPixel;
            Target.x = Mathf.Clamp(Target.x, _min.x, _max.x);
            Target.z = Mathf.Clamp(Target.z, _min.z, _max.z);
        }

        public bool ScreenToGround(Vector2 screen, out Vector3 world)
        {
            var ray = Cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float d)) { world = ray.GetPoint(d); return true; }
            world = default;
            return false;
        }

        void LateUpdate() => Apply(false);

        /// Aspect-aware framing (task Z6): screens narrower than 16:9 (a 4:3 tablet, a tablet held upright)
        /// widen the vertical field of view so the room is as wide as it is on 16:9, not cropped at the sides.
        public static float FovFor(float aspect)
        {
            const float Ref = 16f / 9f;
            if (aspect >= Ref || aspect <= 0f) return Fov;
            float halfH = Mathf.Atan(Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) * Ref);
            return Mathf.Min(55f, 2f * Mathf.Atan(Mathf.Tan(halfH) / aspect) * Mathf.Rad2Deg);
        }

        void Apply(bool snap)
        {
            float targetYaw = 45f + YawStep * 90f;
            float dt = Time.unscaledDeltaTime;
            // ~0.35 s ease between corner views (brief 4.1)
            _yaw = snap ? targetYaw : Mathf.SmoothDampAngle(_yaw, targetYaw, ref _yawVel, 0.12f, 2000f, dt);
            _dist = snap ? Zoom : Mathf.Lerp(_dist, Zoom, 1f - Mathf.Exp(-dt * 12f));
            Cam.fieldOfView = FovFor(Cam.aspect);
            var rot = Quaternion.Euler(Pitch, _yaw, 0);
            transform.position = Target;
            Cam.transform.rotation = rot;
            Cam.transform.position = Target - rot * Vector3.forward * _dist;
        }

        /// <summary>Which room edges currently face the camera (those walls get cut away).</summary>
        public void FrontWalls(out bool south, out bool west, out bool north, out bool east)
        {
            var f = Cam.transform.forward;
            south = f.z > 0.1f; north = f.z < -0.1f;
            west = f.x > 0.1f; east = f.x < -0.1f;
        }
    }
}
