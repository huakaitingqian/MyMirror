using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode] //即使在编辑模式下也能实时响应
public class MyMirror : MonoBehaviour
{
    public bool disablePixelLights = true; //禁用像素光（平行光）
    public int textureSize = 256; //将反射相机的画面渲染到一张贴图上，贴图越大结果越光滑，资源消耗越大
    public float clipPlaneOffset = 0.07f; //裁剪平面偏移？？
    public LayerMask reflectLayers = -1; //层级遮罩，为Int32类型，每一位代表一个层级，-1表示所有层级

    private Dictionary<Camera, Camera> m_ReflectionCameras = new Dictionary<Camera, Camera>(); //反射相机
    private RenderTexture m_ReflectionTexture; //呈现反射相机画面的RenderTexture贴图
    private int m_OldReflectionTextureSize; //用于存储原有的渲染贴图大小

    /// <summary>
    /// 当物体要被某个相机渲染时调用此函数
    /// 此函数实现了渲染反射
    /// 因为该脚本设置为可以在编辑模式下执行，所以反射效果可以在scene中看到
    /// </summary>
    public void OnWillRenderObject()
    {
        //确保该脚本已经被启用并且存在渲染组件，有材质，并且被启用
        if (!enabled || !GetComponent<Renderer>() || !GetComponent<Renderer>().sharedMaterial || !GetComponent<Renderer>().enabled)
        {
            return;
        }

        //获取当前相机，并且判断是否正常
        Camera cam = Camera.current;
        if (!cam)
        {
            return;
        }

        Camera reflectionCamera;
        CreateMirrorObjects(cam, out reflectionCamera);

        Vector3 pos = transform.position;
        Vector3 normal = transform.up;

        int oldPixelLightCount = QualitySettings.pixelLightCount;
        if (disablePixelLights)
        {
            QualitySettings.pixelLightCount = 0;
        }

        UpdateCameraModes(cam, reflectionCamera);

        float d = -Vector3.Dot(normal, pos) - clipPlaneOffset;
        Vector4 reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

        Matrix4x4 reflection = Matrix4x4.zero;
        CalculateReflectionMatrix(ref reflection, reflectionPlane);

        Vector3 oldpos = cam.transform.position;
        Vector3 newpos = reflection.MultiplyPoint(oldpos);
        reflectionCamera.worldToCameraMatrix = cam.worldToCameraMatrix * reflection;

        Vector4 clipPlane = CameraSpacePlane(reflectionCamera, pos, normal, 1.0f);
        reflectionCamera.projectionMatrix = cam.CalculateObliqueMatrix(clipPlane);

        reflectionCamera.cullingMatrix = cam.projectionMatrix * cam.worldToCameraMatrix;

        reflectionCamera.cullingMask = ~(1 << 4) & reflectLayers.value;
        reflectionCamera.targetTexture = m_ReflectionTexture;
        bool oldCulling = GL.invertCulling;
        GL.invertCulling = !oldCulling;
        reflectionCamera.transform.position = newpos;
        Vector3 euler = cam.transform.eulerAngles;
        reflectionCamera.transform.eulerAngles = new Vector3(-euler.x, euler.y, euler.z);
        reflectionCamera.Render();
        reflectionCamera.transform.position = oldpos;
        GL.invertCulling = oldCulling;
        GetComponent<Renderer>().sharedMaterial.SetTexture("_ReflectionTex", m_ReflectionTexture);

        if (disablePixelLights)
        {
            QualitySettings.pixelLightCount = oldPixelLightCount;
        }
    }

    void OnDisable()
    {
        if (m_ReflectionTexture)
        {
            DestroyImmediate(m_ReflectionTexture);
            m_ReflectionTexture = null;
        }
        foreach (var kvp in m_ReflectionCameras)
        {
            DestroyImmediate((kvp.Value).gameObject);
        }
        m_ReflectionCameras.Clear();
    }

