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
    private readonly uint[] _sizesLocalDataBeforeScan = new uint[BUCKET_SIZE * BLOCK_SIZE];
    private readonly uint[] _sizesLocalDataAfterScan = new uint[BUCKET_SIZE * BLOCK_SIZE];
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


        _preScanKernel = _scanShader.FindKernel("PreScan");
        _blockSumKernel = _scanShader.FindKernel("BlockSum");
        _globalScanKernel = _scanShader.FindKernel("GlobalScan");
        _scanShader.SetBuffer(_preScanKernel, "data", _sizesData);
        _scanShader.SetBuffer(_preScanKernel, "blockSumsData", _sizesPrefixSumData);        

        _scanShader.SetBuffer(_blockSumKernel, "blockSumsData", _sizesPrefixSumData);     
        
        _scanShader.SetBuffer(_globalScanKernel, "data", _sizesData);
        _scanShader.SetBuffer(_globalScanKernel, "blockSumsData", _sizesPrefixSumData);
    }

    void Start()
    {
        _localRadixSortShader.Dispatch(_localRadixKernel, BLOCK_SIZE, 1, 1);

        _sizesData.GetData(_sizesLocalDataBeforeScan);
        Debug.Log("Sizes before scan: " + ArrayToString(_sizesLocalDataBeforeScan));

        _scanShader.Dispatch(_preScanKernel, BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE), 1, 1);
        _scanShader.Dispatch(_blockSumKernel, 1, 1, 1);
        _scanShader.Dispatch(_globalScanKernel, BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE), 1, 1);

        GetDataBack();
        PrintData();
        ValidateData();
    }

    void GetDataBack()
    {
        _data.GetData(_sortedData);
        _offsetsData.GetData(_offsetsLocalData);
        _sizesData.GetData(_sizesLocalDataAfterScan);
        _sizesPrefixSumData.GetData(_sizesPrefixSumLocalData);
    }

    void PrintData()
    {
        Debug.Log("Unsorted data: " + ArrayToString(_unsortedData));
        Debug.Log("Sorted data: " + ArrayToString(_sortedData));
        Debug.Log("Offsets local data: " + ArrayToString(_offsetsLocalData));
        Debug.Log("Sizes after scan: " + ArrayToString(_sizesLocalDataAfterScan));
        Debug.Log("Sizes prefix sum after scan: " + ArrayToString(_sizesPrefixSumLocalData));
    }

    void ValidateData()
    {
        // does output sorted data contains all of the elements from input unsorted data? 
        // TODO does output sorted data actually sorted? 
        Dictionary<uint, int> dataDictionary = new Dictionary<uint, int>(256);
        for (uint i = 0; i < DATA_ARRAY_COUNT; i++)
        {
            if (!dataDictionary.ContainsKey(_sortedData[i]))
            {
                dataDictionary.Add(_sortedData[i], 0);
            }

            dataDictionary[_sortedData[i]]++;
            // if (i==0) continue;
            // if (_sortedData[i] < _sortedData[i - 1])
            // {
            //     Debug.LogError("Output data has unsorted element on index " + i);
            // }
        }

        for (uint i = 0; i < DATA_ARRAY_COUNT; i++)
        {
            dataDictionary[_unsortedData[i]]--;
        }

        if (dataDictionary.All(x => x.Value == 0))
        {
            Debug.Log("Output array contains all of the elements from input array");
        }
        else
        {
            Debug.LogError("Output array does not contain all of the elements from input array");
        }

        // Does block prefix sum calculated correctly? 
        bool hasSizesPrefixSumError = false;

        uint prefixSum = 0;
        for (uint i = 0; i < BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE); i++)
        {
            uint sum = 0;
            for (uint j = 0; j < THREADS_PER_BLOCK; j++)
            {
                sum += _sizesLocalDataBeforeScan[i * THREADS_PER_BLOCK + j];
            }

            if (prefixSum != _sizesPrefixSumLocalData[i])
            {
                hasSizesPrefixSumError = true;
                Debug.LogError("Incorrect sum of sizes block " + i + ", should be " + prefixSum + ", got " + _sizesPrefixSumLocalData[i]);
            }

            prefixSum += sum;
        }

        if (!hasSizesPrefixSumError)
        {
            Debug.Log("Sum of sizes block is correct");
        }

        hasSizesPrefixSumError = false;
        for (var i = 1; i < _sizesLocalDataAfterScan.Length; i++)
        {
            if (_sizesLocalDataAfterScan[i] != _sizesLocalDataAfterScan[i - 1] + _sizesLocalDataBeforeScan[i])
            {
                Debug.LogError("Scan operation incorrect at index " + i + ": " + _sizesLocalDataAfterScan[i] + " != " + _sizesLocalDataAfterScan[i - 1] + " + " + _sizesLocalDataBeforeScan[i]);
                hasSizesPrefixSumError = true;
                break;
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