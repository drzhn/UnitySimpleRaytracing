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

    private ComputeBuffer _data;
    private ComputeBuffer _sortedBlocksData;
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
    private readonly uint[] _sortedBlockLocalData = new uint[DATA_ARRAY_COUNT];
    private readonly uint[] _sortedLocalData = new uint[DATA_ARRAY_COUNT];
    private readonly uint[] _offsetsLocalData = new uint[BUCKET_SIZE * BLOCK_SIZE];
    private readonly uint[] _sizesLocalDataBeforeScan = new uint[BUCKET_SIZE * BLOCK_SIZE];
    private readonly uint[] _sizesLocalDataAfterScan = new uint[BUCKET_SIZE * BLOCK_SIZE];
    private readonly uint[] _sizesPrefixSumLocalData = new uint[BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE)];

    private readonly Dictionary<uint, int> _debugDataDictionary = new(256);

    private void Awake()
    {
        _data = new ComputeBuffer(DATA_ARRAY_COUNT, sizeof(uint), ComputeBufferType.Structured);
        _sortedBlocksData = new ComputeBuffer(DATA_ARRAY_COUNT, sizeof(uint), ComputeBufferType.Structured);
        _offsetsData = new ComputeBuffer(BUCKET_SIZE * BLOCK_SIZE, sizeof(uint), ComputeBufferType.Structured);
        _sizesData = new ComputeBuffer(BUCKET_SIZE * BLOCK_SIZE, sizeof(uint), ComputeBufferType.Structured);
        _sizesPrefixSumData = new ComputeBuffer(BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE), sizeof(uint), ComputeBufferType.Structured);

        // Generate random data
        for (uint i = 0; i < DATA_ARRAY_COUNT; i++)
        {
            _unsortedLocalData[i] = (uint)Random.Range(0, uint.MaxValue);
        }

        _data.SetData(_unsortedLocalData);

        // Set data

        _localRadixKernel = _localRadixSortShader.FindKernel("LocalRadixSort");
        _localRadixSortShader.SetBuffer(_localRadixKernel, "unsortedData", _data);
        _localRadixSortShader.SetBuffer(_localRadixKernel, "sortedBlocksData", _sortedBlocksData);
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
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sortedBlocksData", _sortedBlocksData);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "offsetsData", _offsetsData);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sizesData", _sizesData);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sortedData", _data);

        // debug data
        for (uint i = 0; i < 256; i++)
        {
            _debugDataDictionary.Add(i, 0);
        }
    }

    void Start()
    {
        for (int bitOffset = 0; bitOffset < 32; bitOffset += RADIX)
        {
            _localRadixSortShader.SetInt("bitOffset", bitOffset);
            _globalRadixSortShader.SetInt("bitOffset", bitOffset);

            _localRadixSortShader.Dispatch(_localRadixKernel, BLOCK_SIZE, 1, 1);

            _sizesData.GetData(_sizesLocalDataBeforeScan);
            Debug.Log("Sizes before scan: " + ArrayToString(_sizesLocalDataBeforeScan));

            _scanShader.Dispatch(_preScanKernel, BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE), 1, 1);
            _scanShader.Dispatch(_blockSumKernel, 1, 1, 1);
            _scanShader.Dispatch(_globalScanKernel, BLOCK_SIZE / (THREADS_PER_BLOCK / BUCKET_SIZE), 1, 1);

            _globalRadixSortShader.Dispatch(_globalRadixKernel, BLOCK_SIZE, 1, 1);

            GetIntermediateDataBack();
            ValidateIntermediateData(bitOffset);
        }

        GetSortedDataBack();
        PrintData();

        ValidateSortedData();
    }

    void GetIntermediateDataBack()
    {
        _sortedBlocksData.GetData(_sortedBlockLocalData);
        _offsetsData.GetData(_offsetsLocalData);
        _sizesData.GetData(_sizesLocalDataAfterScan);
        _sizesPrefixSumData.GetData(_sizesPrefixSumLocalData);
    }

    void GetSortedDataBack()
    {
        _data.GetData(_sortedLocalData);
    }

    void PrintData()
    {
        Debug.Log("Unsorted data: " + ArrayToString(_unsortedLocalData));
        Debug.Log("Sorted data: " + ArrayToString(_sortedLocalData));
        Debug.Log("Offsets local data: " + ArrayToString(_offsetsLocalData));
        Debug.Log("Sizes after scan: " + ArrayToString(_sizesLocalDataAfterScan));
        Debug.Log("Sizes prefix sum after scan: " + ArrayToString(_sizesPrefixSumLocalData));
    }

    void ValidateSortedData()
    {
        // does output sorted data actually sorted?
        for (uint i = 1; i < DATA_ARRAY_COUNT; i++)
        {
            if (_sortedLocalData[i] < _sortedLocalData[i - 1])
            {
                Debug.LogError("Output data has unsorted element on index " + i);
                return;
            }
        }

        Debug.Log("Output data is sorted");
    }

    uint GetRadix(uint value, int bitOffset)
    {
        return (value >> bitOffset) & (BUCKET_SIZE - 1);
    }

    void ValidateIntermediateData(int bitOffset)
    {
        for (uint i = 0; i < 256; i++)
        {
            _debugDataDictionary[i] = 0;
        } 
        
        // does output sorted data contains all of the elements from input unsorted data? 
        for (uint i = 0; i < DATA_ARRAY_COUNT; i++)
        {
            _debugDataDictionary[GetRadix(_sortedBlockLocalData[i], bitOffset)]++;
        }

        for (uint i = 0; i < DATA_ARRAY_COUNT; i++)
        {
            _debugDataDictionary[GetRadix(_unsortedLocalData[i], bitOffset)]--;
        }

        if (_debugDataDictionary.All(x => x.Value == 0))
        {
            //Debug.Log("Output data contains all of the elements from input array");
        }
        else
        {
            Debug.LogError("Output data does not contain all of the elements from input array");
        }

        for (uint i = 0; i < 256; i++)
        {
            _debugDataDictionary[i] = 0;
        }

        // Does sizes calculated correctly?

        bool hasError = false;
        for (uint i = 0; i < BLOCK_SIZE; i++)
        {
            for (uint j = 0; j < THREADS_PER_BLOCK; j++)
            {
                _debugDataDictionary[GetRadix(_sortedBlockLocalData[i * THREADS_PER_BLOCK + j], bitOffset)]++;
            }

            for (uint k = 0; k < 256; k++)
            {
                if (_debugDataDictionary[k] != _sizesLocalDataBeforeScan[i + k * BLOCK_SIZE])
                {
                    Debug.LogError("In block " + i + " amount of " + k + " is " + _debugDataDictionary[k] + ", not " + _sizesLocalDataBeforeScan[i + k * BLOCK_SIZE]);
                    hasError = true;
                    break;
                }
            }

            for (uint k = 0; k < 256; k++)
            {
                _debugDataDictionary[k] = 0;
            }

            if (hasError)
            {
                break;
            }
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
        _data.Release();
        _sortedBlocksData.Release();
        _offsetsData.Release();
        _sizesData.Release();
        _sizesPrefixSumData.Release();
    }
}