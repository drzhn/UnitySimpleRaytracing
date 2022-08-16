using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

public class ComputeBufferSorter<TKey, TValue> : IDisposable where TKey: struct, IComparable where TValue: struct
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

    private readonly DataBuffer<TKey> _sortedBlocksKeysData;
    private readonly DataBuffer<TValue> _sortedBlocksValuesData;
    private readonly DataBuffer<uint> _offsetsData;
    private readonly DataBuffer<uint> _sizesData;
    private readonly DataBuffer<uint> _sizesPrefixSumData;
    
    private readonly TKey[] _unsortedKeysLocalData = new TKey[Constants.DATA_ARRAY_COUNT];
    private readonly TKey[] _sortedKeysLocalData = new TKey[Constants.DATA_ARRAY_COUNT];
    private readonly uint[] _sizesLocalDataBeforeScan = new uint[Constants.BUCKET_SIZE * Constants.BLOCK_SIZE];

    // private readonly uint[] _sortedBlockLocalData = new uint[Constants.DATA_ARRAY_COUNT];
    // private readonly uint[] _sortedLocalData = new uint[Constants.DATA_ARRAY_COUNT];
    // private readonly uint[] _offsetsLocalData = new uint[Constants.BUCKET_SIZE * Constants.BLOCK_SIZE];
    // private readonly uint[] _sizesLocalDataAfterScan = new uint[Constants.BUCKET_SIZE * Constants.BLOCK_SIZE];
    // private readonly uint[] _sizesPrefixSumLocalData = new uint[Constants.BLOCK_SIZE / (Constants.THREADS_PER_BLOCK / Constants.BUCKET_SIZE)];

    private readonly Dictionary<uint, int> _debugDataDictionary = new(256);
    private readonly int _dataLength;

    public ComputeBufferSorter(int dataLength, ComputeBuffer keys, ComputeBuffer values, IShaderContainer shaderContainer)
    {
        _keys = keys;
        _values = values;

        _dataLength = dataLength;

        _localRadixSortShader = shaderContainer.Sorting.LocalRadixSortShader;
        _globalRadixSortShader = shaderContainer.Sorting.GlobalRadixSortShader;
        _scanShader = shaderContainer.Sorting.ScanShader;


        _sortedBlocksKeysData = new DataBuffer<TKey>(Constants.DATA_ARRAY_COUNT);
        _sortedBlocksValuesData = new DataBuffer<TValue>(Constants.DATA_ARRAY_COUNT);
        _offsetsData = new DataBuffer<uint>(Constants.BUCKET_SIZE * Constants.BLOCK_SIZE);
        _sizesData = new DataBuffer<uint>(Constants.BUCKET_SIZE * Constants.BLOCK_SIZE);
        _sizesPrefixSumData = new DataBuffer<uint>(Constants.BLOCK_SIZE / (Constants.THREADS_PER_BLOCK / Constants.BUCKET_SIZE));

        // Set data

        _localRadixKernel = _localRadixSortShader.FindKernel("LocalRadixSort");
        _localRadixSortShader.SetBuffer(_localRadixKernel, "keysData", _keys);
        _localRadixSortShader.SetBuffer(_localRadixKernel, "valuesData", _values);
        _localRadixSortShader.SetBuffer(_localRadixKernel, "sortedBlocksKeysData", _sortedBlocksKeysData.DeviceBuffer);
        _localRadixSortShader.SetBuffer(_localRadixKernel, "sortedBlocksValuesData", _sortedBlocksValuesData.DeviceBuffer);
        _localRadixSortShader.SetBuffer(_localRadixKernel, "offsetsData", _offsetsData.DeviceBuffer);
        _localRadixSortShader.SetBuffer(_localRadixKernel, "sizesData", _sizesData.DeviceBuffer);


        _preScanKernel = _scanShader.FindKernel("PreScan");
        _blockSumKernel = _scanShader.FindKernel("BlockSum");
        _globalScanKernel = _scanShader.FindKernel("GlobalScan");
        _scanShader.SetBuffer(_preScanKernel, "data", _sizesData.DeviceBuffer);
        _scanShader.SetBuffer(_preScanKernel, "blockSumsData", _sizesPrefixSumData.DeviceBuffer);
        _scanShader.SetBuffer(_blockSumKernel, "blockSumsData", _sizesPrefixSumData.DeviceBuffer);
        _scanShader.SetBuffer(_globalScanKernel, "data", _sizesData.DeviceBuffer);
        _scanShader.SetBuffer(_globalScanKernel, "blockSumsData", _sizesPrefixSumData.DeviceBuffer);

        _globalRadixKernel = _globalRadixSortShader.FindKernel("GlobalRadixSort");
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sortedBlocksKeysData", _sortedBlocksKeysData.DeviceBuffer);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sortedBlocksValuesData", _sortedBlocksValuesData.DeviceBuffer);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "offsetsData", _offsetsData.DeviceBuffer);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sizesData", _sizesData.DeviceBuffer);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sortedKeysData", _keys);
        _globalRadixSortShader.SetBuffer(_globalRadixKernel, "sortedValuesData", _values);

        // debug data

        _keys.GetData(_unsortedKeysLocalData);

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

            _sizesData.DeviceBuffer.GetData(_sizesLocalDataBeforeScan);
            // Debug.Log("Sizes before scan: " + Utils.ArrayToString(_sizesLocalDataBeforeScan));

            _scanShader.Dispatch(_preScanKernel, Constants.BLOCK_SIZE / (Constants.THREADS_PER_BLOCK / Constants.BUCKET_SIZE), 1, 1);
            _scanShader.Dispatch(_blockSumKernel, 1, 1, 1);
            _scanShader.Dispatch(_globalScanKernel, Constants.BLOCK_SIZE / (Constants.THREADS_PER_BLOCK / Constants.BUCKET_SIZE), 1, 1);

            _globalRadixSortShader.Dispatch(_globalRadixKernel, Constants.BLOCK_SIZE, 1, 1);

            GetIntermediateDataBack();
            ValidateIntermediateData(bitOffset);
        }

        GetSortedDataBack();
        // PrintData();

        ValidateSortedData();
    }

    void GetIntermediateDataBack()
    {
        _sortedBlocksKeysData.GetData();
        _offsetsData.GetData();
        _sizesData.GetData();
        _sizesPrefixSumData.GetData();
    }

    void GetSortedDataBack()
    {
        _keys.GetData(_sortedKeysLocalData);
    }

    void PrintData()
    {
        Debug.Log("Unsorted data: " + Utils.ArrayToString(_unsortedKeysLocalData));
        Debug.Log("Sorted data: " + Utils.ArrayToString(_sortedKeysLocalData));
        Debug.Log("Offsets local data: " + Utils.ArrayToString(_offsetsData.LocalBuffer));
        Debug.Log("Sizes after scan: " + Utils.ArrayToString(_sizesData.LocalBuffer));
        Debug.Log("Sizes prefix sum after scan: " + Utils.ArrayToString(_sizesPrefixSumData.LocalBuffer));
    }

    void ValidateSortedData()
    {
        // does output sorted data actually sorted?
        StringBuilder s = new StringBuilder("");
        int countNonUniqueElements = 0;
        for (uint i = 1; i < _dataLength; i++)
        {
            
            if (_sortedKeysLocalData[i].CompareTo(_sortedKeysLocalData[i - 1])  < 0)
            {
                Debug.LogError("Output data has unsorted element on index " + i);
                return;
            }

            if (_sortedKeysLocalData[i].Equals(_sortedKeysLocalData[i - 1]))
            {
                s.Append($"{i}, ");
                countNonUniqueElements++;
            }
        }

        Debug.Log("Output data is sorted");
        if (countNonUniqueElements > 0)
        {
            s.Insert(0, $"Output data has {countNonUniqueElements} non-unique element on indices: ");
            Debug.Log(s.ToString());
        }
    }

    uint GetRadix(TKey value, int bitOffset)
    {
        if (value is uint)
        {
            return (Convert.ToUInt32(value) >> bitOffset) & (Constants.BUCKET_SIZE - 1);
        }        
        if (value is ulong)
        {
            return (uint)((Convert.ToUInt64(value) >> bitOffset) & (Constants.BUCKET_SIZE - 1));
        }

        throw new Exception();
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
            _debugDataDictionary[GetRadix(_sortedBlocksKeysData[i], bitOffset)]++;
        }

        for (uint i = 0; i < Constants.DATA_ARRAY_COUNT; i++)
        {
            _debugDataDictionary[GetRadix(_unsortedKeysLocalData[i], bitOffset)]--;
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
                _debugDataDictionary[GetRadix(_sortedBlocksKeysData[i * Constants.THREADS_PER_BLOCK + j], bitOffset)]++;
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
        for (var i = 1; i < _sizesData.LocalBuffer.Length; i++)
        {
            if (_sizesData.LocalBuffer[i] != _sizesLocalDataBeforeScan[i - 1] + _sizesData.LocalBuffer[i - 1])
            {
                Debug.LogError("Scan operation incorrect at index " + i + ": " + _sizesData.LocalBuffer[i] + " != " + _sizesLocalDataBeforeScan[i - 1] + " + " + _sizesData.LocalBuffer[i - 1]);
                hasSizesPrefixSumError = true;
                break;
            }
        }

        if (!hasSizesPrefixSumError)
        {
            Debug.Log($"Scan operation for bit offset {bitOffset} is correct");
        }
    }

    public void Dispose()
    {
        _sortedBlocksKeysData.Dispose();
        _sortedBlocksValuesData.Dispose();
        _offsetsData.Dispose();
        _sizesData.Dispose();
        _sizesPrefixSumData.Dispose();
    }
}