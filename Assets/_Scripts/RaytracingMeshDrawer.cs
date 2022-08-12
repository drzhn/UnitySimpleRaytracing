using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Random = UnityEngine.Random;

public class RaytracingMeshDrawer : MonoBehaviour
{
    [SerializeField] [Range(0,6365)] private int _indexToCheck;
    [SerializeField] private Shader _imageComposer;
    [SerializeField] private Mesh _mesh;
    [SerializeField] private ShaderContainer _shaderContainer;

    private ComputeShader _objectDrawer;
    private int _objectDrawerKernel;

    private MeshBufferContainer _container;
    private ComputeBufferSorter _sorter;
    private BVHConstructor _bvhConstructor;
    private RenderTexture _renderTexture;
    private Material _imageComposerMaterial;
    private static readonly int ObjectTexture = Shader.PropertyToID("_ObjectTexture");


    void Awake()
    {
        _container = new MeshBufferContainer(_mesh);
        _sorter = new ComputeBufferSorter(_container.Keys, _container.TriangleIndex, _shaderContainer);
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


        // Vector3 dir = -Vector3.forward;
        // TestRecursion(new Ray()
        // {
        //     origin = Vector3.forward * 10,
        //     dir = dir,
        //     inv_dir = new Vector3(1 / dir.x, 1 / dir.y, 1 / dir.z)
        // });
    }

    private struct Ray
    {
        public Vector3 origin;
        public Vector3 dir;
        public Vector3 inv_dir;
    }

    bool RayBoxIntersection(AABB b, Ray r)
    {
        Vector3 t1 = Vector3.Scale(b.min - r.origin, r.inv_dir);
        Vector3 t2 = Vector3.Scale(b.max - r.origin, r.inv_dir);

        Vector3 tmin1 = Vector3.Min(t1, t2);
        Vector3 tmax1 = Vector3.Max(t1, t2);

        float tmin = Mathf.Max(tmin1.x, tmin1.y, tmin1.z);
        float tmax = Mathf.Min(tmax1.x, tmax1.y, tmax1.z);

        return tmax > tmin;
    }

    private void TestRecursion(Ray ray)
    {
        uint[] stack = new uint[64];
        uint currentStackIndex = 0;
        stack[currentStackIndex] = 0;
        currentStackIndex = 1;

        int count = 0;
        while (currentStackIndex != 0)
        {
            count++;
            if (count == 100)
            {
                Debug.LogError("break after infinite cycle");
                break;
            }

            currentStackIndex--;
            uint index = stack[currentStackIndex];

            if (!RayBoxIntersection(_container.BVHLocalData[index], ray))
            {
                continue;
            }

            uint leftIndex = _container.BvhInternalNodeLocalData[index].leftNode;
            uint leftType = _container.BvhInternalNodeLocalData[index].leftNodeType;

            if (leftType == 0)
            {
                Debug.Log("Internal " + leftIndex);
                stack[currentStackIndex] = leftIndex;
                currentStackIndex++;
            }
            else
            {
                Debug.Log("leaf " + leftIndex);
            }

            uint rightIndex = _container.BvhInternalNodeLocalData[index].rightNode;
            uint rightType = _container.BvhInternalNodeLocalData[index].rightNodeType;


            if (rightType == 0)
            {
                Debug.Log("Internal " + rightIndex);
                stack[currentStackIndex] = rightIndex;
                currentStackIndex++;
            }
            else
            {
                Debug.Log("leaf " + rightIndex);
            }
        }
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
        
        // for (int i = 0; i < _container.TrianglesLength - 1; i++)
        // {
        //     AABB aabb = _container.BVHLocalData[i];
        //     DrawAABB(aabb, 1.05f); // Random.Range(1, 1.1f));
        // }
        DrawAABB(_container.BVHLocalData[0], 1.05f); // Random.Range(1, 1.1f));
    }

    private void OnDestroy()
    {
        _sorter.Dispose();
        _container.Dispose();
        _bvhConstructor.Dispose();
    }
}