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
    private BVHConstructor _bvhConstructor;
    private RenderTexture _renderTexture;
    private Material _imageComposerMaterial;
    private static readonly int ObjectTexture = Shader.PropertyToID("_ObjectTexture");


    void Awake()
    {
        // _container = new MeshBufferContainer(_mesh);
        // _sorter = new ComputeBufferSorter(_container.Keys, _container.TriangleIndex, _shaderContainer);
        //
        // _sorter.Sort();

        _bvhConstructor = new BVHConstructor(_shaderContainer);

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
        // _sorter.Dispose();
        // _container.Dispose();
        _bvhConstructor.Dispose();
    }
}