using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;

public static class VecExt
{
    public static float Volume(this Vector3 v) => v.x * v.y * v.z;
    public static float Area(this Vector3 v) => 2f * (v.x * v.y + v.y * v.z + v.z * v.x);
}

public class GPURayTracing : MonoBehaviour
{

    const float INF = 1e30f;
    public struct GPUMesh
    {
        public int root;
    }
    public struct BVHNode
    {
        public Vector3 boxMin, boxMax;
        public int left;
        public int triCount;
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

    [SerializeField] private int maxBounce = 15;
    private int frameCount;

    List<GPUObject> gpuObjects = new List<GPUObject>();
    ComputeBuffer objectBuffer;

    List<Vector3> vertices = new List<Vector3>();
    ComputeBuffer vertexBuffer;
    List<int> triangles = new List<int>();
    ComputeBuffer triangleBuffer;

    List<BVHNode> bvhNodes = new List<BVHNode>();
    ComputeBuffer bvhNodesBuffer;

    public int maxBVHDepth = 32;
    public float C_Trav = 1;

    BVHNode BVH(List<int> tris, int depth = 0)
    {   
        BVHNode node = new BVHNode();

        int size = tris.Count / 3;
        int[] triIdx = new int[size];
        for (int i = 0; i < size; i++) triIdx[i] = i;

        float[][] centroid = new float[size][];
        for (int i = 0; i < size; i++)
            centroid[i] = new float[3];
        for (int axis = 0; axis < 3; axis++)
        {
            for (int i = 0; i < size; i++) {
                Vector3 c = (vertices[tris[i * 3]] + vertices[tris[i * 3 + 1]] + vertices[tris[i * 3 + 2]]) / 3;
                if (axis == 0) centroid[i][axis] = c.x;
                if (axis == 1) centroid[i][axis] = c.y;
                if (axis == 2) centroid[i][axis] = c.z;
            }
        }

        node.boxMin = new Vector3(INF, INF, INF); node.boxMax = new Vector3(-INF, -INF, -INF);
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                node.boxMin = Vector3.Min(node.boxMin, vertices[tris[triIdx[i] * 3 + j]]);
                node.boxMax = Vector3.Max(node.boxMax, vertices[tris[triIdx[i] * 3 + j]]);
            }
        }

        float parentArea = (node.boxMax - node.boxMin).Area();

        float val = parentArea * size;
        int optAxis = 0, optPos = size - 1;
        for (int axis = 0; axis < 3; axis++)
        {
            Array.Sort(triIdx, (a, b) => centroid[a][axis].CompareTo(centroid[b][axis]));
            float[] pref = new float[size], suf = new float[size];

            Vector3 boxMin = new Vector3(INF, INF, INF), boxMax = new Vector3(-INF, -INF, -INF);
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    boxMin = Vector3.Min(boxMin, vertices[tris[triIdx[i] * 3 + j]]);
                    boxMax = Vector3.Max(boxMax, vertices[tris[triIdx[i] * 3 + j]]);
                }
                pref[i] = (boxMax - boxMin).Area() * (i + 1);
            }
            boxMin = new Vector3(INF, INF, INF); boxMax = new Vector3(-INF, -INF, -INF);
            for (int i = size - 1; i >= 0; i--)
            {
                for (int j = 0; j < 3; j++)
                {
                    boxMin = Vector3.Min(boxMin, vertices[tris[triIdx[i] * 3 + j]]);
                    boxMax = Vector3.Max(boxMax, vertices[tris[triIdx[i] * 3 + j]]);
                }
                suf[i] = (boxMax - boxMin).Area() * (size - i);
            }

            for (int i = 0; i < size - 1; i++)
            {
                if (pref[i] + suf[i + 1] + C_Trav * parentArea < val)
                {
                    val = pref[i] + suf[i + 1];
                    optAxis = axis;
                    optPos = i;
                }
            }
        }

        if (optPos == size - 1 || depth + 1 == maxBVHDepth)
        {
            node.triCount = size;
            node.left = triangles.Count / 3;
            for (int i = 0; i < size * 3; i++)
            {
                triangles.Add(tris[i]);
            }
            return node;
        }

        List<int> L = new List<int>(), R = new List<int>();
        Array.Sort(triIdx, (a, b) => centroid[a][optAxis].CompareTo(centroid[b][optAxis]));
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                if (i <= optPos) L.Add(tris[triIdx[i] * 3 + j]);
                else R.Add(tris[triIdx[i] * 3 + j]);
            }
        }

        node.triCount = 0;

        BVHNode left = BVH(L, depth + 1), right = BVH(R, depth + 1);
        node.left = bvhNodes.Count;
        bvhNodes.Add(left);
        bvhNodes.Add(right);

        return node;
    }

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

        shader.SetInt("maxBounce", maxBounce);


        foreach (var obj in RayTracedObject.all)
        {
            int firstVertex = vertices.Count, firstTriangle = triangles.Count;
            foreach (var vertex in obj.meshData.vertices)
            {
                vertices.Add(vertex);
            }
            List<int> tris = new List<int>();
            foreach (var tri in obj.meshData.triangles)
            {
                tris.Add(firstVertex + tri);
            }

            bvhNodes.Add(BVH(tris));

            gpuObjects.Add(new GPUObject
            {
                material = obj.material,
                mesh = new GPUMesh{root = bvhNodes.Count - 1}
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

        bvhNodesBuffer = new ComputeBuffer(Mathf.Max(1, bvhNodes.Count), Marshal.SizeOf<BVHNode>());
        shader.SetBuffer(kernel, "nodes", bvhNodesBuffer);
        bvhNodesBuffer.SetData(bvhNodes);
        
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
        bvhNodesBuffer?.Release();
    }
}
