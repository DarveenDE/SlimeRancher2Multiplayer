using System.Net;
using SR2MP.Packets.FX;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.WorldFX)]
public sealed class WorldFXHandler : BasePacketHandler<WorldFXPacket>
{
    public WorldFXHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(WorldFXPacket packet, IPEndPoint clientEp)
    {
        if (!IsWorldSoundDictionary.TryGetValue(packet.FX, out var isSound))
            return;

        if (!isSound)
        {
            if (fxManager.WorldFXMap == null)
                return;

            if (!fxManager.WorldFXMap.TryGetValue(packet.FX, out var fxPrefab) || !fxPrefab)
                return;

            try { RunWithHandlingPacket(() => FXHelpers.SpawnAndPlayFX(fxPrefab, packet.Position, Quaternion.identity)); }
            catch { return; }
        }
        else
        {
            if (fxManager.WorldAudioCueMap == null)
                return;

            if (!fxManager.WorldAudioCueMap.TryGetValue(packet.FX, out var cue) || !cue)
                return;

            var volume = WorldSoundVolumeDictionary.TryGetValue(packet.FX, out var configuredVolume)
                ? configuredVolume
                : 1f;

            RemoteFXManager.PlayTransientAudio(cue, packet.Position, volume);
        }

        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
