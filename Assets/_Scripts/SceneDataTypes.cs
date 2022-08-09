using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct AABB
{
    public Vector3 min;
    public float _dummy0;
    public Vector3 max;
    public float _dummy1;
    
    public override string ToString()
    {
        return $"min:{min}, max:{max}";
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct Triangle
{
    public Vector3 a;
    public float _dummy0;
    public Vector3 b;
    public float _dummy1;
    public Vector3 c;
    public float _dummy2;

    // TODO uv, texture index, etc
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
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
        string GetNodeType(uint type)
        {
            return type == 0 ? "I" : "L";
        }

        return $"index:{index}, left:{leftNode} {GetNodeType(leftNodeType)}, right:{rightNode} {GetNodeType(rightNodeType)}, parent:{parent}\n";
    }
};

[StructLayout(LayoutKind.Sequential, Pack = 16)]
struct LeafNode
{
    public uint parent;
    public uint index;

    public override string ToString()
    {
        return $"index:{index}, parent:{parent}\n";
    }
};