using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

public class ComputeBufferSorter : IDisposable
{
    private readonly ComputeShader _localRadixSortShader;
    private readonly ComputeShader _globalRadixSortShader;
    private readonly ComputeShader _scanShader;

    private readonly int _localRadixKernel;
    private readonly int _preScanKernel;
    private readonly int _blockSumKernel;
    private readonly int _globalScanKernel;
    private readonly int _globalRadixKernel;

    private readonly ComputeBuffer _keys;
    private readonly ComputeBuffer _values;

    private readonly ComputeBuffer _sortedBlocksKeysData;
    private readonly ComputeBuffer _sortedBlocksValuesData;
    private readonly ComputeBuffer _offsetsData;
    private readonly ComputeBuffer _sizesData;
    private readonly ComputeBuffer _sizesPrefixSumData;


    private readonly uint[] _unsortedLocalData = new uint[Constants.DATA_ARRAY_COUNT];
    private readonly uint[] _sortedBlockLocalData = new uint[Constants.DATA_ARRAY_COUNT];
    private readonly uint[] _sortedLocalData = new uint[Constants.DATA_ARRAY_COUNT];
    private readonly uint[] _offsetsLocalData = new uint[Constants.BUCKET_SIZE * Constants.BLOCK_SIZE];
    private readonly uint[] _sizesLocalDataBeforeScan = new uint[Constants.BUCKET_SIZE * Constants.BLOCK_SIZE];
    private readonly uint[] _sizesLocalDataAfterScan = new uint[Constants.BUCKET_SIZE * Constants.BLOCK_SIZE];
    private readonly uint[] _sizesPrefixSumLocalData = new uint[Constants.BLOCK_SIZE / (Constants.THREADS_PER_BLOCK / Constants.BUCKET_SIZE)];

    private readonly Dictionary<uint, int> _debugDataDictionary = new(256);

