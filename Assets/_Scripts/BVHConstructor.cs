using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class BVHConstructor : IDisposable
{
    private readonly ComputeBuffer _sortedMortonCodes;
    private readonly ComputeBuffer _internalNodes; // size = THREADS_PER_BLOCK * BLOCK_SIZE - 1
    private readonly ComputeBuffer _leafNodes; // size = THREADS_PER_BLOCK * BLOCK_SIZE

    private ComputeShader _bvhShader;
    private int _treeConstructionKernel;

    struct InternalNode
    {
        public uint leftNode;
        public uint leftNodeType;
        public uint rightNode;
        public uint rightNodeType;
        public uint parent;
        public uint index;

        public override string ToString()
        {
            string GetType(uint type)
            {
                return type == 0 ? "I" : "L";
            }

            return $"index:{index}, left:{leftNode} {GetType(leftNodeType)}, right:{rightNode} {GetType(rightNodeType)}, parent:{parent}\n";
        }
    };

    struct LeafNode
    {
        public uint parent;
        public uint index;

        public override string ToString()
        {
            return $"index:{index}, parent:{parent}\n";
        }
    };

    public BVHConstructor(IShaderContainer container)
    {
        uint[] array = new uint[]
        {
            1, 2, 4, 5, 19, 24, 25, 30
        };

        InternalNode[] internalNodes = new InternalNode[array.Length - 1];
        for (var i = 0; i < internalNodes.Length; i++)
        {
            internalNodes[i].leftNode = 0xFFFFFFFF;
            internalNodes[i].leftNodeType = 0xFFFFFFFF;
            internalNodes[i].rightNode = 0xFFFFFFFF;
            internalNodes[i].rightNodeType = 0xFFFFFFFF;
            internalNodes[i].parent = 0xFFFFFFFF;
            internalNodes[i].index = 0xFFFFFFFF;
        }

        LeafNode[] leafNodes = new LeafNode[array.Length];
        for (var i = 0; i < leafNodes.Length; i++)
        {
            leafNodes[i].parent = 0xFFFFFFFF;
            leafNodes[i].index = 0xFFFFFFFF;
        }


        _sortedMortonCodes = new ComputeBuffer(Constants.DATA_ARRAY_COUNT, sizeof(uint), ComputeBufferType.Structured);
        _internalNodes = new ComputeBuffer(array.Length - 1, Marshal.SizeOf(typeof(InternalNode)), ComputeBufferType.Structured);
        _leafNodes = new ComputeBuffer(array.Length, Marshal.SizeOf(typeof(LeafNode)), ComputeBufferType.Structured);

        _sortedMortonCodes.SetData(array);
        _internalNodes.SetData(internalNodes);
        _leafNodes.SetData(leafNodes);

        _bvhShader = container.BVH.BVHShader;
        _treeConstructionKernel = _bvhShader.FindKernel("TreeConstructor");

        _bvhShader.SetInt("trianglesCount", array.Length);
        _bvhShader.SetBuffer(_treeConstructionKernel, "sortedMortonCodes", _sortedMortonCodes);
        _bvhShader.SetBuffer(_treeConstructionKernel, "internalNodes", _internalNodes);
        _bvhShader.SetBuffer(_treeConstructionKernel, "leafNodes", _leafNodes);

        _bvhShader.Dispatch(_treeConstructionKernel, Constants.BLOCK_SIZE, 1, 1);

        _internalNodes.GetData(internalNodes);
        _leafNodes.GetData(leafNodes);

        Debug.Log(Utils.ArrayToString(leafNodes));
        Debug.Log(Utils.ArrayToString(internalNodes));
    }

    public void Dispose()
    {
        _sortedMortonCodes.Dispose();
        _internalNodes.Dispose();
        _leafNodes.Dispose();
    }
}