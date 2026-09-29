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

public class RayTracedObject : MonoBehaviour
{
    public static List<RayTracedObject> all = new List<RayTracedObject>();

    public RTMaterial material;   // shows in the Inspector

    public SphereData sphereData;

    void OnEnable()  { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    void Update()
    {
        sphereData = new SphereData(transform.position, transform.lossyScale.x * 0.5f);
    }
}