
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct RedChannelJob : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<Color32> pixels;

    public NativeArray<long> partialSums;

    public int pixelsPerChunk;

    public void Execute(int index)
    {
        int start = index * pixelsPerChunk;
        int end = Mathf.Min(start + pixelsPerChunk, pixels.Length);

        long sum = 0;

        for (int i = start; i < end; i++)
        {
            sum += pixels[i].r;
        }

        partialSums[index] = sum;
    }
}

