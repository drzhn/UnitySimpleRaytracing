using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class _debugRayBoxIntersectionTester : MonoBehaviour
{
    [SerializeField] private Vector3 _boxMin;
    [SerializeField] private Vector3 _boxMax;
    private Transform _box;
    private Transform _ray;

    void Start()
    {
        _box = new GameObject("Box").transform;
        _ray = new GameObject("Ray").transform;
        _ray.position = new Vector3(0, 0, 40);
    }

    private struct Box
    {
        public Vector3 min;
        public Vector3 max;
    }

    private struct Ray
    {
        public Vector3 origin;
        public Vector3 dir;
        public Vector3 inv_dir;
    }

    bool RayBoxIntersection(Box b, Ray r)
    {
        Vector3 t1 = Vector3.Scale(b.min - r.origin, r.inv_dir);
        Vector3 t2 = Vector3.Scale(b.max - r.origin, r.inv_dir);

        Vector3 tmin1 = Vector3.Min(t1, t2);
        Vector3 tmax1 = Vector3.Max(t1, t2);

        float tmin = Mathf.Max(tmin1.x, tmin1.y, tmin1.z);
        float tmax = Mathf.Min(tmax1.x, tmax1.y, tmax1.z);

        return tmax > tmin;
    }

    void Update()
    {
        Box box = new Box()
        {
            min = _boxMin,
            max = _boxMax
        };
        Ray ray = new Ray()
        {
            origin = _ray.position,
            dir = _ray.forward,
            inv_dir = new Vector3(1 / _ray.forward.x, 1 / _ray.forward.y, 1 / _ray.forward.z)
        };
        if (RayBoxIntersection(box, ray))
        {
            Debug.DrawLine(_ray.position, _ray.position + _ray.forward * 10f, Color.red);
        }
        else
        {
            Debug.DrawLine(_ray.position, _ray.position + _ray.forward * 10f, Color.blue);
        }
    }

    private void OnDrawGizmos()
    {
        if (_box == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube((_boxMax + _boxMin) / 2, _boxMax - _boxMin);
    }
}