using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Rendering.LookDev;

public class CPURayTracing : MonoBehaviour
{
    [SerializeField] int width;
    int height;
    private Camera cam;
    [SerializeField] private RawImage rawImage;

    private Texture2D texture;
    private Color[] pixels;
    private Color[,] sum;
    private int frameCount = 0;

    [SerializeField] private float maxDepth;

    void ColorPixel(int x, int y, Color color)
    {
        pixels[y * width + x] = color;
    }

    private List<RayTracedObject> allObjects = RayTracedObject.all;

    private struct HitInfo
    {
        public float distance;
        public Vector3 point;
        public Vector3 normal;
        public RTMaterial material;

        public static readonly HitInfo None = new HitInfo(float.PositiveInfinity, new Vector3(0, 0), new Vector3(0, 0), new RTMaterial(Color.black, Color.black, 1));

        public HitInfo(float distance, Vector3 point, Vector3 normal, RTMaterial material)
        {
            this.distance = distance;
            this.point = point;
            this.normal = normal;
            this.material = material;
        }
    }

    float IntersectSphere(Ray ray, SphereData sphere)
    {
        Vector3 oc = ray.origin - sphere.center;
        float b = Vector3.Dot(oc, ray.direction);
        float c = Vector3.Dot(oc, oc) - sphere.radius * sphere.radius;
        float disc = b * b - c;

        if (disc < 0) return float.PositiveInfinity;

        float s = Mathf.Sqrt(disc);

        float t = -b - s;
        if (t > 0.001f) return t;

        t = -b + s;
        if (t > 0.001f) return t;

        return float.PositiveInfinity;
    }

    Ray RandomRay(Vector3 hitPoint, Vector3 normal)
    {
        return new Ray(hitPoint, (normal + Random.onUnitSphere).normalized);
    }

    public RTMaterial sky;

    Color TraceRay(Ray ray, Color rayColor, float depth = 0)
    {
        if (depth > maxDepth || rayColor == Color.black)
            return Color.black;
        
        HitInfo closestHit = HitInfo.None;
        foreach (RayTracedObject obj in allObjects)
        {   
            float dist = IntersectSphere(ray, obj.sphereData);
            Vector3 hitPoint = ray.GetPoint(dist);
            if (dist < closestHit.distance)
            {
                closestHit = new HitInfo(dist, hitPoint, (hitPoint - obj.sphereData.center).normalized, obj.material);
            }
        }

        if (float.IsInfinity(closestHit.distance)) {
            return sky.emission * rayColor;
        }
        return closestHit.material.emission * rayColor + TraceRay(RandomRay(closestHit.point, closestHit.normal), rayColor * closestHit.material.color, depth + 1);
    }

    Ray GetCameraRay(int x, int y)
    {
        Vector3 origin = transform.position;
        float fov = cam.fieldOfView * Mathf.Deg2Rad; // in radians
        float ratio = (float)width / height;

        float u = (x + 0.5f) / width * 2 - 1, v = (y + 0.5f) / height * 2 - 1;
        float halfHeight = Mathf.Tan(fov / 2), halfWidth = halfHeight * ratio;

        Vector3 direction = transform.forward + transform.right * u * halfWidth + transform.up * v * halfHeight;

        return new Ray(origin, direction);
    }
    
    void Start()
    {
        height = width * Screen.height / Screen.width;

        sum = new Color[width, height];

        cam = GetComponent<Camera>();

        texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        rawImage.texture = texture;
        rawImage.enabled = true;

        pixels = new Color[width * height];
    }

    void LateUpdate()
    {
        frameCount++;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color col = Color.black;
                int cnt = 8;
                for (int i = 1; i <= cnt; i++)
                {
                    col += TraceRay(GetCameraRay(x, y), Color.white);
                }

                sum[x, y] += col / cnt;
                ColorPixel(x, y, (sum[x, y] / frameCount).gamma);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
    }
}
