using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{
    public int SphereSeed;

    public ComputeShader rayTracingShader;
    public ComputeBuffer buffer;
    public Texture skyBox;

    private RenderTexture _target, _converged;
    private uint _currentSample = 0;
    private Material _addMaterial;

    public List<Transform> transformsToWatch;

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

    #region Meshes
    private static bool _meshObjectsNeedRebuilding = false;

    private static List<RayTracingObject> _rayTracingObjects = new List<RayTracingObject>();
    private static List<MeshObject> _meshObjects = new List<MeshObject>();
    private static List<Vector3> _vertices = new List<Vector3>();
    private static List<int> _indices = new List<int>();

    private ComputeBuffer _meshObjectBuffer;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _indexBuffer;

    struct MeshObject
    {
        public Matrix4x4 localToWorldMatrix;
        public int indices_offset;
        public int indices_count;
    }
    #endregion

    struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
        public float smoothness;
        public Vector3 emission;
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

        if (_meshObjectBuffer != null)
            _meshObjectBuffer.Release();

        if (_vertexBuffer != null)
            _vertexBuffer.Release();

        if (_indexBuffer != null)
            _indexBuffer.Release();
    }

    private void SetUpScene()
    {
        UnityEngine.Random.InitState(SphereSeed);

        List<Sphere> spheres = new List<Sphere>();

        // Add a number of random spheres
        for (int i = 0; i < maxSpheres - 1; i++)
        {
            Sphere sphere = new Sphere();

            // Radius and radius
            sphere.radius = sphereRadius.x + UnityEngine.Random.value * (sphereRadius.y - sphereRadius.x);
            Vector2 randomPos = UnityEngine.Random.insideUnitCircle * spherePlacementRadius;
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
            Color color = UnityEngine.Random.ColorHSV();
            float chance = UnityEngine.Random.value;
            if (chance < 0.8f)
            {
                bool metal = chance < 0.4f;
                sphere.albedo = metal ? Vector4.zero : new Vector4(color.r, color.g, color.b);
                sphere.specular = metal ? new Vector4(color.r, color.g, color.b) : new Vector4(0.04f, 0.04f, 0.04f);
                sphere.smoothness = UnityEngine.Random.value;
            }
            else
            {
                Color emission = UnityEngine.Random.ColorHSV(0, 1, 0, 1, 3.0f, 8.0f);
                sphere.emission = new Vector3(emission.r, emission.g, emission.b);
            }

            // Add spheres to list
            spheres.Add(sphere);

        SkipSphere:
            continue;
        }

        // Assign to compute buffer
        _sphereBuffer = new ComputeBuffer(spheres.Count, 56);
        _sphereBuffer.SetData(spheres);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RebuildMeshObjectBuffers();
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
        Graphics.Blit(_target, _converged, _addMaterial);
        Graphics.Blit(_converged, destination);

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

        // If render texture doesn't exist or doesn't match the screen render
        if (_converged == null || _converged.width != Screen.width || _converged.height != Screen.height)
        {
            // Release render texture we do have
            if (_converged != null)
                _converged.Release();

            // Get a new render texture to work with
            _converged = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _converged.enableRandomWrite = true;
            _converged.Create();
            _currentSample = 0;
        }
    }

    private void SetShaderParameters()
    {
        rayTracingShader.SetTexture(0, "_SkyboxTexture", skyBox);
        rayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        rayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        rayTracingShader.SetVector("_PixelOffset", new Vector2(UnityEngine.Random.value, UnityEngine.Random.value));
        rayTracingShader.SetFloat("_Seed", UnityEngine.Random.value);

        SetComputeBuffer("_Spheres", _sphereBuffer);
        SetComputeBuffer("_MeshObjects", _meshObjectBuffer);
        SetComputeBuffer("_Vertices", _vertexBuffer);
        SetComputeBuffer("_Indices", _indexBuffer);
    }

    private void Update()
    {
        for (int i = 0; i < transformsToWatch.Count; i++)
        {
            if (transformsToWatch[i].hasChanged)
            {
                _currentSample = 0;
                transformsToWatch[i].hasChanged = false;
                _meshObjectsNeedRebuilding = true;
            }
        }
    }

    public static void RegisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Add(obj);
        _meshObjectsNeedRebuilding = true;
    }

    public static void UnregisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Remove(obj);
        _meshObjectsNeedRebuilding = true;
    }

    private void RebuildMeshObjectBuffers()
    {
        if (!_meshObjectsNeedRebuilding)
            return;

        _meshObjectsNeedRebuilding = false;
        _currentSample = 0;

        // Clear all lists
        _meshObjects.Clear();
        _vertices.Clear();
        _indices.Clear();

        // Loop over all objects and gather their data
        foreach (RayTracingObject obj in _rayTracingObjects)
        {
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;

            // Add vertex data
            int firstVertex = _vertices.Count;
            _vertices.AddRange(mesh.vertices);

            // Add index data - if the vertex buffer wasn't empty before, the indices need to be offset
            int firstIndex = _indices.Count;
            int[] indices = mesh.GetIndices(0);
            _indices.AddRange(indices.Select(index => index + firstVertex));

            // Add the object itself
            _meshObjects.Add(new MeshObject()
            {
                localToWorldMatrix = obj.transform.localToWorldMatrix,
                indices_offset = firstIndex,
                indices_count = indices.Length
            });

            CreateComputeBuffer(ref _meshObjectBuffer, _meshObjects, 72);
            CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
            CreateComputeBuffer(ref _indexBuffer, _indices, 4);
        }
    }

    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
        where T : struct
    {
        // Do we already have a compute buffer?
        if (buffer != null)
        {
            // If no data of buffer doesn't match the given criterea, release it
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }

        if (data.Count != 0)
        {
            // If the buffer has been releaseed or wasn't there to begin with, create it
            if (buffer == null)
            {
                buffer = new ComputeBuffer(data.Count, stride);
            }

            // Set data on the buffer 
            buffer.SetData(data);
        }
    }

    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            rayTracingShader.SetBuffer(0, name, buffer);
        }
    }
}
