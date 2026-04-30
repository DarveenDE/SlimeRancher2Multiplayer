using System.Collections.Concurrent;
using MelonLoader;

namespace SR2MP.Shared.Utils;

[RegisterTypeInIl2Cpp(false)]
public sealed class MainThreadDispatcher : MonoBehaviour
{
    public static MainThreadDispatcher Instance { get; private set; }

    // ReSharper disable once InconsistentNaming
    private static readonly ConcurrentQueue<Action?> actionQueue = new();
    private static int queuedActionCount;

    private const int MaxActionsPerFrame = 256;
    private const int TimeCheckInterval = 32;
    private const float MaxFrameMilliseconds = 4f;
    private float nextBacklogLogTime;

    public static void Initialize()
    {
        if (Instance != null) return;

        var obj = new GameObject("SR2MP_MainThreadDispatcher");
        Instance = obj.AddComponent<MainThreadDispatcher>();
        DontDestroyOnLoad(obj);

        SrLogger.LogMessage("Main thread dispatcher initialized", SrLogTarget.Both);
    }

#pragma warning disable CA1822 // Mark members as static
    public void Update()
#pragma warning restore CA1822 // Mark members as static
    {
        var frameStart = Time.realtimeSinceStartup;
        var processed = 0;

        while (processed < MaxActionsPerFrame && actionQueue.TryDequeue(out var action))
        {
            if (System.Threading.Interlocked.Decrement(ref queuedActionCount) < 0)
                System.Threading.Volatile.Write(ref queuedActionCount, 0);

            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                SrLogger.LogError($"Error executing main thread action: {ex}", SrLogTarget.Both);
            }

            processed++;

            if (processed % TimeCheckInterval == 0
                && (Time.realtimeSinceStartup - frameStart) * 1000f >= MaxFrameMilliseconds)
            {
                break;
            }
        }

        var backlog = Math.Max(0, System.Threading.Volatile.Read(ref queuedActionCount));
        if (backlog > 0 && Time.realtimeSinceStartup >= nextBacklogLogTime)
        {
            nextBacklogLogTime = Time.realtimeSinceStartup + 5f;
            SrLogger.LogPacketSize($"Main thread dispatcher backlog: {backlog} action(s)", SrLogTarget.Both);
        }
    }

    public static void Enqueue(Action? action)
    {
        if (action == null)
            return;

        System.Threading.Interlocked.Increment(ref queuedActionCount);
        actionQueue.Enqueue(action);
    }

    public static void Clear()
    {
        while (actionQueue.TryDequeue(out _)) { }
        System.Threading.Volatile.Write(ref queuedActionCount, 0);
        SrLogger.LogPacketSize("Main thread dispatcher queue cleared", SrLogTarget.Both);
    }

#pragma warning disable CA1822 // Mark members as static
    public void OnDestroy() => Instance = null!;
#pragma warning restore CA1822 // Mark members as static
}
