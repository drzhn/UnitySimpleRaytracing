using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct AABB
{
    public Vector3 min;
    public float _dummy0;
    public Vector3 max;
    public float _dummy1;
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

public class MeshBufferContainer : IDisposable
{
    // TODO reduce scene data for finding AABB scene in runtime
    private static readonly AABB Whole = new AABB()
    {
        min = new Vector3(-3, -3, -1),
        max = new Vector3(3, 3, 1)
    };

    public ComputeBuffer Keys => _keysBuffer;
    public ComputeBuffer TriangleIndex => _triangleIndexBuffer;
    public ComputeBuffer TriangleData => _triangleDataBuffer;
    public int TrianglesLength => _trianglesLength;

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

    private static void GetCentroidAndAABB(Vector3 a, Vector3 b, Vector3 c, out Vector3 centroid, out AABB aabb)
    {
        Vector3 min = new Vector3(
            Math.Min(Math.Min(a.x, b.x), c.x),
            Math.Min(Math.Min(a.y, b.y), c.y),
            Math.Min(Math.Min(a.z, b.z), c.z)
        );
        Vector3 max = new Vector3(
            Math.Max(Math.Max(a.x, b.x), c.x),
            Math.Max(Math.Max(a.y, b.y), c.y),
            Math.Max(Math.Max(a.z, b.z), c.z)
        );

        centroid = (min + max) * 0.5f;
        aabb = new AABB
        {
            min = min,
            max = max
        };
    }

    private static Vector3 NormalizeCentroid(Vector3 centroid)
    {
        Vector3 ret = centroid;
        ret.x -= Whole.min.x;
        ret.y -= Whole.min.y;
        ret.z -= Whole.min.z;
        ret.x /= (Whole.max.x - Whole.min.x);
        ret.y /= (Whole.max.y - Whole.min.y);
        ret.z /= (Whole.max.z - Whole.min.z);
        return ret;
    }

    private readonly int _trianglesLength;

    private readonly ComputeBuffer _keysBuffer;
    private readonly ComputeBuffer _triangleIndexBuffer;
    private readonly ComputeBuffer _triangleDataBuffer;
    private readonly ComputeBuffer _aabbDataBuffer;

    private readonly uint[] _keysLocalData = new uint[Constants.DATA_ARRAY_COUNT];
    private readonly uint[] _triangleIndexLocalData = new uint[Constants.DATA_ARRAY_COUNT];
    private readonly AABB[] _aabbLocalData = new AABB[Constants.DATA_ARRAY_COUNT];
    private readonly Triangle[] _triangleDataLocalData = new Triangle[Constants.DATA_ARRAY_COUNT];

    public MeshBufferContainer(Mesh mesh) // TODO multiple meshes
    {
        if (Marshal.SizeOf(typeof(Triangle)) != 48)
        {
            Debug.LogError("Triangle struct size = " + Marshal.SizeOf(typeof(Triangle)) + ", not 48");
        }

        if (Marshal.SizeOf(typeof(AABB)) != 32)
        {
            Debug.LogError("AABB struct size = " + Marshal.SizeOf(typeof(AABB)) + ", not 32");
        }

        _keysBuffer = new ComputeBuffer(Constants.DATA_ARRAY_COUNT, sizeof(uint), ComputeBufferType.Structured);
        _triangleIndexBuffer = new ComputeBuffer(Constants.DATA_ARRAY_COUNT, sizeof(uint), ComputeBufferType.Structured);
        _triangleDataBuffer = new ComputeBuffer(Constants.DATA_ARRAY_COUNT, Marshal.SizeOf(typeof(Triangle)), ComputeBufferType.Structured);
        _aabbDataBuffer = new ComputeBuffer(Constants.DATA_ARRAY_COUNT, Marshal.SizeOf(typeof(AABB)), ComputeBufferType.Structured);

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        _trianglesLength = triangles.Length / 3;

        for (uint i = 0; i < _trianglesLength; i++)
        {
            Vector3 a = vertices[triangles[i * 3 + 0]];
            Vector3 b = vertices[triangles[i * 3 + 1]];
            Vector3 c = vertices[triangles[i * 3 + 2]];
            GetCentroidAndAABB(a, b, c, out var centroid, out var aabb);
            centroid = NormalizeCentroid(centroid);
            uint mortonCode = Morton3D(centroid.x, centroid.y, centroid.z);
            _keysLocalData[i] = mortonCode;
            _triangleIndexLocalData[i] = i;
            _triangleDataLocalData[i] = new Triangle
            {
                a = a,
                b = b,
                c = c
            };
            _aabbLocalData[i] = aabb;
        }

        for (var i = _trianglesLength; i < Constants.DATA_ARRAY_COUNT; i++)
        {
            _keysLocalData[i] = uint.MaxValue;
            _triangleIndexLocalData[i] = uint.MaxValue;
        }

        _keysBuffer.SetData(_keysLocalData);
        _triangleIndexBuffer.SetData(_triangleIndexLocalData);
        _triangleDataBuffer.SetData(_triangleDataLocalData);
        _aabbDataBuffer.SetData(_aabbLocalData);
    }


    public void Dispose()
    {
        _keysBuffer.Dispose();
        _triangleIndexBuffer.Dispose();
        _triangleDataBuffer.Dispose();
        _aabbDataBuffer.Dispose();
    }
}