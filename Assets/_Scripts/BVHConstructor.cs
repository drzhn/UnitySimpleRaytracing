using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class BVHConstructor : IDisposable
{
    private readonly ComputeBuffer _sortedMortonCodes;
    private readonly ComputeBuffer _sortedTriangleIndices;
    private readonly ComputeBuffer _triangleAABB;

    private readonly ComputeBuffer _internalNodes; // size = THREADS_PER_BLOCK * BLOCK_SIZE - 1
    private readonly ComputeBuffer _leafNodes; // size = THREADS_PER_BLOCK * BLOCK_SIZE
    private readonly ComputeBuffer _bvhData; // size = THREADS_PER_BLOCK * BLOCK_SIZE
    private readonly DataBuffer<uint> _atomics; // size = THREADS_PER_BLOCK * BLOCK_SIZE

    private readonly ComputeShader _bvhShader;
    private readonly int _treeConstructionKernel;
    private readonly int _bvhConstructionKernel;

    private readonly int _trianglesCount;

    public BVHConstructor(
        int trianglesCount,
        ComputeBuffer sortedMortonCodes,
        ComputeBuffer sortedTriangleIndices,
        ComputeBuffer triangleAABB,
        ComputeBuffer internalNodes,
        ComputeBuffer leafNodes,
        ComputeBuffer BVHData,
        IShaderContainer container)
    {
        _sortedMortonCodes = sortedMortonCodes;
        _sortedTriangleIndices = sortedTriangleIndices;
        _triangleAABB = triangleAABB;

        _internalNodes = internalNodes;
        _leafNodes = leafNodes;
        _bvhData = BVHData;
        _atomics = new DataBuffer<uint>(Constants.DATA_ARRAY_COUNT, 0);
        _trianglesCount = trianglesCount;

        _bvhShader = container.BVH.BVHShader;
        _treeConstructionKernel = _bvhShader.FindKernel("TreeConstructor");
        _bvhConstructionKernel = _bvhShader.FindKernel("BVHConstructor");

        _bvhShader.SetInt("trianglesCount", trianglesCount);
        _bvhShader.SetBuffer(_treeConstructionKernel, "sortedMortonCodes", _sortedMortonCodes);
        _bvhShader.SetBuffer(_treeConstructionKernel, "internalNodes", _internalNodes);
        _bvhShader.SetBuffer(_treeConstructionKernel, "leafNodes", _leafNodes);

        _bvhShader.SetBuffer(_bvhConstructionKernel, "internalNodes", _internalNodes);
        _bvhShader.SetBuffer(_bvhConstructionKernel, "leafNodes", _leafNodes);
        _bvhShader.SetBuffer(_bvhConstructionKernel, "triangleAABB", _triangleAABB);
        _bvhShader.SetBuffer(_bvhConstructionKernel, "sortedTriangleIndices", _sortedTriangleIndices);
        _bvhShader.SetBuffer(_bvhConstructionKernel, "atomicsData", _atomics.DeviceBuffer);
        _bvhShader.SetBuffer(_bvhConstructionKernel, "BVHData", _bvhData);


        // _internalNodes.GetData(internalNodes);
        // _leafNodes.GetData(leafNodes);
        //
        // Debug.Log(Utils.ArrayToString(leafNodes));
        // Debug.Log(Utils.ArrayToString(internalNodes));
    }

    public void ConstructTree()
    {
        _bvhShader.Dispatch(_treeConstructionKernel, Constants.BLOCK_SIZE, 1, 1);
        
        uint[] sortedTriangleIndices = new uint[_trianglesCount];
        _sortedTriangleIndices.GetData(sortedTriangleIndices);
        Debug.Log(Utils.ArrayToString(sortedTriangleIndices));

        InternalNode[] internalNodes = new InternalNode[_trianglesCount - 1];
        _internalNodes.GetData(internalNodes);
        Debug.Log(Utils.ArrayToString(internalNodes));

        LeafNode[] leafNodes = new LeafNode[_trianglesCount];
        _leafNodes.GetData(leafNodes);
        Debug.Log(Utils.ArrayToString(leafNodes));
    }

    public void ConstructBVH()
    {
        _bvhShader.Dispatch(_bvhConstructionKernel, Constants.BLOCK_SIZE, 1, 1);
        
        AABB[] aabbs = new AABB[_trianglesCount - 1];
        _bvhData.GetData(aabbs);
        Debug.Log(Utils.ArrayToString(aabbs));
        
        _atomics.GetData();
        Debug.Log(Utils.ArrayToString(_atomics.LocalBuffer));
    }

    public void Dispose()
    {
        _atomics.Dispose();
    }
}