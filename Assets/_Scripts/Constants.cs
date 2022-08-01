public static class Constants
{
    public const int THREADS_PER_BLOCK = 1024;
    public const int BLOCK_SIZE = 512;
    public const int ELEM_PER_THREAD = 1; // TODO later
    public const int DATA_ARRAY_COUNT = ELEM_PER_THREAD * THREADS_PER_BLOCK * BLOCK_SIZE;

    public const int RADIX = 8;
    public const int BUCKET_SIZE = 1 << 8;
}