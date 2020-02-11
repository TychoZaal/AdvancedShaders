using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader rayTracingShader;
    public ComputeBuffer buffer;
    public Texture skyBox;

    private RenderTexture _target;

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

        buffer = new ComputeBuffer(48, 48);
        int[] cols = new int[12];
        buffer.SetData(cols);
        rayTracingShader.SetBuffer(rayTracingShader.FindKernel("CSMain"), "colors", buffer);

        rayTracingShader.Dispatch(0, xThreads, yThreads, 1);
        
        buffer.GetData(cols);

        for (int i = 0; i < cols.Length - 1; i++)
        {
            Debug.Log(cols[i]);
        }

        // Apply new texture as post processing overlay
        Graphics.Blit(_target, destination);
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
        }
    }

    private void SetShaderParameters()
    {
        rayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        rayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        rayTracingShader.SetTexture(0, "_SkyboxTexture", skyBox);
    }
}
