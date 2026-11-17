using UnityEngine;

namespace TenPercent2D.Market3D
{
    /// <summary>
    /// 极轻微的手持呼吸感，只在 Play 模式下生效，方便录屏/看效果时画面不“死”。
    /// </summary>
    public class MarketCameraDrift : MonoBehaviour
    {
        public Vector3 positionAmplitude = new Vector3(0.018f, 0.012f, 0.010f);
        public Vector3 rotationAmplitude = new Vector3(0.28f, 0.42f, 0.16f);
        public float speed = 0.22f;

        Vector3 m_BasePosition;
        Quaternion m_BaseRotation;
        float m_Seed;

        void Start()
        {
            m_BasePosition = transform.localPosition;
            m_BaseRotation = transform.localRotation;
            m_Seed = Random.value * 100f;

            if (!Application.isPlaying) enabled = false;
        }

        void LateUpdate()
        {
            float t = Time.time * speed + m_Seed;
            float nx = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f;
            float nz = (Mathf.PerlinNoise(t * 1.3f, 2.1f) - 0.5f) * 2f;

            transform.localPosition = m_BasePosition + Vector3.Scale(positionAmplitude, new Vector3(nx, ny, nz));
            transform.localRotation = m_BaseRotation * Quaternion.Euler(
                rotationAmplitude.x * ny,
                rotationAmplitude.y * nx,
                rotationAmplitude.z * nz);
        }
    }
}
