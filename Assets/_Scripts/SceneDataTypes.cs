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
    float _dummy0;
    public Vector3 b;
    float _dummy1;
    public Vector3 c;
    float _dummy2;
    public Vector2 a_uv;
    public Vector2 b_uv;
    public Vector2 c_uv;
    Vector2 _dummy3;

    public Vector3 a_normal;
    float _dummy4;
    public Vector3 b_normal;
    float _dummy5;
    public Vector3 c_normal;
    float _dummy6;
    
    
    // TODO texture index, etc
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct InternalNode
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
    
    public static InternalNode NullLeaf = new InternalNode()
    {
        leftNode = 0xFFFFFFFF,
        leftNodeType = 0xFFFFFFFF,
        rightNode = 0xFFFFFFFF,
        rightNodeType = 0xFFFFFFFF,
        parent = 0xFFFFFFFF,
        index = 0xFFFFFFFF
    };
};

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct LeafNode
{
    public uint parent;
    public uint index;

    public override string ToString()
    {
        return $"index:{index}, parent:{parent}\n";
    }

    public static LeafNode NullLeaf = new LeafNode()
    {
        parent = 0xFFFFFFFF,
        index = 0xFFFFFFFF
    };
};