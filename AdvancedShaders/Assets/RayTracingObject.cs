using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class RayTracingObject : MonoBehaviour
{
    public RayTracingMaster rayTracingMaster;
    private void OnEnable()
    {
        RayTracingMaster.RegisterObject(this);
        rayTracingMaster.transformsToWatch.Add(transform);
    }

    private void OnDisable()
    {
        RayTracingMaster.UnregisterObject(this);
    }
}
