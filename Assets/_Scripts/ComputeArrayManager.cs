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
    private int _preScanKernel;
    private int _blockSumKernel;
    private int _globalScanKernel;
    private int _globalRadixKernel;

    private ComputeBuffer _unsortedData;
    private ComputeBuffer _sortedData;
    private ComputeBuffer _offsetsData;
    private ComputeBuffer _sizesData;
    private ComputeBuffer _sizesPrefixSumData;

    private const int THREADS_PER_BLOCK = 1024;
    private const int BLOCK_SIZE = 512;
    private const int ELEM_PER_THREAD = 1; // TODO later
    private const int DATA_ARRAY_COUNT = ELEM_PER_THREAD * THREADS_PER_BLOCK * BLOCK_SIZE;

    private const int RADIX = 8;
    private const int BUCKET_SIZE = 1 << 8;

    private readonly uint[] _unsortedLocalData = new uint[DATA_ARRAY_COUNT];
    private readonly uint[] _sortedLocalData = new uint[DATA_ARRAY_COUNT];
    private readonly uint[] _offsetsLocalData = new uint[BUCKET_SIZE * BLOCK_SIZE];
    private readonly uint[] _sizesLocalDataBeforeScan = new uint[BUCKET_SIZE * BLOCK_SIZE];
    private readonly uint[] _sizesLocalDataAfterScan = new uint[BUCKET_SIZE * BLOCK_SIZE];
    private readonly uint[] _sizesPrefixSumLocalData = new uint[BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE)];

    private void Awake()
    {
        _unsortedData = new ComputeBuffer(DATA_ARRAY_COUNT, sizeof(uint), ComputeBufferType.Structured);
        _sortedData = new ComputeBuffer(DATA_ARRAY_COUNT, sizeof(uint), ComputeBufferType.Structured);
        _offsetsData = new ComputeBuffer(BUCKET_SIZE * BLOCK_SIZE, sizeof(uint), ComputeBufferType.Structured);
        _sizesData = new ComputeBuffer(BUCKET_SIZE * BLOCK_SIZE, sizeof(uint), ComputeBufferType.Structured);
        _sizesPrefixSumData = new ComputeBuffer(BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE), sizeof(uint), ComputeBufferType.Structured);

        // Generate random data
        for (uint i = 0; i < DATA_ARRAY_COUNT; i++)
        {
            _unsortedLocalData[i] = (uint)Random.Range(0, 256);
        }

        _unsortedData.SetData(_unsortedLocalData);

        // Set data

        _localRadixKernel = _localRadixSortShader.FindKernel("LocalRadixSort");
        _localRadixSortShader.SetBuffer(_localRadixKernel, "data", _unsortedData);
        _localRadixSortShader.SetBuffer(_localRadixKernel, "offsetsData", _offsetsData);
        _localRadixSortShader.SetBuffer(_localRadixKernel, "sizesData", _sizesData);


        _preScanKernel = _scanShader.FindKernel("PreScan");
        _blockSumKernel = _scanShader.FindKernel("BlockSum");
        _globalScanKernel = _scanShader.FindKernel("GlobalScan");
        _scanShader.SetBuffer(_preScanKernel, "data", _sizesData);
        _scanShader.SetBuffer(_preScanKernel, "blockSumsData", _sizesPrefixSumData);
        _scanShader.SetBuffer(_blockSumKernel, "blockSumsData", _sizesPrefixSumData);
        _scanShader.SetBuffer(_globalScanKernel, "data", _sizesData);
        _scanShader.SetBuffer(_globalScanKernel, "blockSumsData", _sizesPrefixSumData);

        _globalRadixKernel = _globalRadixSortShader.FindKernel("GlobalRadixSort");
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "data", _unsortedData);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "offsetsData", _offsetsData);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sizesData", _sizesData);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sortedData", _sortedData);
    }

    void Start()
    {
        _localRadixSortShader.Dispatch(_localRadixKernel, BLOCK_SIZE, 1, 1);

        _sizesData.GetData(_sizesLocalDataBeforeScan);
        Debug.Log("Sizes before scan: " + ArrayToString(_sizesLocalDataBeforeScan));

        _scanShader.Dispatch(_preScanKernel, BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE), 1, 1);
        _scanShader.Dispatch(_blockSumKernel, 1, 1, 1);
        _scanShader.Dispatch(_globalScanKernel, BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE), 1, 1);

        // _offsetsData.GetData(_offsetsLocalData);
        // _sizesData.GetData(_sizesLocalDataAfterScan);
        // _unsortedData.GetData(_sortedLocalData);
        // Debug.Log("Data after local sort: " + ArrayToString(_sortedLocalData));

        // TryDataCPU();

        _globalRadixSortShader.Dispatch(_globalRadixKernel, BLOCK_SIZE, 1, 1);

        GetDataBack();
        PrintData();
        ValidateData();
    }

    void TryDataCPU()
    {
        uint[] resultData = new uint[DATA_ARRAY_COUNT];
        uint[] offsetData = new uint[BUCKET_SIZE];
        uint[] sizesData = new uint[BUCKET_SIZE];
        for (uint groupId = 0; groupId < BLOCK_SIZE; groupId++)
        {
            for (uint j = 0; j < BUCKET_SIZE; j++)
            {
                offsetData[j] = _offsetsLocalData[groupId * BUCKET_SIZE + j];
                sizesData[j] = _sizesLocalDataAfterScan[groupId + j * BLOCK_SIZE];
            }

            for (uint threadId = 0; threadId < THREADS_PER_BLOCK; threadId++)
            {
                uint radix = _sortedLocalData[groupId * THREADS_PER_BLOCK + threadId];
                uint indexOutput = sizesData[radix] + threadId - offsetData[radix];

                resultData[indexOutput] = _sortedLocalData[groupId * THREADS_PER_BLOCK + threadId];
            }
        }

        Debug.Log("");
        Debug.Log("Should be: " + ArrayToString(resultData));
        Debug.Log("");
    }

    void GetDataBack()
    {
        _sortedData.GetData(_sortedLocalData);
        _offsetsData.GetData(_offsetsLocalData);
        _sizesData.GetData(_sizesLocalDataAfterScan);
        _sizesPrefixSumData.GetData(_sizesPrefixSumLocalData);
    }

    void PrintData()
    {
        Debug.Log("Unsorted data: " + ArrayToString(_unsortedLocalData));
        Debug.Log("Sorted data: " + ArrayToString(_sortedLocalData));
        Debug.Log("Offsets local data: " + ArrayToString(_offsetsLocalData));
        Debug.Log("Sizes after scan: " + ArrayToString(_sizesLocalDataAfterScan));
        Debug.Log("Sizes prefix sum after scan: " + ArrayToString(_sizesPrefixSumLocalData));
    }

    void ValidateData()
    {
        Dictionary<uint, int> dataDictionary = new Dictionary<uint, int>(256);
        for (uint i = 0; i < 256; i++)
        {
            dataDictionary.Add(i, 0);
        }

        // does output sorted data contains all of the elements from input unsorted data? 
        for (uint i = 0; i < DATA_ARRAY_COUNT; i++)
        {
            dataDictionary[_sortedLocalData[i]]++;
            if (i == 0) continue;
            if (_sortedLocalData[i] < _sortedLocalData[i - 1])
            {
                Debug.LogError("Output data has unsorted element on index " + i);
            }
        }

        for (uint i = 0; i < DATA_ARRAY_COUNT; i++)
        {
            dataDictionary[_unsortedLocalData[i]]--;
        }

        if (dataDictionary.All(x => x.Value == 0))
        {
            Debug.Log("Output data contains all of the elements from input array");
        }
        else
        {
            Debug.LogError("Output data does not contain all of the elements from input array");
        }

        for (uint i = 0; i < 256; i++)
        {
            dataDictionary[i] = 0;
        }

        // Does sizes calculated correctly?
        for (uint i = 0; i < 256; i++)
        {
            dataDictionary[i] = 0;
        }

        for (uint i = 0; i < BLOCK_SIZE; i++)
        {
            for (uint j = 0; j < THREADS_PER_BLOCK; j++)
            {
                dataDictionary[_unsortedLocalData[i * THREADS_PER_BLOCK + j]]++;
            }

            for (uint k = 0; k < 256; k++)
            {
                if (dataDictionary[k] != _sizesLocalDataBeforeScan[i + k * BLOCK_SIZE])
                {
                    Debug.LogError("In block " + i + " amount of " + k + " is " + dataDictionary[k] + ", not " + _sizesLocalDataBeforeScan[i + k * BLOCK_SIZE]);
                    // break;
                }
            }

            for (uint k = 0; k < 256; k++)
            {
                dataDictionary[k] = 0;
            }

            // if (hasError)
            // {
            //     break;
            // }
        }

        // Does block prefix sum calculated correctly? 
        bool hasSizesPrefixSumError = false;
        for (var i = 1; i < _sizesLocalDataAfterScan.Length; i++)
        {
            if (_sizesLocalDataAfterScan[i] != _sizesLocalDataBeforeScan[i - 1] + _sizesLocalDataAfterScan[i - 1])
            {
                Debug.LogError("Scan operation incorrect at index " + i + ": " + _sizesLocalDataAfterScan[i] + " != " + _sizesLocalDataBeforeScan[i - 1] + " + " + _sizesLocalDataAfterScan[i - 1]);
                hasSizesPrefixSumError = true;
                break;
            }
        }

        if (!hasSizesPrefixSumError)
        {
            Debug.Log("Scan operation is correct");
        }
    }

    private static StringBuilder ArrayToString(uint[] array, uint maxElements = 4096)
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
        _unsortedData.Release();
        _sortedData.Release();
        _offsetsData.Release();
        _sizesData.Release();
        _sizesPrefixSumData.Release();
    }
}