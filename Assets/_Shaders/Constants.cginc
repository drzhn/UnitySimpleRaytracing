// we will only process 512*1024 elements, 1024 elems per thread
#define RADIX 8
#define BUCKET_SIZE 256 // 2 ^ RADIX
#define BLOCK_SIZE 512
#define THREADS_PER_BLOCK 1024
#define WARP_SIZE 32

struct AABB
{
    float4 min;
    float4 max;
};

struct Triangle
{
    float4 a;
    float4 b;
    float4 c;
};
