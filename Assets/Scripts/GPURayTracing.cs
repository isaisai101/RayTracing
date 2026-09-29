using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class GPURayTracing : MonoBehaviour
{
    public struct GPUObject 
    {
        public RTMaterial material;

        public SphereData sphereData;
    }

    private Camera cam;

    [SerializeField] private RawImage rawImage;
    [SerializeField] private ComputeShader shader;

    RenderTexture resultTexture, sumTexture;
    int kernel;

    [SerializeField] private int maxDepth;
    private int frameCount;

    List<GPUObject> gpuObjects = new List<GPUObject>();
    ComputeBuffer objectBuffer;

    void Start()
    {
        cam = GetComponent<Camera>();

        kernel = shader.FindKernel("CSMain");

        resultTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
        resultTexture.enableRandomWrite = true;
        resultTexture.Create();
        sumTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
        sumTexture.enableRandomWrite = true;
        sumTexture.Create();

        shader.SetTexture(kernel, "result", resultTexture);
        shader.SetTexture(kernel, "sum", sumTexture);

        rawImage.enabled = true;
        rawImage.texture = resultTexture;

        shader.SetVector("skyColor", Color.skyBlue * 0.5f);

        shader.SetVector("camPos", cam.transform.position);
        shader.SetVector("camForward", cam.transform.forward);
        shader.SetVector("camRight", cam.transform.right);
        shader.SetVector("camUp", cam.transform.up);

        shader.SetFloat("fov", cam.fieldOfView * Mathf.Deg2Rad);

        shader.SetInt("width", resultTexture.width);
        shader.SetInt("height", resultTexture.height);

        shader.SetInt("maxDepth", maxDepth);
    }

    void LateUpdate()
    {
        frameCount++;
        shader.SetInt("frame", frameCount);

        gpuObjects.Clear();
        foreach (var obj in RayTracedObject.all)
        {
            gpuObjects.Add(new GPUObject {material = obj.material, sphereData = obj.sphereData});
        }
        
        if (objectBuffer == null || objectBuffer.count < gpuObjects.Count)
        {
            objectBuffer?.Release();
            objectBuffer = new ComputeBuffer(Mathf.Max(1, gpuObjects.Count), Marshal.SizeOf<GPUObject>());
            shader.SetBuffer(kernel, "objects", objectBuffer);
        }
        objectBuffer.SetData(gpuObjects);
        shader.SetInt("objectCount", gpuObjects.Count);

        int groupsX = Mathf.CeilToInt(resultTexture.width / 8f);
        int groupsY = Mathf.CeilToInt(resultTexture.height / 8f);
        shader.Dispatch(kernel, groupsX, groupsY, 1);
    }

    void OnDestroy()
    {
        if (resultTexture != null) { resultTexture.Release(); Destroy(resultTexture); }
        if (sumTexture != null)    { sumTexture.Release();    Destroy(sumTexture); }
        objectBuffer?.Release();
    }
}
