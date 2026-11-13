
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
    Color pink = new Color(1f, 0.76f, 0.83f);
    Color lightPink = new Color(1f, 0.91f, 0.94f);
    Color darkPink = new Color(0.78f, 0.22f, 0.43f);
    Color textColour = new Color(0.30f, 0.16f, 0.22f);
    Color white = Color.white;

    // Main background
    GUI.color = pink;
    GUI.DrawTexture(
        new Rect(15, 15, 440, 680),
        Texture2D.whiteTexture
    );

    // Header panel
    GUI.color = darkPink;
    GUI.DrawTexture(
        new Rect(15, 15, 440, 65),
        Texture2D.whiteTexture
    );

    GUI.color = white;

    GUIStyle heading = new GUIStyle(GUI.skin.label);
    heading.fontSize = 21;
    heading.fontStyle = FontStyle.Bold;
    heading.normal.textColor = white;

    GUI.Label(
        new Rect(30, 32, 410, 35),
        "PARALLEL IMAGE PROCESSOR",
        heading
    );

    GUIStyle section = new GUIStyle(GUI.skin.label);
    section.fontSize = 15;
    section.fontStyle = FontStyle.Bold;
    section.normal.textColor = darkPink;

    GUIStyle normal = new GUIStyle(GUI.skin.label);
    normal.fontSize = 13;
    normal.normal.textColor = textColour;

    GUIStyle result = new GUIStyle(normal);
    result.fontSize = 17;
    result.fontStyle = FontStyle.Bold;
    result.normal.textColor = darkPink;

    // Image panel
    GUI.color = lightPink;
    GUI.DrawTexture(
        new Rect(30, 95, 410, 265),
        Texture2D.whiteTexture
    );

    GUI.color = darkPink;
    GUI.Label(new Rect(45, 105, 380, 25),
        "IMAGE ANALYSIS", section);

    GUI.color = white;

    if (previewTexture != null)
    {
        GUI.DrawTexture(
            new Rect(45, 140, 180, 180),
            previewTexture,
            ScaleMode.ScaleToFit
        );
    }

    GUI.color = textColour;

    GUI.Label(new Rect(240, 160, 185, 25),
        $"Resolution: {previewTexture.width} x {previewTexture.height}", normal);

    GUI.Label(new Rect(240, 195, 185, 25),
        $"Pixels: {pixelCount:N0}", normal);

    // Red channel panel
    GUI.color = lightPink;
    GUI.DrawTexture(
        new Rect(30, 375, 410, 85),
        Texture2D.whiteTexture
    );

    GUI.color = darkPink;
    GUI.Label(new Rect(45, 385, 380, 25),
        "RED CHANNEL ANALYSIS", section);

    GUI.Label(new Rect(45, 420, 380, 30),
        $"Red channel sum: {redChannelSum:N0}", result);

    // Performance panel
    GUI.color = lightPink;
    GUI.DrawTexture(
        new Rect(30, 475, 410, 115),
        Texture2D.whiteTexture
    );

    GUI.color = darkPink;
    GUI.Label(new Rect(45, 485, 380, 25),
        "PERFORMANCE BENCHMARK", section);

    GUI.color = textColour;

    GUI.Label(new Rect(45, 520, 380, 25),
        $"Single-threaded: {singleAverage:F4} ms", normal);

    GUI.Label(new Rect(45, 550, 380, 25),
        $"Jobs + Burst: {parallelAverage:F4} ms", normal);

    GUI.color = darkPink;

    GUI.Label(new Rect(45, 600, 380, 30),
        $"Speedup: {speedup:F2}x", result);

    // Benchmark button
    GUI.color = darkPink;

    GUIStyle button = new GUIStyle(GUI.skin.button);
    button.fontSize = 14;
    button.fontStyle = FontStyle.Bold;
    button.normal.textColor = white;
    button.hover.textColor = white;
    button.normal.background = Texture2D.whiteTexture;

    if (GUI.Button(
        new Rect(30, 645, 410, 40),
        "RUN BENCHMARK AGAIN",
        button))
    {
        RunBenchmark();
    }

    GUI.color = Color.white;
}



    void OnDestroy()
    {
        if (previewTexture != null)
            Destroy(previewTexture);
    }
}