    public ComputeBufferSorter(ComputeBuffer keys, ComputeBuffer values, IShaderContainer shaderContainer)
    {
        _keys = keys;
        _values = values;

        _localRadixSortShader = shaderContainer.Sorting.LocalRadixSortShader;
        _globalRadixSortShader = shaderContainer.Sorting.GlobalRadixSortShader;
        _scanShader = shaderContainer.Sorting.ScanShader;
        

        _sortedBlocksKeysData = new ComputeBuffer(Constants.DATA_ARRAY_COUNT, sizeof(uint), ComputeBufferType.Structured);
        _sortedBlocksValuesData = new ComputeBuffer(Constants.DATA_ARRAY_COUNT, sizeof(uint), ComputeBufferType.Structured);
        _offsetsData = new ComputeBuffer(Constants.BUCKET_SIZE * Constants.BLOCK_SIZE, sizeof(uint), ComputeBufferType.Structured);
        _sizesData = new ComputeBuffer(Constants.BUCKET_SIZE * Constants.BLOCK_SIZE, sizeof(uint), ComputeBufferType.Structured);
        _sizesPrefixSumData = new ComputeBuffer(Constants.BLOCK_SIZE / (Constants.THREADS_PER_BLOCK / Constants.BUCKET_SIZE), sizeof(uint), ComputeBufferType.Structured);

        // Set data

        _localRadixKernel = _localRadixSortShader.FindKernel("LocalRadixSort");
        _localRadixSortShader.SetBuffer(_localRadixKernel, "keysData", _keys);
        _localRadixSortShader.SetBuffer(_localRadixKernel, "valuesData", _values);
        _localRadixSortShader.SetBuffer(_localRadixKernel, "sortedBlocksKeysData", _sortedBlocksKeysData);
        _localRadixSortShader.SetBuffer(_localRadixKernel, "sortedBlocksValuesData", _sortedBlocksValuesData);
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
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sortedBlocksKeysData", _sortedBlocksKeysData);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sortedBlocksValuesData", _sortedBlocksValuesData);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "offsetsData", _offsetsData);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sizesData", _sizesData);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sortedKeysData", _keys);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sortedValuesData", _values);

        // debug data
        
        _keys.GetData(_unsortedLocalData);
        
        for (uint i = 0; i < 256; i++)
        {
            _debugDataDictionary.Add(i, 0);
        }
    }

    public void Sort()
    {
        for (int bitOffset = 0; bitOffset < 32; bitOffset += Constants.RADIX)
        {
            _localRadixSortShader.SetInt("bitOffset", bitOffset);
            _globalRadixSortShader.SetInt("bitOffset", bitOffset);

            _localRadixSortShader.Dispatch(_localRadixKernel, Constants.BLOCK_SIZE, 1, 1);

            _sizesData.GetData(_sizesLocalDataBeforeScan);
            Debug.Log("Sizes before scan: " + Utils.ArrayToString(_sizesLocalDataBeforeScan));

            _scanShader.Dispatch(_preScanKernel, Constants.BLOCK_SIZE / (Constants.THREADS_PER_BLOCK / Constants.BUCKET_SIZE), 1, 1);
            _scanShader.Dispatch(_blockSumKernel, 1, 1, 1);
            _scanShader.Dispatch(_globalScanKernel, Constants.BLOCK_SIZE / (Constants.THREADS_PER_BLOCK / Constants.BUCKET_SIZE), 1, 1);

            _globalRadixSortShader.Dispatch(_globalRadixKernel, Constants.BLOCK_SIZE, 1, 1);

            GetIntermediateDataBack();
            ValidateIntermediateData(bitOffset);
        }

        GetSortedDataBack();
        PrintData();

        ValidateSortedData();
    }

    void GetIntermediateDataBack()
    {
        _sortedBlocksKeysData.GetData(_sortedBlockLocalData);
        _offsetsData.GetData(_offsetsLocalData);
        _sizesData.GetData(_sizesLocalDataAfterScan);
        _sizesPrefixSumData.GetData(_sizesPrefixSumLocalData);
    }

    void GetSortedDataBack()
    {
        _keys.GetData(_sortedLocalData);
    }

    void PrintData()
    {
        Debug.Log("Unsorted data: " + Utils.ArrayToString(_unsortedLocalData));
        Debug.Log("Sorted data: " + Utils.ArrayToString(_sortedLocalData));
        Debug.Log("Offsets local data: " + Utils.ArrayToString(_offsetsLocalData));
        Debug.Log("Sizes after scan: " + Utils.ArrayToString(_sizesLocalDataAfterScan));
        Debug.Log("Sizes prefix sum after scan: " + Utils.ArrayToString(_sizesPrefixSumLocalData));
    }

    void ValidateSortedData()
    {
        // does output sorted data actually sorted?
        for (uint i = 1; i < Constants.DATA_ARRAY_COUNT; i++)
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
        return (value >> bitOffset) & (Constants.BUCKET_SIZE - 1);
    }

    void ValidateIntermediateData(int bitOffset)
    {
        for (uint i = 0; i < 256; i++)
        {
            _debugDataDictionary[i] = 0;
        }

        // does output sorted data contains all of the elements from input unsorted data? 
        for (uint i = 0; i < Constants.DATA_ARRAY_COUNT; i++)
        {
            _debugDataDictionary[GetRadix(_sortedBlockLocalData[i], bitOffset)]++;
        }

        for (uint i = 0; i < Constants.DATA_ARRAY_COUNT; i++)
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
        for (uint i = 0; i < Constants.BLOCK_SIZE; i++)
        {
            for (uint j = 0; j < Constants.THREADS_PER_BLOCK; j++)
            {
                _debugDataDictionary[GetRadix(_sortedBlockLocalData[i * Constants.THREADS_PER_BLOCK + j], bitOffset)]++;
            }

            for (uint k = 0; k < 256; k++)
            {
                if (_debugDataDictionary[k] != _sizesLocalDataBeforeScan[i + k * Constants.BLOCK_SIZE])
                {
                    Debug.LogError("In block " + i + " amount of " + k + " is " + _debugDataDictionary[k] + ", not " + _sizesLocalDataBeforeScan[i + k * Constants.BLOCK_SIZE]);
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

    public void Dispose()
    {
        _sortedBlocksKeysData.Release();
        _sortedBlocksValuesData.Release();
        _offsetsData.Release();
        _sizesData.Release();
        _sizesPrefixSumData.Release();
    }
}