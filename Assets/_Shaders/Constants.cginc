#define RADIX 8
#define BUCKET_SIZE 256 // 2 ^ RADIX
#define BLOCK_SIZE 512
#define THREADS_PER_BLOCK 1024
#define WARP_SIZE 32

#define MAX_FLOAT 0x7F7FFFFF

struct AABB
{
    float3 min;
    float _dummy0;
    float3 max;
    float _dummy1;
};

#define INTERNAL_NODE 0
#define LEAF_NODE 1

struct InternalNode
{
    uint leftNode;
    uint leftNodeType; // TODO combine node types in one 4 byte word 
    uint rightNode;
    uint rightNodeType;
    uint parent;
    uint index;
};

struct LeafNode
{
    uint parent;
    uint index;
};

struct Triangle
{
    float3 a;
    float _dummy0;
    float3 b;
    float _dummy1;
    float3 c;
    float _dummy2;
};
