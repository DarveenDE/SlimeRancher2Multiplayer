using SR2MP.Packets.FX;
using SR2MP.Shared.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.PlayerFX)]
public sealed class PlayerFXHandler : BaseClientPacketHandler<PlayerFXPacket>
{
    public PlayerFXHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(PlayerFXPacket packet)
    {
        if (!IsPlayerSoundDictionary.TryGetValue(packet.FX, out var isSound))
            return;

        try
        {
            if (!isSound)
            {
                if (fxManager.PlayerFXMap == null)
                    return;

                if (!fxManager.PlayerFXMap.TryGetValue(packet.FX, out var fxPrefab) || !fxPrefab)
                    return;

                RunWithHandlingPacket(() => FXHelpers.SpawnAndPlayFX(fxPrefab, packet.Position, Quaternion.identity));
                return;
            }

            if (!playerObjects.TryGetValue(packet.Player, out var playerObject) || !playerObject)
                return;

            if (fxManager.PlayerAudioCueMap == null)
                return;

            if (!fxManager.PlayerAudioCueMap.TryGetValue(packet.FX, out var cue) || !cue)
                return;

            var volume = PlayerSoundVolumeDictionary.TryGetValue(packet.FX, out var configuredVolume)
                ? configuredVolume
                : 1f;

            var isTransient = ShouldPlayerSoundBeTransientDictionary.TryGetValue(packet.FX, out var transient)
                && transient;

            if (isTransient)
            {
                RemoteFXManager.PlayTransientAudio(cue, playerObject.transform.position, volume);
            }
            else
            {
                var playerAudio = playerObject.GetComponent<SECTR_PointSource>();
                if (!playerAudio)
                    return;

                var shouldLoop = DoesPlayerSoundLoopDictionary.TryGetValue(packet.FX, out var loop)
                    && loop;

                RunWithHandlingPacket(() =>
                {
                playerAudio.Cue = cue;
                    playerAudio.Loop = shouldLoop;

                    playerAudio.instance.Volume = volume;
                    playerAudio.Play();
                });
            }
        }
        catch
        {
            // SrLogger.LogWarning($"This \"error\" is NOT serious, DO NOT REPORT IT!\n{ex}");
        }
    }
}
