using UnityEngine;
using System.Collections.Generic;

[System.Serializable] 
public struct RTMaterial
{
    [ColorUsage(false, false)] public Color color;
    [ColorUsage(false, true)] public Color emission;
    public float roughness;
    
    public RTMaterial(Color color, Color emission, float roughness)
    {
        this.color = color;
        this.emission = emission;
        this.roughness = roughness;
    }
}

public struct SphereData
{
    public Vector3 center;
    public float radius;

    public SphereData(Vector3 center, float radius)
    {
        this.center = center;
        this.radius = radius;
    }
}

[System.Serializable] 
public struct MeshData
{
    public Vector3[] vertices;
    public int[] triangles;
}

public class RayTracedObject : MonoBehaviour
{
    public static List<RayTracedObject> all = new List<RayTracedObject>();

    public RTMaterial material = new RTMaterial(Color.white, Color.black, 1f);   // shows in the Inspector

    public MeshData meshData;

    void OnEnable()  { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    void Awake()
    {
        Mesh mesh;
        var mf = GetComponent<MeshFilter>();
        if (mf != null)
            mesh = mf.sharedMesh;
        else
        {
            mesh = new Mesh();
            GetComponent<SkinnedMeshRenderer>().BakeMesh(mesh, true);
        }

        meshData.vertices = mesh.vertices;
        for (int i = 0; i < meshData.vertices.Length; i++)
            meshData.vertices[i] = transform.TransformPoint(meshData.vertices[i]);
        meshData.triangles = mesh.triangles;
    }
}