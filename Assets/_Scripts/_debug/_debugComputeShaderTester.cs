using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class _debugComputeShaderTester : MonoBehaviour
{
    [SerializeField] private ComputeShader _computeShader;
    private int _computeShaderKernel;

    private const int BUFFER_SIZE = 128;
    private ComputeBuffer _buffer;
    private ulong [] _bufferData = new ulong [BUFFER_SIZE];

    void Awake()
    {
        _buffer = new ComputeBuffer(BUFFER_SIZE, sizeof(ulong), ComputeBufferType.Structured);

        _computeShaderKernel = _computeShader.FindKernel("CSMain");
        _computeShader.SetBuffer(_computeShaderKernel, "_buffer", _buffer);

        _computeShader.Dispatch(_computeShaderKernel, 1, 1, 1);

        _buffer.GetData(_bufferData);
        Debug.Log(Utils.ArrayToString(_bufferData));
    }

    private void OnDestroy()
    {
        _buffer.Release();
    }
}