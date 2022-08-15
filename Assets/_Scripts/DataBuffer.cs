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

    public override string ToString()
    {
        if (!_synced)
        {
            GetData();
        }

        return Utils.ArrayToString(_localBuffer).ToString();
    }

    public void Dispose()
    {
        _deviceBuffer.Release();
    }
}