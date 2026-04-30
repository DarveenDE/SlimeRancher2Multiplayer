using System.Net;
using SR2MP.Packets.Utils;

namespace SR2MP.Server.Models;

public sealed class ClientInfo
{
    public sealed class QueuedPacket
    {
        public byte[] Data { get; }
        public PacketReliability Reliability { get; }

        public QueuedPacket(byte[] data, PacketReliability reliability)
        {
            Data = data;
            Reliability = reliability;
        }
    }

    private const int MaxQueuedInitialSyncPackets = 2048;
    private readonly Queue<QueuedPacket> initialSyncQueue = new();
    private readonly object initialSyncLock = new();

    public IPEndPoint EndPoint { get; set; }
    private DateTime LastHeartbeat { get; set; }
    public string PlayerId { get; set; }
    public bool InitialSyncComplete { get; private set; }

    public ClientInfo(IPEndPoint endPoint, string playerId = "")
    {
        EndPoint = endPoint;
        LastHeartbeat = DateTime.UtcNow;
        PlayerId = playerId;
        InitialSyncComplete = false;
    }

    public void UpdateHeartbeat() => LastHeartbeat = DateTime.UtcNow;

    public bool IsTimedOut()
        => (DateTime.UtcNow - LastHeartbeat).TotalSeconds > 30;

    public string GetClientInfo() => $"{EndPoint.Address}:{EndPoint.Port}";

    public bool QueueUntilInitialSyncComplete(byte[] data, PacketReliability reliability)
    {
        if (reliability == PacketReliability.Unreliable)
            return false;

        lock (initialSyncLock)
        {
            if (InitialSyncComplete)
                return false;

            if (initialSyncQueue.Count >= MaxQueuedInitialSyncPackets)
            {
                SrLogger.LogWarning($"Initial sync queue full for {PlayerId}; dropping packet type={data[0]}", SrLogTarget.Both);
                return true;
            }

            initialSyncQueue.Enqueue(new QueuedPacket(data, reliability));
            return true;
        }
    }

    public List<QueuedPacket> MarkInitialSyncComplete()
    {
        lock (initialSyncLock)
        {
            InitialSyncComplete = true;
            var queuedPackets = initialSyncQueue.ToList();
            initialSyncQueue.Clear();
            return queuedPackets;
        }
    }
}
