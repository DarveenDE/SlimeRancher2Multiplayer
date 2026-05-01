using SR2MP.Packets.FX;
using SR2MP.Shared.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.WorldFX)]
public sealed class WorldFXHandler : BaseClientPacketHandler<WorldFXPacket>
{
    public WorldFXHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(WorldFXPacket packet)
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
            catch { }
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
    }
}
