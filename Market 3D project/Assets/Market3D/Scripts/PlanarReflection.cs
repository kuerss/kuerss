using UnityEngine;

namespace TenPercent2D.Market3D
{
    /// <summary>
    /// 平面反射：为某个平面生成一台镜像相机，把渲染结果塞进材质的 _ReflectionTex。
    /// 湿路面（水平面，法线朝上）和玻璃（垂直面，法线朝店内）各挂一个。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PlanarReflection : MonoBehaviour
    {
        public Vector3 planeNormalLocal = Vector3.up;
        public float clipPlaneOffset = 0.04f;
        public int textureWidth = 1024;
        public int textureHeight = 576;
        public Material[] targetMaterials = new Material[0];
        public string textureProperty = "_ReflectionTex";
        public LayerMask cullingMask = ~0;
        public bool hideReflectionCameraInHierarchy = true;

        Camera m_ReflectionCamera;
        RenderTexture m_Texture;

        public RenderTexture ReflectionTexture => m_Texture;

        void OnEnable()
        {
            RenderReflection(Camera.main);
        }

        void OnDisable()
        {
            Release();
        }

        void OnValidate()
        {
            textureWidth = Mathf.Clamp(textureWidth, 64, 4096);
            textureHeight = Mathf.Clamp(textureHeight, 64, 4096);
        }

        void LateUpdate()
        {
            RenderReflection(Camera.main);
        }

        public void RenderReflection(Camera sourceCamera)
        {
            if (!enabled || sourceCamera == null) return;
            if (sourceCamera.transform == transform) return;

            EnsureResources(sourceCamera);

            Camera cam = m_ReflectionCamera;
            cam.CopyFrom(sourceCamera);
            cam.targetTexture = m_Texture;
            cam.cullingMask = cullingMask;
            cam.clearFlags = sourceCamera.clearFlags;
            cam.backgroundColor = sourceCamera.backgroundColor;
            cam.depth = sourceCamera.depth - 20f;
            cam.enabled = false;
            cam.allowHDR = sourceCamera.allowHDR;
            cam.farClipPlane = Mathf.Max(sourceCamera.farClipPlane, 100f);

            Vector3 pos = transform.position;
            Vector3 normal = transform.TransformDirection(planeNormalLocal).normalized;

            float d = -Vector3.Dot(normal, pos) - clipPlaneOffset;
            Vector4 reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

            Matrix4x4 reflection = Matrix4x4.identity;
            CalculateReflectionMatrix(ref reflection, reflectionPlane);

            Vector3 newPos = reflection.MultiplyPoint(sourceCamera.transform.position);
            Vector3 euler = sourceCamera.transform.eulerAngles;

            cam.transform.position = newPos;
            cam.transform.eulerAngles = new Vector3(-euler.x, euler.y, euler.z);
            cam.worldToCameraMatrix = sourceCamera.worldToCameraMatrix * reflection;

            Vector4 clipPlane = CameraSpacePlane(cam, pos, normal, 1f);
            cam.projectionMatrix = sourceCamera.CalculateObliqueMatrix(clipPlane);

            bool oldInvert = GL.invertCulling;
            GL.invertCulling = true;
            cam.Render();
            GL.invertCulling = oldInvert;

            PushTexture();
        }

        void PushTexture()
        {
            if (m_Texture == null) return;
            if (targetMaterials == null) return;
            for (int i = 0; i < targetMaterials.Length; i++)
            {
                Material m = targetMaterials[i];
                if (m != null) m.SetTexture(textureProperty, m_Texture);
            }
        }

        void EnsureResources(Camera sourceCamera)
        {
            if (m_ReflectionCamera == null)
            {
                var go = new GameObject("~ReflectionCamera_" + name);
                go.transform.SetParent(transform, false);
                if (hideReflectionCameraInHierarchy) go.hideFlags = HideFlags.HideAndDontSave;
                m_ReflectionCamera = go.AddComponent<Camera>();
                m_ReflectionCamera.enabled = false;
                m_ReflectionCamera.allowMSAA = true;
            }

            int w = Mathf.Max(16, textureWidth);
            int h = Mathf.Max(16, textureHeight);
            if (m_Texture == null || m_Texture.width != w || m_Texture.height != h)
            {
                Release();
                m_Texture = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Default)
                {
                    name = "~PlanarReflection_" + name,
                    hideFlags = HideFlags.HideAndDontSave,
                    useMipMap = false,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                m_Texture.Create();
            }
        }

        void Release()
        {
            if (m_Texture != null)
            {
                if (Application.isPlaying) Destroy(m_Texture); else DestroyImmediate(m_Texture);
                m_Texture = null;
            }
        }

        static void CalculateReflectionMatrix(ref Matrix4x4 m, Vector4 p)
        {
            m.m00 = 1f - 2f * p.x * p.x;
            m.m01 = -2f * p.x * p.y;
            m.m02 = -2f * p.x * p.z;
            m.m03 = -2f * p.x * p.w;

            m.m10 = -2f * p.y * p.x;
            m.m11 = 1f - 2f * p.y * p.y;
            m.m12 = -2f * p.y * p.z;
            m.m13 = -2f * p.y * p.w;

            m.m20 = -2f * p.z * p.x;
            m.m21 = -2f * p.z * p.y;
            m.m22 = 1f - 2f * p.z * p.z;
            m.m23 = -2f * p.z * p.w;

            m.m30 = 0f;
            m.m31 = 0f;
            m.m32 = 0f;
            m.m33 = 1f;
        }

        Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
        {
            Vector3 offsetPos = pos + normal * clipPlaneOffset;
            Matrix4x4 m = cam.worldToCameraMatrix;
            Vector3 cpos = m.MultiplyPoint(offsetPos);
            Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }
    }
}
