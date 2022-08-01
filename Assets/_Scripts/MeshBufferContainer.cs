using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct AABB
{
    public Vector4 min;
    public Vector4 max;
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct Triangle
{
    public Vector4 a;
    public Vector4 b;
    public Vector4 c;

    // TODO uv, texture index, etc
}

public class MeshBufferContainer : IDisposable
{
    public ComputeBuffer Keys => _keysBuffer;
    public ComputeBuffer Data => _dataBuffer;

    private static uint ExpandBits(uint v)
    {
        v = (v * 0x00010001u) & 0xFF0000FFu;
        v = (v * 0x00000101u) & 0x0F00F00Fu;
        v = (v * 0x00000011u) & 0xC30C30C3u;
        v = (v * 0x00000005u) & 0x49249249u;
        return v;
    }

    private static uint Morton3D(float x, float y, float z)
    {
        x = Math.Min(Math.Max(x * 1024.0f, 0.0f), 1023.0f);
        y = Math.Min(Math.Max(y * 1024.0f, 0.0f), 1023.0f);
        z = Math.Min(Math.Max(z * 1024.0f, 0.0f), 1023.0f);
        uint xx = ExpandBits((uint)x);
        uint yy = ExpandBits((uint)y);
        uint zz = ExpandBits((uint)z);
        return xx * 4 + yy * 2 + zz;
    }

    private readonly ComputeBuffer _keysBuffer;
    private readonly ComputeBuffer _dataBuffer;

    public MeshBufferContainer(Mesh mesh) // TODO multiple meshes
    {
        Debug.Log(Marshal.SizeOf(typeof(Triangle)));
        _keysBuffer = new ComputeBuffer(Constants.DATA_ARRAY_COUNT, sizeof(uint), ComputeBufferType.Structured);
        _dataBuffer = new ComputeBuffer(Constants.DATA_ARRAY_COUNT, Marshal.SizeOf(typeof(Triangle)), ComputeBufferType.Structured);

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        int trianglesLength = triangles.Length / 3;

        for (var i = 0; i < triangles.Length; i += 3)
        {
        }
    }


    public void Dispose()
    {
        _keysBuffer.Dispose();
        _dataBuffer.Dispose();
    }
}