    private void UpdateCameraModes(Camera src, Camera dest)
    {
        if (dest == null)
        {
            return;
        }

        dest.clearFlags = src.clearFlags;
        dest.backgroundColor = src.backgroundColor;
        if (src.clearFlags == CameraClearFlags.Skybox)
        {
            Skybox sky = src.GetComponent<Skybox>();
            Skybox mysky = dest.GetComponent<Skybox>();

            if (!sky || !sky.material)
            {
                mysky.enabled = false;
            }
            else
            {
                mysky.enabled = true;
                mysky.material = sky.material;
            }
        }

        dest.farClipPlane = src.farClipPlane;
        dest.nearClipPlane = src.nearClipPlane;
        dest.orthographic = src.orthographic;
        dest.fieldOfView = src.fieldOfView;
        dest.aspect = src.aspect;
        dest.orthographicSize = src.orthographicSize;
        dest.renderingPath = src.renderingPath;
    }

    /// <summary>
    /// 按需为摄像机创建物体
    /// </summary>
    /// <param name="currentCamera"></param>
    /// <param name="reflectionCamera"></param>
    private void CreateMirrorObjects(Camera currentCamera, out Camera reflectionCamera)
    {
        reflectionCamera = null;

        //当反射相机的渲染贴图不存在或者大小不满足设定值，销毁当前渲染贴图然后重新创建符合大小号的贴图，并完成初始化
        if (!m_ReflectionTexture || m_OldReflectionTextureSize != textureSize)
        {
            if (m_ReflectionTexture)
            {
                DestroyImmediate(m_ReflectionTexture);
            }
            m_ReflectionTexture = new RenderTexture(textureSize, textureSize, 16);
            m_ReflectionTexture.name = "_MirrorReflection" + GetInstanceID();
            m_ReflectionTexture.isPowerOfTwo = true; //渲染纹理是否是2的乘方大小
            m_ReflectionTexture.hideFlags = HideFlags.DontSave; //该物体不会被保存到场景中。当一个新场景加载的时候也不会被销毁，必须手动DestroyImmediate销毁
            m_OldReflectionTextureSize = textureSize;
        }

        m_ReflectionCameras.TryGetValue(currentCamera, out reflectionCamera);
        if (!reflectionCamera)
        {
            GameObject go = new GameObject("MyMirror Reflection Camera id" + GetInstanceID() + "for " + currentCamera.GetInstanceID(), typeof(Camera), typeof(Skybox));
            reflectionCamera = go.GetComponent<Camera>();
            reflectionCamera.enabled = false;
            reflectionCamera.transform.position = transform.position;
            reflectionCamera.transform.rotation = transform.rotation;
            reflectionCamera.gameObject.AddComponent<FlareLayer>();
            go.hideFlags = HideFlags.HideAndDontSave;
            m_ReflectionCameras[currentCamera] = reflectionCamera;
        }
    }

    private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
    {
        Vector3 offsetPos = pos + normal * clipPlaneOffset;
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(offsetPos);
        Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;

        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }

    static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane)
    {
        reflectionMat.m00 = (1F - 2F * plane[0] * plane[0]);
        reflectionMat.m01 = (-2F * plane[0] * plane[1]);
        reflectionMat.m02 = (-2F * plane[0] * plane[2]);
        reflectionMat.m03 = (-2F * plane[3] * plane[0]);

        reflectionMat.m10 = (-2F * plane[1] * plane[0]);
        reflectionMat.m11 = (1F - 2F * plane[1] * plane[1]);
        reflectionMat.m12 = (-2F * plane[1] * plane[2]);
        reflectionMat.m13 = (-2F * plane[3] * plane[1]);

        reflectionMat.m20 = (-2F * plane[2] * plane[0]);
        reflectionMat.m21 = (-2F * plane[2] * plane[1]);
        reflectionMat.m22 = (1F - 2F * plane[2] * plane[2]);
        reflectionMat.m23 = (-2F * plane[3] * plane[2]);

        reflectionMat.m30 = 0F;
        reflectionMat.m31 = 0F;
        reflectionMat.m32 = 0F;
        reflectionMat.m33 = 1F;
    }

}
