namespace WorkerService.Helpers;

public static class SizeConverterHelper
{
    public static float BytesToKilobytes(long size)
    {
        return size / 1024f;
    }

    public static float BytesToMegabytes(long size)
    {
        return BytesToKilobytes(size) / 1024f;
    }

    public static float BytesToGigabytes(long size)
    {
        return BytesToMegabytes(size) / 1024f;
    }
}