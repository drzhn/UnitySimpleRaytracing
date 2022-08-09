using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class DataBuffer<T> : IDisposable where T : struct
{
    public ComputeBuffer DeviceBuffer => _deviceBuffer;
    public T[] LocalBuffer => _localBuffer;

    private readonly ComputeBuffer _deviceBuffer;
    private readonly T[] _localBuffer;
    private bool _synced;

    public DataBuffer(int size, T initialValue) : this(size)
    {
        for (int i = 0; i < size; i++)
        {
            _localBuffer[i] = initialValue;
        }

        _deviceBuffer.SetData(_localBuffer);
        _synced = true;
    }

    public DataBuffer(int size)
    {
        _deviceBuffer = new ComputeBuffer(size, Marshal.SizeOf(typeof(T)), ComputeBufferType.Structured);
        _localBuffer = new T[size];
        _synced = false;
    }

    public T this[uint i]
    {
        get
        {
            if (!_synced)
            {
                GetData();
            }

            return _localBuffer[i];
        }
        set
        {
            _localBuffer[i] = value;
            _synced = false;
        }
    }

    public void GetData()
    {
        _deviceBuffer.GetData(_localBuffer);
        _synced = true;
    }

    public void Sync()
    {
        _deviceBuffer.SetData(_localBuffer);
        _synced = true;
    }

    public void Dispose()
    {
        _deviceBuffer.Release();
    }
}

public class MeshBufferContainer : IDisposable
{
    // TODO reduce scene data for finding AABB scene in runtime
    private static readonly AABB Whole = new AABB()
    {
        min = new Vector3(-3, -3, -1),
        max = new Vector3(3, 3, 1)
    };

    public ComputeBuffer Keys => _keysBuffer.DeviceBuffer;
    public ComputeBuffer TriangleIndex => _triangleIndexBuffer.DeviceBuffer;
    public ComputeBuffer TriangleData => _triangleDataBuffer.DeviceBuffer;
    public ComputeBuffer TriangleAABB => _triangleAABBBuffer.DeviceBuffer;
    public ComputeBuffer BvhData => _bvhDataBuffer.DeviceBuffer;
    public ComputeBuffer BvhLeafNode => _bvhLeafNodesBuffer.DeviceBuffer;
    public ComputeBuffer BvhInternalNode => _bvhInternalNodesBuffer.DeviceBuffer;
    
    public AABB[] TriangleAABBLocalData => _triangleAABBBuffer.LocalBuffer;
    public AABB[] BVHLocalData => _bvhDataBuffer.LocalBuffer;
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

    private readonly DataBuffer<uint> _keysBuffer; // TODO uint64, bc on large scenes we can have multiple triangles with the same morton code
    private readonly DataBuffer<uint> _triangleIndexBuffer;
    private readonly DataBuffer<Triangle> _triangleDataBuffer;
    private readonly DataBuffer<AABB> _triangleAABBBuffer;

    private readonly DataBuffer<AABB> _bvhDataBuffer;
    private readonly DataBuffer<LeafNode> _bvhLeafNodesBuffer;
    private readonly DataBuffer<InternalNode> _bvhInternalNodesBuffer;

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

        _keysBuffer = new DataBuffer<uint>(Constants.DATA_ARRAY_COUNT, uint.MaxValue);
        _triangleIndexBuffer = new DataBuffer<uint>(Constants.DATA_ARRAY_COUNT, uint.MaxValue);
        _triangleDataBuffer = new DataBuffer<Triangle>(Constants.DATA_ARRAY_COUNT);
        _triangleAABBBuffer = new DataBuffer<AABB>(Constants.DATA_ARRAY_COUNT);

        _bvhDataBuffer = new DataBuffer<AABB>(Constants.DATA_ARRAY_COUNT);
        _bvhLeafNodesBuffer = new DataBuffer<LeafNode>(Constants.DATA_ARRAY_COUNT);
        _bvhInternalNodesBuffer = new DataBuffer<InternalNode>(Constants.DATA_ARRAY_COUNT);

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
            _keysBuffer[i] = mortonCode;
            _triangleIndexBuffer[i] = i;
            _triangleDataBuffer[i] = new Triangle
            {
                a = a,
                b = b,
                c = c
            };
            _triangleAABBBuffer[i] = aabb;
        }

        _keysBuffer.Sync();
        _triangleIndexBuffer.Sync();
        _triangleDataBuffer.Sync();
        _triangleAABBBuffer.Sync();
    }

    public void GetAllGpuData()
    {
        _keysBuffer.GetData();
        _triangleIndexBuffer.GetData();
        _triangleDataBuffer.GetData();
        _triangleAABBBuffer.GetData();
        _bvhDataBuffer.GetData();
        _bvhLeafNodesBuffer.GetData();
        _bvhInternalNodesBuffer.GetData();
    }


    public void Dispose()
    {
        _keysBuffer.Dispose();
        _triangleIndexBuffer.Dispose();
        _triangleDataBuffer.Dispose();
        _triangleAABBBuffer.Dispose();
        _bvhDataBuffer.Dispose();
        _bvhLeafNodesBuffer.Dispose();
        _bvhInternalNodesBuffer.Dispose();
    }
}