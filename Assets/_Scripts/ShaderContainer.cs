using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IShaderContainer
{
    public SortingShaderContainer Sorting { get; }
    public BVHShaderContainer BVH { get; }
}

[Serializable]
public class SortingShaderContainer
{
    public ComputeShader LocalRadixSortShader => _localRadixSortShader;
    public ComputeShader ScanShader => _scanShader;
    public ComputeShader GlobalRadixSortShader => _globalRadixSortShader;

    [SerializeField] private ComputeShader _localRadixSortShader;
    [SerializeField] private ComputeShader _scanShader;
    [SerializeField] private ComputeShader _globalRadixSortShader;
}

[Serializable]
public class BVHShaderContainer
{
    public ComputeShader BVHShader => _BVHShader;
    [SerializeField] private ComputeShader _BVHShader;
}

[Serializable]
public class ShaderContainer : MonoBehaviour, IShaderContainer
{
    public SortingShaderContainer Sorting => _sorting;
    public BVHShaderContainer BVH => _bvh;

    [SerializeField] private SortingShaderContainer _sorting;
    [SerializeField] private BVHShaderContainer _bvh;
}