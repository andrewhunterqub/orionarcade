using UnityEngine;

namespace Loab.Game
{
    /// <summary>
    /// Miniature tilt-shift (visual brief 5). Cheap: two blur passes at quarter resolution, then a band mask.
    /// UI Toolkit draws after the camera, so interface, icons and thought bubbles are never blurred.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class TiltShift : MonoBehaviour
    {
        public Material Mat;
        public bool Enabled = true;
        public float Strength = 0.8f;   // 0..1 blend of the blurred image at the band edges
        public float Band = 0.2f;       // half-height of the sharp band in screen units (0..0.5)
        public float Softness = 0.22f;
        public float Spread = 1.4f;
        public float Focus = 0.5f;

        static readonly int ParamsId = Shader.PropertyToID("_Params");
        static readonly int DirId = Shader.PropertyToID("_Dir");
        static readonly int BlurId = Shader.PropertyToID("_BlurTex");

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (Mat == null || !Enabled || Strength < 0.02f) { Graphics.Blit(src, dst); return; }
            int w = Mathf.Max(1, src.width / 4), h = Mathf.Max(1, src.height / 4);
            var a = RenderTexture.GetTemporary(w, h, 0, src.format);
            var b = RenderTexture.GetTemporary(w, h, 0, src.format);
            a.filterMode = FilterMode.Bilinear; b.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, a);
            for (int i = 0; i < 2; i++)
            {
                Mat.SetVector(DirId, new Vector4(Spread * (i + 1), 0, 0, 0));
                Graphics.Blit(a, b, Mat, 0);
                Mat.SetVector(DirId, new Vector4(0, Spread * (i + 1), 0, 0));
                Graphics.Blit(b, a, Mat, 0);
            }
            Mat.SetTexture(BlurId, a);
            Mat.SetVector(ParamsId, new Vector4(Focus, Band, Softness, Strength));
            Graphics.Blit(src, dst, Mat, 1);
            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
        }
    }
}
