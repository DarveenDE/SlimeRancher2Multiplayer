using System.Net;
using System.Reflection;
using SR2MP.Packets.Utils;
using SR2MP.Packets.Internal;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Server.Managers;

public sealed class PacketManager
{
    private readonly Dictionary<byte, IPacketHandler> handlers = new();
    private readonly NetworkManager networkManager;
    private readonly ClientManager clientManager;

    public PacketManager(NetworkManager networkManager, ClientManager clientManager)
    {
        this.networkManager = networkManager;
        this.clientManager = clientManager;
    }

    public void RegisterHandlers()
    {
        RegisterAuthorityRules();
        RegisterSubsystems();

        var handlerTypes = Main.Core.GetTypes()
            .Where(type => type.GetCustomAttribute<PacketHandlerAttribute>() != null
                        && typeof(IPacketHandler).IsAssignableFrom(type)
                        && !type.IsAbstract);

        foreach (var type in handlerTypes)
        {
            var attribute = type.GetCustomAttribute<PacketHandlerAttribute>();
            if (attribute == null) continue;

            try
            {
                if (Activator.CreateInstance(type, networkManager, clientManager) is IPacketHandler handler)
                {
                    handlers[attribute.PacketType] = handler;
                    SrLogger.LogMessage($"Registered server handler: {type.Name} for packet type {attribute.PacketType}", SrLogTarget.Both);
                }
            }
            catch (Exception ex)
            {
                SrLogger.LogError($"Failed to register handler {type.Name}: {ex}", SrLogTarget.Both);
            }
        }

        SrLogger.LogMessage($"Total handlers registered: {handlers.Count}", SrLogTarget.Both);
    }

    public void HandlePacket(byte[] data, IPEndPoint clientEp)
    {
        if (data.Length < 10)
        {
            SrLogger.LogWarning($"Received packet too small for chunk header: {data.Length} bytes", SrLogTarget.Both);
            return;
        }

        byte packetType = data[0];
        ushort chunkIndex = (ushort)(data[1] | (data[2] << 8));
        ushort totalChunks = (ushort)(data[3] | (data[4] << 8));
        ushort packetId = (ushort)(data[5] | (data[6] << 8));
        PacketReliability reliability = (PacketReliability)data[7];
        ushort sequenceNumber = (ushort)(data[8] | (data[9] << 8));

        bool isAckPacket = packetType == (byte)PacketType.ReservedAck;
        bool isConnectPacket = packetType == (byte)PacketType.Connect;
        bool isKnownClient = clientManager.TryGetClient(clientEp, out _);

        if (!PacketAuthority.CanClientSendToServer(packetType))
        {
            SrLogger.LogWarning(
                $"Rejected client packet from {clientEp}: {PacketAuthority.FormatPacketType(packetType)} is {PacketAuthority.GetClientToServerRule(packetType)} and cannot be sent to the host.",
                SrLogTarget.Both);
            return;
        }

        if (!isKnownClient && !isConnectPacket && !isAckPacket)
        {
            SrLogger.LogWarning(
                $"Ignored packet from unknown client {clientEp}: {PacketAuthority.FormatPacketType(packetType)}",
                SrLogTarget.Both);
            return;
        }

        byte[] chunkData = new byte[data.Length - 10];
        Buffer.BlockCopy(data, 10, chunkData, 0, chunkData.Length);
        // Buffer.BlockCopy(data, 10, chunkData, 0, data.Length - 10);

        string senderKey = clientEp.ToString();

        if (!PacketChunkManager.TryMergePacket((PacketType)packetType, chunkData, chunkIndex,
            totalChunks, packetId, senderKey, reliability, sequenceNumber,
            out data, out var packetReliability, out var packetSequenceNumber))
            return;

        if (data.Length == 0 || data[0] != packetType)
        {
            SrLogger.LogWarning(
                $"Packet type mismatch from {clientEp}: header={packetType}, payload={(data.Length > 0 ? data[0] : -1)}",
                SrLogTarget.Both);
            return;
        }

        // Handle reliability ACK packets
        if (isAckPacket)
        {
            var ackPacket = new AckPacket();
            using (var reader = new PacketReader(data))
            {
                reader.Skip(1);
                ackPacket.Deserialise(reader);
            }

            networkManager.HandleAck(clientEp, ackPacket.PacketId, ackPacket.OriginalPacketType);
            return;
        }

        if (packetReliability == PacketReliability.ReliableOrdered)
        {
            var (bufResult, toProcess) = networkManager.AcceptOrderedPacketWithBuffer(
                clientEp, packetSequenceNumber, packetType, data);

            switch (bufResult)
            {
                case StreamReceiveResult.Duplicate:
                    SendAck(clientEp, packetId, packetType, isConnectPacket);
                    return;
                case StreamReceiveResult.Buffered:
                    // ACK so the sender doesn't resend; dispatch deferred until gap fills.
                    SendAck(clientEp, packetId, packetType, isConnectPacket);
                    return;
            }

            // Delivered: ACK then dispatch this packet plus any newly-unlocked buffered packets.
            SendAck(clientEp, packetId, packetType, isConnectPacket);
            var capturedEp = clientEp;
            foreach (var packetData in toProcess!)
            {
                var captured = packetData;
                if (handlers.TryGetValue(captured[0], out var h))
                    MainThreadDispatcher.Enqueue(() => h.Handle(captured, capturedEp));
                else
                    SrLogger.LogWarning($"No handler found for buffered packet type: {captured[0]}");
            }
            return;
        }

        // Always ACK reliable packets (even duplicates)
        // Otherwise clients will resend if the ACK packet was lost
        if (packetReliability != PacketReliability.Unreliable)
        {
            SendAck(clientEp, packetId, packetType, isConnectPacket);
        }

        // Packet deduplication (per client)
        var packetTypeKey = ((PacketType)packetType).ToString();
        var uniqueId = $"{senderKey}_{packetId}";

        if (PacketDeduplication.IsDuplicate(packetTypeKey, uniqueId))
        {
            SrLogger.LogPacketSize($"Duplicate packet ignored from {senderKey}: {packetTypeKey} (packetId={packetId})", SrLogTarget.Both);
            return;
        }

        if (handlers.TryGetValue(packetType, out var handler))
        {
            try
            {
                MainThreadDispatcher.Enqueue(() => handler.Handle(data, clientEp));
            }
            catch (Exception ex)
            {
                SrLogger.LogError($"Error handling packet type {packetType}: {ex}", SrLogTarget.Both);
            }
        }
        else
        {
            SrLogger.LogWarning($"No handler found for packet type: {packetType}");
        }
    }

