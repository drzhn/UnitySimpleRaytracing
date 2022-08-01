using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class RaytracingMeshDrawer : MonoBehaviour
{
    [SerializeField] private ComputeShader _objectDrawer;
    [SerializeField] private Shader _imageComposer;
    [SerializeField] private Mesh _mesh;
    [SerializeField] private ShaderContainer _shaderContainer; 
    

    private int _objectDrawerKernel;

    private MeshBufferContainer _container;
    private ComputeBufferSorter _sorter;
    private RenderTexture _renderTexture;
    private Material _imageComposerMaterial;
    private static readonly int ObjectTexture = Shader.PropertyToID("_ObjectTexture");


    struct Range
    {
        public int start;
        public int end;

        public override string ToString()
        {
            return $"{start}, {end}";
        }
    }

    int sign(int x)
    {
        return (x > 0) ? 1 : -1;
    }

    int clz(uint v)
    {
        int ret = 0;
        for (int i = 31; i >= 0; i--)
        {
            if (((v >> i) & 1) == 0)
            {
                ret++;
            }
            else
            {
                break;
            }
        }

        return ret;
    }

    int delta(uint[] array, int x, int y, int numObjects)
    {
        if (x >= 0 && x <= numObjects - 1 && y >= 0 && y <= numObjects - 1)
        {
            return clz(array[x] ^ array[y]);
        }

        return -1;
    }

    Range DetermineRange(uint[] array, int numObjects, int idx)
    {
        int d = sign(delta(array, idx, idx + 1, numObjects) - delta(array, idx, idx - 1, numObjects));
        int dmin = delta(array, idx, idx - d, numObjects);
        int lmax = 2;
        while (delta(array, idx, idx + lmax * d, numObjects) > dmin)
            lmax = lmax * 2;
        int l = 0;
        for (int t = lmax / 2; t >= 1; t /= 2)
        {
            if (delta(array, idx, idx + (l + t) * d, numObjects) > dmin)
                l += t;
        }

        int j = idx + l * d;
        Range range = new Range
        {
            start = Math.Min(idx, j),
            end = Math.Max(idx, j)
        };

        return range;
    }

    int FindSplit(uint[] array,
        int first,
        int last)
    {
        // Identical Morton codes => split the range in the middle.

        uint firstCode = array[first];
        uint lastCode = array[last];

        if (firstCode == lastCode)
            return (first + last) >> 1;

        // Calculate the number of highest bits that are the same
        // for all objects, using the count-leading-zeros intrinsic.

        int commonPrefix = clz(firstCode ^ lastCode);

        // Use binary search to find where the next bit differs.
        // Specifically, we are looking for the highest object that
        // shares more than commonPrefix bits with the first one.

        int split = first; // initial guess
        int step = last - first;

        do
        {
            step = (step + 1) >> 1; // exponential decrease
            int newSplit = split + step; // proposed new position

            if (newSplit < last)
            {
                uint splitCode = array[newSplit];
                int splitPrefix = clz(firstCode ^ splitCode);
                if (splitPrefix > commonPrefix)
                    split = newSplit; // accept proposal
            }
        } while (step > 1);

        return split;
    }

    void Awake()
    {
        // uint[] array = new uint[]
        // {
        //     1, 2, 4, 5, 19, 24, 25, 30
        // };
        //
        // for (var i = 0; i < array.Length - 1; i++)
        // {
        //     Range d = DetermineRange(array, array.Length, i);
        //     print(d + " - " + FindSplit(array, d.start, d.end));
        // }

        _container = new MeshBufferContainer(_mesh);
        _sorter = new ComputeBufferSorter(_container.Keys, _container.TriangleIndex, _shaderContainer);
        
        _sorter.Sort();

        _renderTexture = new RenderTexture(1024, 1024, GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormat.D32_SFloat);
        _renderTexture.enableRandomWrite = true;

        _objectDrawerKernel = _objectDrawer.FindKernel("ObjectDrawer");
        _objectDrawer.SetInt("screenWidth", Screen.width);
        _objectDrawer.SetInt("screenHeight", Screen.height);
        _objectDrawer.SetTexture(_objectDrawerKernel, "_texture", _renderTexture);

        _imageComposerMaterial = new Material(_imageComposer);
        _imageComposerMaterial.SetTexture(ObjectTexture, _renderTexture);
    }

    private void Start()
    {
        _objectDrawer.Dispatch(_objectDrawerKernel, 32, 32, 1);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(src, dest, _imageComposerMaterial);
    }

    private void OnDestroy()
    {
        _sorter.Dispose();
        _container.Dispose();
    }
}