using System.Net;
using SR2MP.Packets.FX;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.PlayerFX)]
public sealed class PlayerFXHandler : BasePacketHandler<PlayerFXPacket>
{
    public PlayerFXHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(PlayerFXPacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        if (!IsPlayerSoundDictionary.TryGetValue(packet.FX, out var isSound))
            return;

        if (!isSound)
        {
            if (!fxManager.PlayerFXMap.TryGetValue(packet.FX, out var fxPrefab) || !fxPrefab)
                return;

            RunWithHandlingPacket(() => FXHelpers.SpawnAndPlayFX(fxPrefab, packet.Position, Quaternion.identity));
        }
        else
        {
            packet.Player = client.PlayerId;

            if (!playerObjects.TryGetValue(packet.Player, out var playerObject) || !playerObject)
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

        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
