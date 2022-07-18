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
    private int _localRadixKernel;
    private int _scanKernel;

    private ComputeBuffer _data;
    private ComputeBuffer _offsetsData;
    private ComputeBuffer _sizesData;
    private ComputeBuffer _sizesPrefixSumData;

    private const int THREADS_PER_BLOCK = 1024;
    private const int BLOCK_SIZE = 512;
    private const int ELEM_PER_THREAD = 1; // TODO later
    private const int DATA_ARRAY_COUNT = ELEM_PER_THREAD * THREADS_PER_BLOCK * BLOCK_SIZE;

    private const int RADIX = 8;
    private const int BUCKET_SIZE = 1 << 8;

    private readonly uint[] _unsortedData = new uint[DATA_ARRAY_COUNT];
    private readonly uint[] _sortedData = new uint[DATA_ARRAY_COUNT];
    private readonly uint[] _offsetsLocalData = new uint[BUCKET_SIZE * BLOCK_SIZE];
    private readonly uint[] _sizesLocalData = new uint[BUCKET_SIZE * BLOCK_SIZE];
    private readonly uint[] _sizesPrefixSumLocalData = new uint[BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE)];

    private void Awake()
    {
        _data = new ComputeBuffer(DATA_ARRAY_COUNT, sizeof(uint), ComputeBufferType.Structured);
        _offsetsData = new ComputeBuffer(BUCKET_SIZE * BLOCK_SIZE, sizeof(uint), ComputeBufferType.Structured);
        _sizesData = new ComputeBuffer(BUCKET_SIZE * BLOCK_SIZE, sizeof(uint), ComputeBufferType.Structured);
        _sizesPrefixSumData = new ComputeBuffer(BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE), sizeof(uint), ComputeBufferType.Structured);

        // Generate random data
        for (uint i = 0; i < DATA_ARRAY_COUNT; i++)
        {
            _unsortedData[i] = (uint)Random.Range(0, 256);
        }

        _data.SetData(_unsortedData);

        // Set data

        _localRadixKernel = _localRadixSortShader.FindKernel("CSMain");
        _localRadixSortShader.SetBuffer(_localRadixKernel, "data", _data);
        _localRadixSortShader.SetBuffer(_localRadixKernel, "offsetsData", _offsetsData);
        _localRadixSortShader.SetBuffer(_localRadixKernel, "sizesData", _sizesData);


        _scanKernel = _scanShader.FindKernel("CSMain");
        _scanShader.SetBuffer(_scanKernel, "data", _sizesData);
        _scanShader.SetBuffer(_scanKernel, "blockSumsData", _sizesPrefixSumData);
    }

    void Start()
    {
        _localRadixSortShader.Dispatch(_localRadixKernel, BLOCK_SIZE, 1, 1);
        _scanShader.Dispatch(_scanKernel, BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE), 1, 1);

        GetDataBack();
        PrintData();
        ValidateData();
    }

    void GetDataBack()
    {
        _data.GetData(_sortedData);
        _offsetsData.GetData(_offsetsLocalData);
        _sizesData.GetData(_sizesLocalData);
        _sizesPrefixSumData.GetData(_sizesPrefixSumLocalData);
    }

    void PrintData()
    {
        Debug.Log(ArrayToString(_unsortedData));
        Debug.Log(ArrayToString(_sortedData));
        Debug.Log(ArrayToString(_offsetsLocalData));
        Debug.Log(ArrayToString(_sizesLocalData));
        Debug.Log(ArrayToString(_sizesPrefixSumLocalData));
    }

    void ValidateData()
    {
        Dictionary<uint, int> dataDictionary = new Dictionary<uint, int>(256);
        for (uint i = 0; i < DATA_ARRAY_COUNT; i++)
        {
            if (!dataDictionary.ContainsKey(_sortedData[i]))
            {
                dataDictionary.Add(_sortedData[i], 0);
            }
            dataDictionary[_sortedData[i]]++;
        }

        for (uint i = 0; i < DATA_ARRAY_COUNT; i++)
        {
            dataDictionary[_unsortedData[i]]--;
        }

        Debug.Log("Output array contains all of the elements of input array: " + dataDictionary.All(x => x.Value == 0));

        bool hasSizesPrefixSumError = false;
        for (uint i = 0; i < BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE); i++)
        {
            uint sum = 0;
            for (uint j = 0; j < THREADS_PER_BLOCK; j++)
            {
                sum += _sizesLocalData[i * THREADS_PER_BLOCK + j];
            }

            if (sum != _sizesPrefixSumLocalData[i])
            {
                hasSizesPrefixSumError = true;
                Debug.LogError("Incorrect sum of sizes block " + i + ", should be " + sum + ", got " + _sizesPrefixSumLocalData[i]);
            }
        }

        if (!hasSizesPrefixSumError)
        {
            Debug.Log("Sum of sizes block is correct");
        }
    }

    private static StringBuilder ArrayToString(uint[] array, uint maxElements = 2048)
    {
        StringBuilder builder = new StringBuilder("");
        for (var i = 0; i < array.Length; i++)
        {
            if (i >= maxElements) break;
            builder.Append(array[i] + " ");
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