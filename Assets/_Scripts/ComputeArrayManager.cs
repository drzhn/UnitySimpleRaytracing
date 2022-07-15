using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

public class ComputeArrayManager : MonoBehaviour
{
    [SerializeField] private ComputeShader _localRadixSortShader;
    [SerializeField] private ComputeShader _globalRadixSortShader;
    [SerializeField] private ComputeShader _scanShader;

    private ComputeBuffer _data;
    private ComputeBuffer _offsetsData;
    private ComputeBuffer _sizesData;
    private ComputeBuffer _sizesPrefixSumData;

    private const int THREADS_PER_BLOCK = 1024;
    private const int BLOCK_SIZE = 8;
    private const int ELEM_PER_THREAD = 1; // TODO later
    private const int DATA_ARRAY_COUNT = ELEM_PER_THREAD * THREADS_PER_BLOCK * BLOCK_SIZE;

    private const int RADIX = 8;
    private const int BUCKET_SIZE = 1 << 8;

    private void Awake()
    {
        _data = new ComputeBuffer(DATA_ARRAY_COUNT, sizeof(uint), ComputeBufferType.Structured);
        _offsetsData = new ComputeBuffer(BUCKET_SIZE * BLOCK_SIZE, sizeof(uint), ComputeBufferType.Structured);
        _sizesData = new ComputeBuffer(BUCKET_SIZE * BLOCK_SIZE, sizeof(uint), ComputeBufferType.Structured);
        _sizesPrefixSumData = new ComputeBuffer(BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE), sizeof(uint), ComputeBufferType.Structured);
    }

    void Start()
    {
        uint[] unsortedData = new uint[DATA_ARRAY_COUNT];
        uint[] sortedData = new uint[DATA_ARRAY_COUNT];
        uint[] offsetsLocalData = new uint[BUCKET_SIZE * BLOCK_SIZE];
        uint[] sizesLocalData = new uint[BUCKET_SIZE * BLOCK_SIZE];
        uint[] sizesPrefixSumLocalData = new uint[BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE)];

        for (uint i = 0; i < unsortedData.Length; i++)
        {
            unsortedData[i] = (uint)Random.Range(0, 256);
        }

        Debug.Log(ArrayToString(unsortedData));

        _data.SetData(unsortedData);
        var localRadixKernel = _localRadixSortShader.FindKernel("CSMain");
        _localRadixSortShader.SetBuffer(localRadixKernel, "data", _data);
        _localRadixSortShader.SetBuffer(localRadixKernel, "offsetsData", _offsetsData);
        _localRadixSortShader.SetBuffer(localRadixKernel, "sizesData", _sizesData);

        _localRadixSortShader.Dispatch(localRadixKernel, BLOCK_SIZE, 1, 1);

        var scanKernel = _scanShader.FindKernel("CSMain");
        _scanShader.SetBuffer(scanKernel, "data", _sizesData);
        _scanShader.SetBuffer(scanKernel, "blockSumsData", _sizesPrefixSumData);
        _scanShader.Dispatch(scanKernel, BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE), 1, 1);


        _data.GetData(sortedData);
        _offsetsData.GetData(offsetsLocalData);
        _sizesData.GetData(sizesLocalData);
        _sizesPrefixSumData.GetData(sizesPrefixSumLocalData);

        Debug.Log(ArrayToString(sortedData));
        Debug.Log(ArrayToString(offsetsLocalData));
        Debug.Log(ArrayToString(sizesLocalData));
        Debug.Log(ArrayToString(sizesPrefixSumLocalData));

        //validation

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

        Debug.Log("Output array contains all of the elements of input array: " + sortedData.All(x => x == 0));

        bool hasSizesPrefixSumError = false;
        for (uint i = 0; i < BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE); i++)
        {
            uint sum = 0;
            for (uint j = 0; j < THREADS_PER_BLOCK; j++)
            {
                sum += sizesLocalData[i * THREADS_PER_BLOCK + j];
            }
        
            if (sum != sizesPrefixSumLocalData[i])
            {
                hasSizesPrefixSumError = true;
                Debug.LogError("Incorrect sum of sizes block " + i+", should be " + sum + ", got " + sizesPrefixSumLocalData[i]);
            }
        }
        
        if (!hasSizesPrefixSumError)
        {
            Debug.Log("Sum of sizes block is correct");
        }
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
        _sizesPrefixSumData.Release();
    }
}