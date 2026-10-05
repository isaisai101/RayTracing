using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class GPURayTracing : MonoBehaviour
{

    const float INF = 1e30f;
    public struct GPUMesh
    {
        public int firstVertex;
        public int vertexCount;
        public int firstTriangle;
        public int triangleCount;
        public Vector3 boundsMin;
        public Vector3 boundsMax;
    }
    public struct GPUObject 
    {
        public RTMaterial material;
        public GPUMesh mesh;
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

    List<Vector3> vertices = new List<Vector3>();
    ComputeBuffer vertexBuffer;
    List<int> triangles = new List<int>();
    ComputeBuffer triangleBuffer;

    void Start()
    {
        cam = GetComponent<Camera>();

        kernel = shader.FindKernel("CSMain");

        resultTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
        resultTexture.enableRandomWrite = true;
        resultTexture.filterMode = FilterMode.Point;
        resultTexture.Create();
        sumTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
        sumTexture.enableRandomWrite = true;
        sumTexture.filterMode = FilterMode.Point;
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


        foreach (var obj in RayTracedObject.all)
        {
            int firstVertex = vertices.Count, firstTriangle = triangles.Count;
            Vector3 boundsMin = new Vector3(INF, INF, INF), boundsMax = new Vector3(-INF, -INF, -INF);
            foreach (var vertex in obj.meshData.vertices)
            {
                vertices.Add(vertex);   

                boundsMin = Vector3.Min(boundsMin, vertex);
                boundsMax = Vector3.Max(boundsMax, vertex);
            }
            foreach (var tri in obj.meshData.triangles)
            {
                triangles.Add(tri);   
            }

            gpuObjects.Add(new GPUObject 
            {
                material = obj.material,
                mesh = new GPUMesh
                {
                    firstVertex = firstVertex, 
                    vertexCount = obj.meshData.vertices.Length, 
                    firstTriangle = firstTriangle / 3,
                    triangleCount = obj.meshData.triangles.Length / 3,
                    boundsMin = boundsMin,
                    boundsMax = boundsMax
                }
            });
        }
    
        objectBuffer = new ComputeBuffer(Mathf.Max(1, gpuObjects.Count), Marshal.SizeOf<GPUObject>());
        shader.SetBuffer(kernel, "objects", objectBuffer);
        objectBuffer.SetData(gpuObjects);
        shader.SetInt("objectCount", gpuObjects.Count);

        vertexBuffer = new ComputeBuffer(Mathf.Max(1, vertices.Count), Marshal.SizeOf<Vector3>());
        shader.SetBuffer(kernel, "vertices", vertexBuffer);
        vertexBuffer.SetData(vertices);
        shader.SetInt("vertexCount", vertices.Count);

        triangleBuffer = new ComputeBuffer(Mathf.Max(1, triangles.Count), Marshal.SizeOf<int>());
        shader.SetBuffer(kernel, "triangles", triangleBuffer);
        triangleBuffer.SetData(triangles);
        shader.SetInt("triangleCount", triangles.Count / 3);
        
    }

    void LateUpdate()
    {
        frameCount++;
        shader.SetInt("frame", frameCount);

        int groupsX = Mathf.CeilToInt(resultTexture.width / 8f);
        int groupsY = Mathf.CeilToInt(resultTexture.height / 8f);
        shader.Dispatch(kernel, groupsX, groupsY, 1);
    }

    void OnDestroy()
    {
        if (resultTexture != null) { resultTexture.Release(); Destroy(resultTexture); }
        if (sumTexture != null)    { sumTexture.Release();    Destroy(sumTexture); }
        objectBuffer?.Release();
        vertexBuffer?.Release();
        triangleBuffer?.Release();
    }
}
