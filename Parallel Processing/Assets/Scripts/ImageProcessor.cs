
using System.IO;
using System.Diagnostics;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

public class ImageProcessor : MonoBehaviour
{
    public string imageName = "test_image.png";

    const int CHUNK_SIZE = 1000;
    const int BATCH_SIZE = 32;
    const int WARMUP_RUNS = 5;
    const int TEST_RUNS = 20;

    Texture2D previewTexture;

    long redChannelSum;
    double singleAverage;
    double parallelAverage;
    float speedup;
    int pixelCount;

    bool benchmarkComplete;

    void Start()
    {
        RunBenchmark();
    }

    public void RunBenchmark()
    {
        benchmarkComplete = false;

        string path = Path.Combine(
            Application.streamingAssetsPath,
            imageName
        );

        byte[] imageData = File.ReadAllBytes(path);

        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(imageData);

        if (previewTexture != null)
            Destroy(previewTexture);

        previewTexture = texture;

        Color32[] pixels = texture.GetPixels32();

        pixelCount = pixels.Length;

        UnityEngine.Debug.Log(
            $"Loaded image: {texture.width} x {texture.height}"
        );

        int chunkCount =
            (pixels.Length + CHUNK_SIZE - 1) / CHUNK_SIZE;

        NativeArray<Color32> nativePixels =
            new NativeArray<Color32>(pixels, Allocator.TempJob);

        NativeArray<long> partialSums =
            new NativeArray<long>(chunkCount, Allocator.TempJob);

        try
        {
            RedChannelJob job = new RedChannelJob
            {
                pixels = nativePixels,
                partialSums = partialSums,
                pixelsPerChunk = CHUNK_SIZE
            };

            // Warm-up
            for (int i = 0; i < WARMUP_RUNS; i++)
            {
                CalculateSingleThreaded(pixels);

                JobHandle warmupHandle =
                    job.Schedule(chunkCount, BATCH_SIZE);

                warmupHandle.Complete();
            }

            // Single-threaded benchmark
            Stopwatch singleTimer = Stopwatch.StartNew();

            long singleSum = 0;

            for (int i = 0; i < TEST_RUNS; i++)
            {
                singleSum = CalculateSingleThreaded(pixels);
            }

            singleTimer.Stop();

            singleAverage =
                singleTimer.Elapsed.TotalMilliseconds / TEST_RUNS;

            // Parallel benchmark
            Stopwatch parallelTimer = Stopwatch.StartNew();

            long parallelSum = 0;

            for (int i = 0; i < TEST_RUNS; i++)
            {
                JobHandle handle =
                    job.Schedule(chunkCount, BATCH_SIZE);

                handle.Complete();

                parallelSum = 0;

                for (int j = 0; j < chunkCount; j++)
                {
                    parallelSum += partialSums[j];
                }
            }

            parallelTimer.Stop();

            parallelAverage =
                parallelTimer.Elapsed.TotalMilliseconds / TEST_RUNS;

            redChannelSum = singleSum;

            speedup = parallelAverage > 0
                ? (float)(singleAverage / parallelAverage)
                : 0;

            // Console output
            UnityEngine.Debug.Log("===== BENCHMARK RESULTS =====");

            UnityEngine.Debug.Log($"Image pixels: {pixelCount:N0}");
            UnityEngine.Debug.Log($"Test iterations: {TEST_RUNS}");

            UnityEngine.Debug.Log(
                $"Single-threaded average: {singleAverage:F4} ms"
            );

            UnityEngine.Debug.Log(
                $"Jobs + Burst average: {parallelAverage:F4} ms"
            );

            UnityEngine.Debug.Log($"Single-threaded sum: {singleSum}");
            UnityEngine.Debug.Log($"Parallel sum: {parallelSum}");

            UnityEngine.Debug.Log(
                $"Results match: {singleSum == parallelSum}"
            );

            UnityEngine.Debug.Log($"Speedup ratio: {speedup:F2}x");

            benchmarkComplete = singleSum == parallelSum;
        }
        finally
        {
            nativePixels.Dispose();
            partialSums.Dispose();
        }
    }

    long CalculateSingleThreaded(Color32[] pixels)
    {
        long sum = 0;

        foreach (Color32 pixel in pixels)
        {
            sum += pixel.r;
        }

        return sum;
    }

    void OnGUI()
    {
        GUI.Box(new Rect(15, 15, 440, 680),
            "PARALLEL IMAGE PROCESSOR");

        GUILayout.BeginArea(new Rect(35, 50, 400, 630));

        GUI.skin.label.fontSize = 14;

        GUILayout.Label("IMAGE ANALYSIS");
        GUILayout.Space(10);

        if (previewTexture != null)
        {
            GUILayout.Label(
                previewTexture,
                GUILayout.Width(220),
                GUILayout.Height(220)
            );

            GUILayout.Label(
                $"Resolution: {previewTexture.width} x {previewTexture.height}"
            );
        }

        GUILayout.Label($"Total pixels: {pixelCount:N0}");

        GUILayout.Space(20);

        GUILayout.Label("RED CHANNEL ANALYSIS");

        GUILayout.Label($"Red channel sum: {redChannelSum:N0}");

        GUILayout.Space(20);

        GUILayout.Label("PERFORMANCE BENCHMARK");

        GUILayout.Label(
            $"Single-threaded: {singleAverage:F4} ms"
        );

        GUILayout.Label(
            $"Jobs + Burst: {parallelAverage:F4} ms"
        );

        GUILayout.Space(10);

        GUILayout.Label($"Speedup: {speedup:F2}x");

        GUILayout.Space(15);

        GUILayout.Label(
            benchmarkComplete
                ? "STATUS: BOTH RESULTS MATCH"
                : "STATUS: CHECK RESULTS"
        );

        GUILayout.Space(20);

        if (GUILayout.Button("RUN BENCHMARK AGAIN",
            GUILayout.Height(40)))
        {
            RunBenchmark();
        }

        GUILayout.EndArea();
    }

    void OnDestroy()
    {
        if (previewTexture != null)
            Destroy(previewTexture);
    }
}