    private void SendAck(IPEndPoint clientEp, ushort packetId, byte packetType, bool allowUnknownClient = false)
    {
        if (!allowUnknownClient && !clientManager.TryGetClient(clientEp, out _))
            return;

        var ackPacket = new AckPacket
        {
            PacketId = packetId,
            OriginalPacketType = packetType
        };

        using var writer = new PacketWriter();
        writer.WritePacket(ackPacket);

        // no need to acknowledge ACK packets
        networkManager.Send(writer.ToArray(), clientEp, PacketReliability.Unreliable);
    }

    /// <summary>
    /// Populates <see cref="AuthorityPipeline.Instance"/> with one rule per packet type
    /// that has non-trivial authority requirements.  Called once at server startup before
    /// any packets are processed.
    ///
    /// Adding a rule here is the *single* place to record who may send what —
    /// no per-handler ownership checks are needed after this.
    /// </summary>
    private static void RegisterAuthorityRules()
    {
        var pipeline = AuthorityPipeline.Instance;

        // ActorSpawn: the spawned actor ID must lie in the range assigned to the sender.
        pipeline.Register(PacketType.ActorSpawn, new RangeOnlyRule());

        // ActorUpdate: only the registered owner may stream updates (throttled log).
        pipeline.Register(PacketType.ActorUpdate, new OwnerOnlyRule(rejectionLogThrottleSeconds: 5f));

        // ActorDestroy: gadgets are shared (any client); regular actors require ownership.
        pipeline.Register(PacketType.ActorDestroy, new SharedGadgetOrOwnerRule());

        // ActorTransfer: current owner must be the host or the requesting client.
        pipeline.Register(PacketType.ActorTransfer, new OwnerOrHostRule());

        // ActorUnload: only the registered owner may unload.
        pipeline.Register(PacketType.ActorUnload, new OwnerOnlyRule());

        // ActorFeral: only the registered owner may declare their slime feral.
        pipeline.Register(PacketType.ActorFeral, new OwnerOnlyRule());

        // CurrencyAdjust: client must send a current baseline; stale requests are rejected.
        pipeline.Register(PacketType.CurrencyAdjust, new CurrencyBaselineRule());

        SrLogger.LogMessage("Authority rules registered.", SrLogTarget.Main);
    }

    /// <summary>
    /// Registers all <see cref="ISyncedSubsystem"/> implementations with
    /// <see cref="SubsystemRegistry.Instance"/>.  Called once at server startup
    /// before any Initial-Sync begins.
    /// </summary>
    private static void RegisterSubsystems()
    {
        var registry = SR2MP.Shared.Sync.SubsystemRegistry.Instance;
        registry.Register(SR2MP.Shared.Sync.MapUnlockSubsystem.Instance);
        registry.Register(SR2MP.Shared.Sync.CommStationSubsystem.Instance);
        registry.Register(SR2MP.Shared.Sync.PuzzleStateSubsystem.Instance);
        registry.Register(SR2MP.Shared.Sync.LandPlotsSubsystem.Instance);
        registry.Register(SR2MP.Shared.Sync.GardenResourceAttachSubsystem.Instance);
        registry.Register(SR2MP.Shared.Sync.RefinerySubsystem.Instance);
        registry.Register(SR2MP.Shared.Sync.ResourceNodeSubsystem.Instance);
    }
}
