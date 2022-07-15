using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

public class ComputeArrayManager : MonoBehaviour
{
    [SerializeField] private ComputeShader _radixSortShader;

    private ComputeBuffer _data;
    private ComputeBuffer _offsetsData;
    private ComputeBuffer _sizesData;

    private const int THREADS_PER_BLOCK = 1024;
    private const int BLOCK_SIZE = 3;
    private const int ELEM_PER_THREAD = 1; // TODO later
    private const int DATA_ARRAY_COUNT = ELEM_PER_THREAD * THREADS_PER_BLOCK * BLOCK_SIZE;

    private const int RADIX = 8;
    private const int BUCKET_SIZE = 1 << 8;

    private void Awake()
    {
        _data = new ComputeBuffer(DATA_ARRAY_COUNT, sizeof(uint), ComputeBufferType.Structured);
        _offsetsData = new ComputeBuffer(BUCKET_SIZE * BLOCK_SIZE, sizeof(uint), ComputeBufferType.Structured);
        _sizesData = new ComputeBuffer(BUCKET_SIZE * BLOCK_SIZE, sizeof(uint), ComputeBufferType.Structured);
    }

    void Start()
    {
        uint[] unsortedData = new uint[DATA_ARRAY_COUNT];
        uint[] sortedData = new uint[DATA_ARRAY_COUNT];
        uint[] offsetsLocalData = new uint[1 << 8];
        uint[] sizesLocalData = new uint[1 << 8];
        for (uint i = 0; i < unsortedData.Length; i++)
        {
            unsortedData[i] = (uint)Random.Range(0, 256);
        }

        Debug.Log(ArrayToString(unsortedData));

        _data.SetData(unsortedData);
        var kernel = _radixSortShader.FindKernel("CSMain");
        _radixSortShader.SetBuffer(kernel, "data", _data);
        _radixSortShader.SetBuffer(kernel, "offsetsData", _offsetsData);
        _radixSortShader.SetBuffer(kernel, "sizesData", _sizesData);
        
        _radixSortShader.Dispatch(kernel, BLOCK_SIZE, 1, 1);
        
        _data.GetData(sortedData);
        _offsetsData.GetData(offsetsLocalData);
        _sizesData.GetData(sizesLocalData);

        Debug.Log(ArrayToString(sortedData));
        Debug.Log(ArrayToString(offsetsLocalData));
        Debug.Log(ArrayToString(sizesLocalData));

        for (uint i = 0; i < DATA_ARRAY_COUNT; i++)
        {
            int index = Array.IndexOf(sortedData, unsortedData[i]);
            if (index == -1)
            {
                Debug.LogError("Cannot find element!" + unsortedData[i]);
            }
            else
            {
                sortedData[index] = 0;
            }
        }

        Debug.Log(sortedData.All(x => x == 0));
    }

    private StringBuilder ArrayToString(uint[] array)
    {
        StringBuilder builder = new StringBuilder("");
        foreach (uint u in array)
        {
            builder.Append(u + " ");
        }

        return builder;
    }

    private void OnDestroy()
    {
        _data.Release();
        _offsetsData.Release();
        _sizesData.Release();
    }
}