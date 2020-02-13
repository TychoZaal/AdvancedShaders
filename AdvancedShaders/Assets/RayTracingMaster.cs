using System.Collections.Generic;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader rayTracingShader;
    public ComputeBuffer buffer;
    public Texture skyBox;

    private RenderTexture _target;
    private uint _currentSample = 0;
    private Material _addMaterial;

    public Light directionalLight;

    #region Filler
    [Tooltip("240 is Screen.width / 8. The higher the number to more details on the X axis")]
    [SerializeField]
    private int xThreads = 240;

    [Tooltip("135 is Screen.height / 8. The higher the number to more details on the Y axis")]
    [SerializeField]
    private int yThreads = 135;
    #endregion

    [SerializeField]
    private Camera _camera;

    public Vector2 sphereRadius = new Vector2(3.0f, 8.0f);
    public uint maxSpheres = 100;
    public float spherePlacementRadius = 100f;
    private ComputeBuffer _sphereBuffer;

    struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
    }

    private void OnEnable()
    {
        _currentSample = 0;
        SetUpScene();
    }

    private void OnDisable()
    {
        if (_sphereBuffer != null)
            _sphereBuffer.Release();
    }

    private void SetUpScene()
    {
        List<Sphere> spheres = new List<Sphere>();

        // Add a number of random spheres
        for (int i = 0; i < maxSpheres - 1; i++)
        {
            Sphere sphere = new Sphere();

            // Radius and radius
            sphere.radius = sphereRadius.x + Random.value * (sphereRadius.y - sphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * spherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);

            foreach (Sphere other in spheres)
            {
                float minDistance = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDistance * minDistance)
                {
                    goto SkipSphere;
                }
            }

            // Albedo and specular color
            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.5f;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;

            // Add spheres to list
            spheres.Add(sphere);

        SkipSphere:
            continue;
        }

        // Assign to compute buffer
        _sphereBuffer = new ComputeBuffer(spheres.Count, 40);
        _sphereBuffer.SetData(spheres);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        // Ensures there is a render texture with the proper resolution
        InitRenderTexture();

        // Set the target and dispatch the compute shader
        rayTracingShader.SetTexture(0, "Result", _target);

        rayTracingShader.Dispatch(0, xThreads, yThreads, 1);

        if (_addMaterial == null)
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));

        _addMaterial.SetFloat("_Sample", _currentSample);

        // Apply new texture as post processing overlay
        Graphics.Blit(_target, destination, _addMaterial);

        _currentSample++;
    }

    // Ensures there is a render texture with the proper resolution
    private void InitRenderTexture()
    {
        // If render texture doesn't exist or doesn't match the screen render
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            // Release render texture we do have
            if (_target != null)
                _target.Release();

            // Get a new render texture to work with
            _target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
            _currentSample = 0;
        }
    }

    private void SetShaderParameters()
    {
        rayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        rayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        rayTracingShader.SetTexture(0, "_SkyboxTexture", skyBox);
        rayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        Vector3 l = directionalLight.transform.forward;
        rayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, directionalLight.intensity));
        rayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
    }

    private void Update()
    {
        if (transform.hasChanged || directionalLight.transform.hasChanged)
        {
            _currentSample = 0;
            transform.hasChanged = false;
            directionalLight.transform.hasChanged = false;
        }
        rayTracingShader.SetFloat("_Time", Time.time);
    }
}
