using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Random = UnityEngine.Random;

public class RaytracingMeshDrawer : MonoBehaviour
{
    [SerializeField] [Range(0, 6365)] private int _indexToCheck;
    [SerializeField] private Shader _imageComposer;
    [SerializeField] private Mesh _mesh;
    [SerializeField] private ShaderContainer _shaderContainer;

    private ComputeShader _objectDrawer;
    private int _objectDrawerKernel;

    private MeshBufferContainer _container;
    private ComputeBufferSorter<uint, uint> _sorter;
    private BVHConstructor _bvhConstructor;
    private RenderTexture _renderTexture;
    private Material _imageComposerMaterial;
    private static readonly int ObjectTexture = Shader.PropertyToID("_ObjectTexture");


    void Awake()
    {
        _container = new MeshBufferContainer(_mesh);
        _sorter = new ComputeBufferSorter<uint, uint>(_container.TrianglesLength, _container.Keys, _container.TriangleIndex, _shaderContainer);
        _bvhConstructor = new BVHConstructor(_container.TrianglesLength,
            _container.Keys,
            _container.TriangleIndex,
            _container.TriangleAABB,
            _container.BvhInternalNode,
            _container.BvhLeafNode,
            _container.BvhData,
            _shaderContainer);

        Debug.Log("Triangles Length " + _container.TrianglesLength);

        _sorter.Sort();

        _bvhConstructor.ConstructTree();
        _bvhConstructor.ConstructBVH();

        _container.GetAllGpuData();
        _container.PrintData();

        _renderTexture = new RenderTexture(Screen.width, Screen.height, GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormat.D32_SFloat);
        _renderTexture.enableRandomWrite = true;

        _objectDrawer = _shaderContainer.Raytracing;
        _objectDrawerKernel = _objectDrawer.FindKernel("Raytracing");

        _objectDrawer.SetInt("screenWidth", Screen.width);
        _objectDrawer.SetInt("screenHeight", Screen.height);
        _objectDrawer.SetFloat("cameraFov", Mathf.Tan(Camera.main.fieldOfView * Mathf.Deg2Rad / 2));
        _objectDrawer.SetMatrix("cameraToWorldMatrix", Camera.main.cameraToWorldMatrix);
        _objectDrawer.SetTexture(_objectDrawerKernel, "_texture", _renderTexture);
        _objectDrawer.SetBuffer(_objectDrawerKernel, "sortedTriangleIndices", _container.TriangleIndex);
        _objectDrawer.SetBuffer(_objectDrawerKernel, "triangleAABB", _container.TriangleAABB);
        _objectDrawer.SetBuffer(_objectDrawerKernel, "internalNodes", _container.BvhInternalNode);
        _objectDrawer.SetBuffer(_objectDrawerKernel, "leafNodes", _container.BvhLeafNode);
        _objectDrawer.SetBuffer(_objectDrawerKernel, "bvhData", _container.BvhData);
        _objectDrawer.SetBuffer(_objectDrawerKernel, "triangleData", _container.TriangleData);

        _imageComposerMaterial = new Material(_imageComposer);
        _imageComposerMaterial.SetTexture(ObjectTexture, _renderTexture);
    }

    private void Update()
    {
        _objectDrawer.SetInt("indexToCheck", _indexToCheck);
        _objectDrawer.Dispatch(_objectDrawerKernel, (Screen.width / 32) + 1, (Screen.height / 32) + 1, 1);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(src, dest, _imageComposerMaterial);
    }


    private void DrawAABB(AABB aabb, float random = 1)
    {
        Vector3 center = (aabb.min + aabb.max) / 2;
        Gizmos.DrawWireCube((aabb.min + aabb.max) / 2, (aabb.max - aabb.min) * random);
    }

    private void OnDrawGizmos()
    {
        if (_container == null) return;

        for (int i = 0; i < _container.TrianglesLength; i++)
        {
            AABB aabb = _container.TriangleAABBLocalData[i];
            DrawAABB(aabb);
        }

        Gizmos.color = Color.red;

        for (int i = 0; i < _container.TrianglesLength - 1; i++)
        {
            AABB aabb = _container.BVHLocalData[i];
            DrawAABB(aabb, 1); // Random.Range(1, 1.1f));
        }
        // DrawAABB(_container.BVHLocalData[0], 1.05f); // Random.Range(1, 1.1f));
    }

    private void OnDestroy()
    {
        _sorter.Dispose();
        _container.Dispose();
        _bvhConstructor.Dispose();
    }
}