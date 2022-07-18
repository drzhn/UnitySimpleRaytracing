// we will only process 16*1024*1024 elements, 1024 elems per thread
#define RADIX 8
#define BUCKET_SIZE 256 // 2 ^ RADIX
#define BLOCK_SIZE 512
#define THREADS_PER_BLOCK 1024
#define WARP_SIZE 32