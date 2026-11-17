using UnityEngine;

namespace TenPercent2D.Market3D
{
    /// <summary>
    /// Market 场景的轻量后期：亮部泛光 + 调色 + 暗角。
    /// 不依赖 Post Processing Stack，Built-in 管线直接 OnRenderImage。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class MarketPostFX : MonoBehaviour
    {
        public Shader shader;

        [Header("泛光")]
        [Range(0f, 4f)] public float bloomThreshold = 1.0f;
        [Range(0f, 4f)] public float bloomIntensity = 0.85f;
        [Range(0f, 3f)] public float bloomRadius = 1.15f;
        public Color bloomTint = new Color(1f, 0.86f, 0.80f, 1f);

        [Header("调色")]
        [Range(0.2f, 3f)] public float exposure = 1.0f;
        [Range(0.5f, 1.6f)] public float contrast = 1.06f;
        [Range(0f, 2f)] public float saturation = 1.12f;
        [Range(0f, 2f)] public float vignette = 0.55f;
        [Range(-0.2f, 0.2f)] public float warmth = 0.045f;

        Material m_Material;

        void OnDisable()
        {
            if (m_Material != null)
            {
                if (Application.isPlaying) Destroy(m_Material); else DestroyImmediate(m_Material);
                m_Material = null;
            }
        }

        Material EnsureMaterial()
        {
            if (m_Material != null) return m_Material;
            if (shader == null) shader = Shader.Find("Market/PostFX");
            if (shader == null || !shader.isSupported) return null;
            m_Material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return m_Material;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            Material mat = EnsureMaterial();
            if (mat == null)
            {
                Graphics.Blit(src, dst);
                return;
            }

            mat.SetFloat("_Threshold", bloomThreshold);
            mat.SetFloat("_Intensity", bloomIntensity);
            mat.SetFloat("_Exposure", exposure);
            mat.SetFloat("_Contrast", contrast);
            mat.SetFloat("_Saturation", saturation);
            mat.SetFloat("_Vignette", vignette);
            mat.SetFloat("_Warmth", warmth);
            mat.SetColor("_Tint", bloomTint);

            int w = Mathf.Max(1, src.width / 4);
            int h = Mathf.Max(1, src.height / 4);

            RenderTexture bright = RenderTexture.GetTemporary(w, h, 0, src.format);
            RenderTexture tmp = RenderTexture.GetTemporary(w, h, 0, src.format);

            Graphics.Blit(src, bright, mat, 0);

            for (int i = 0; i < 3; i++)
            {
                mat.SetVector("_BlurDir", new Vector4(bloomRadius / w, 0f, 0f, 0f));
                Graphics.Blit(bright, tmp, mat, 1);
                mat.SetVector("_BlurDir", new Vector4(0f, bloomRadius / h, 0f, 0f));
                Graphics.Blit(tmp, bright, mat, 1);
            }

            mat.SetTexture("_BloomTex", bright);
            Graphics.Blit(src, dst, mat, 2);

            RenderTexture.ReleaseTemporary(bright);
            RenderTexture.ReleaseTemporary(tmp);
        }
    }
}
