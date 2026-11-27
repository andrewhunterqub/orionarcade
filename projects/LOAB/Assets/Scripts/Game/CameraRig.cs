using UnityEngine;

namespace Loab.Game
{
    /// <summary>Isometric orthographic camera: drag to pan, wheel/pinch to zoom, 90 degree rotation steps.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public Vector3 Target;
        public float Zoom = 6.8f;
        public int YawStep;          // 0..3
        float _yaw = 45f, _zoomVel;
        Vector3 _min, _max;

        public static CameraRig Create(Vector3 roomCentre, Vector3 min, Vector3 max)
        {
            var go = new GameObject("CameraRig");
            var rig = go.AddComponent<CameraRig>();
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(go.transform, false);
            rig.Cam = camGo.AddComponent<Camera>();
            rig.Cam.orthographic = true;
            rig.Cam.nearClipPlane = 0.1f;
            rig.Cam.farClipPlane = 200f;
            rig.Cam.clearFlags = CameraClearFlags.SolidColor;
            rig.Cam.backgroundColor = new Color(0.05f, 0.06f, 0.13f);
            camGo.AddComponent<AudioListener>();
            rig.Target = roomCentre;
            rig._min = min; rig._max = max;
            rig.Apply(true);
            return rig;
        }

        public void Rotate(int dir)
        {
            YawStep = (YawStep + dir + 4) % 4;
        }

        public void ZoomBy(float delta) => Zoom = Mathf.Clamp(Zoom * (1f + delta), 3.5f, 13f);

        public void Pan(Vector2 screenDelta)
        {
            // Convert a screen-space drag into ground-plane movement.
            float unitsPerPixel = Cam.orthographicSize * 2f / Screen.height;
            var right = Cam.transform.right; right.y = 0; right.Normalize();
            var fwd = Cam.transform.up; fwd.y = 0; fwd.Normalize();
            Target -= (right * screenDelta.x + fwd * screenDelta.y / Mathf.Sin(35f * Mathf.Deg2Rad)) * unitsPerPixel;
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

        void Apply(bool snap)
        {
            float targetYaw = 45f + YawStep * 90f;
            _yaw = snap ? targetYaw : Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f));
            Cam.orthographicSize = snap ? Zoom : Mathf.SmoothDamp(Cam.orthographicSize, Zoom, ref _zoomVel, 0.08f, 100f, Time.unscaledDeltaTime);
            var rot = Quaternion.Euler(35f, _yaw, 0);
            transform.position = Target;
            Cam.transform.rotation = rot;
            Cam.transform.position = Target - rot * Vector3.forward * 40f;
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
