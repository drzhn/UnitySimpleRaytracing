# UnitySimpleRaytracing

Software raytracing implementation on the GPU (BVH building and traversal). Used LBVH+radix sort on the spatial subdivision part.

Based on these articles
- N. Satish, M. Harris and M. Garland, "Designing efficient sorting algorithms for manycore GPUs," 2009 IEEE International Symposium on Parallel & Distributed Processing, Rome, Italy, 2009, pp. 1-10
- https://developer.nvidia.com/blog/thinking-parallel-part-iii-tree-construction-gpu/

**WARNING**: for GPU sorting part I used new HLSL wave intrinsics for scan stage. So it's obligation to run this project on Nvidia GPUs because of lane size equal to 32. 
