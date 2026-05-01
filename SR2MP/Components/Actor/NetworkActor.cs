using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.Regions;
using Il2CppMonomiPark.SlimeRancher.Slime;
using System.Collections;
using Il2CppInterop.Runtime.Attributes;
using Il2CppMonomiPark.SlimeRancher.Player.CharacterController;
using Il2CppMonomiPark.SlimeRancher.World;
using MelonLoader;
using SR2MP.Packets.Actor;
using SR2MP.Shared.Utils;
using Unity.Mathematics;

using Delegate = Il2CppSystem.Delegate;
using Type = Il2CppSystem.Type;

namespace SR2MP.Components.Actor;

[RegisterTypeInIl2Cpp(false)]
public sealed class NetworkActor : MonoBehaviour
{
    internal RegionMember regionMember;
    private Identifiable identifiable;
    private Rigidbody rigidbody;
    private SlimeEmotions emotions;
    private readonly List<AuthoritySensitiveBehaviourState> authoritySensitiveBehaviours = new();
    private bool authoritySensitiveBehavioursCached;

    private float syncTimer = Timers.ActorTimer;
    public Vector3 SavedVelocity { get; internal set; }

    private const float PositionUpdateThresholdSqr = 0.0025f;
    private const float VelocityUpdateThresholdSqr = 0.01f;
    // cos(0.5°) — avoids Quaternion.Angle (which calls acos) on every tick
    private const float RotationDotThreshold = 0.99996192f;
    private const float EmotionUpdateThreshold = 0.01f;

    private bool hasSentUpdate;
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;
    private Vector3 lastSentVelocity;
    private float4 lastSentEmotions;

    private ActorId _cachedActorId;
    private bool _actorIdCached;

    private byte attemptedGetIdentifiable = 0;
    private bool isValid = true;
    private bool isDestroyed = false;

    public ActorId ActorId
    {
        get
        {
            if (isDestroyed)
            {
                isValid = false;
                return new ActorId(0);
            }

            if (_actorIdCached)
                return _cachedActorId;

            if (!identifiable)
            {
                try
                {
                    identifiable = GetComponent<Identifiable>();
                }
                catch (Exception ex)
                {
                    SrLogger.LogWarning($"Failed to get Identifiable component: {ex.Message}", SrLogTarget.Both);
                    isValid = false;
                    return new ActorId(0);
                }

                attemptedGetIdentifiable++;

                if (attemptedGetIdentifiable >= 10)
                {
                    SrLogger.LogWarning("Failed to get Identifiable after 10 attempts", SrLogTarget.Both);
                    isValid = false;
                }

                if (!identifiable)
                {
                    return new ActorId(0);
                }
            }

            try
            {
                var id = identifiable.GetActorId();
                if (id.Value != 0)
                {
                    _cachedActorId = id;
                    _actorIdCached = true;
                }
                return id;
            }
            catch (Exception ex)
            {
                SrLogger.LogWarning($"Failed to get ActorId: {ex.Message}", SrLogTarget.Both);
                isValid = false;
                return new ActorId(0);
            }
        }
    }

    public bool LocallyOwned { get; set; }
    private bool cachedLocallyOwned;

    internal Vector3 previousPosition;
    internal Vector3 nextPosition;

    internal Quaternion previousRotation;
    internal Quaternion nextRotation;

    private float interpolationStart;
    private float interpolationEnd;

    private float4 EmotionsFloat => emotions
                                    ? emotions._model.Emotions
                                    : new float4(0, 0, 0, 0);

    private void Start()
    {
        try
        {
            // Check for components that shouldn't have NetworkActor
            if (GetComponent<Gadget>())
            {
                Destroy(this);
                return;
            }
            if (GetComponent<SRCharacterController>())
            {
                Destroy(this);
                return;
            }

            emotions = GetComponent<SlimeEmotions>();
            cachedLocallyOwned = LocallyOwned;
            rigidbody = GetComponent<Rigidbody>();
            CacheAuthoritySensitiveBehaviours();
            SetRigidbodyState(LocallyOwned);
            SetAuthoritySensitiveBehaviourState(LocallyOwned);
            identifiable = GetComponent<Identifiable>();

            if (identifiable)
            {
                try
                {
                    var id = identifiable.GetActorId();
                    if (id.Value != 0) { _cachedActorId = id; _actorIdCached = true; }
                }
                catch { /* will be resolved lazily via the ActorId property */ }
            }

            regionMember = GetComponent<RegionMember>();

            if (regionMember)
            {
                try
                {
                    regionMember.add_BeforeHibernationChanged(
                        Delegate.CreateDelegate(Type.GetType("MonomiPark.SlimeRancher.Regions.RegionMember")
                                .GetEvent("BeforeHibernationChanged").EventHandlerType,
                            this.Cast<Il2CppSystem.Object>(),
                            nameof(HibernationChanged),
                            true)
                            .Cast<RegionMember.OnHibernationChange>());
                }
                catch (Exception ex)
                {
                    SrLogger.LogWarning($"Failed to add hibernation event: {ex.Message}", SrLogTarget.Both);
                }
            }
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"NetworkActor.Start error: {ex}", SrLogTarget.Both);
            isValid = false;
        }
    }

    [HideFromIl2Cpp]
    private IEnumerator WaitOneFrameOnHibernationChange(bool value)
    {
        yield return null;

        if (!isValid || isDestroyed)
        {
            yield break;
        }

        try
        {
            if (value)
            {
                LocallyOwned = false;

                var actorId = ActorId;
                if (actorId.Value == 0)
                {
                    yield break;
                }

                if (Main.Server.IsRunning())
                    actorManager.ClearActorOwner(actorId.Value);

                var packet = new ActorUnloadPacket { ActorId = actorId };
                Main.SendToAllOrServer(packet);
            }
            else
            {
                var actorId = ActorId;
                if (actorId.Value == 0)
                {
                    yield break;
                }

                if (Main.Server.IsRunning())
                {
                    LocallyOwned = true;
                    actorManager.SetActorOwner(actorId.Value, LocalID);
                }

                var packet = new ActorTransferPacket
                {
                    ActorId = actorId,
                    OwnerPlayer = LocalID,
                };
                Main.SendToAllOrServer(packet);
            }
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"WaitOneFrameOnHibernationChange error: {ex}", SrLogTarget.Both);
            isValid = false;
        }
    }

    public void HibernationChanged(bool value)
    {
        if (!isValid || isDestroyed)
            return;

        try
        {
            MelonCoroutines.Start(WaitOneFrameOnHibernationChange(value));
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"HibernationChanged error: {ex}", SrLogTarget.Both);
        }
    }

    private void UpdateInterpolation()
    {
        if (LocallyOwned) return;
        if (isDestroyed) return;

        var timer = Mathf.InverseLerp(interpolationStart, interpolationEnd, UnityEngine.Time.unscaledTime);
        timer = Mathf.Clamp01(timer);

        transform.position = Vector3.Lerp(previousPosition, nextPosition, timer);
        transform.rotation = Quaternion.Lerp(previousRotation, nextRotation, timer);
    }

    private void Update()
    {
        PerformanceDiagnostics.RecordNetworkActorUpdate(LocallyOwned);

        if (isDestroyed)
            return;

        if (!isValid)
        {
            isDestroyed = true;
            Destroy(this);
            return;
        }

        try
        {
            if (cachedLocallyOwned != LocallyOwned)
            {
                SetRigidbodyState(LocallyOwned);
                SetAuthoritySensitiveBehaviourState(LocallyOwned);

                if (LocallyOwned && rigidbody)
                    rigidbody.velocity = SavedVelocity;
            }

            cachedLocallyOwned = LocallyOwned;
            syncTimer -= UnityEngine.Time.unscaledDeltaTime;

            UpdateInterpolation();

            if (syncTimer >= 0) return;

            if (LocallyOwned)
            {
                PerformanceDiagnostics.RecordNetworkActorLocalTick();
                syncTimer = Timers.ActorTimer;

                previousPosition = transform.position;
                previousRotation = transform.rotation;
                nextPosition = transform.position;
                nextRotation = transform.rotation;

                var actorId = ActorId;
                if (actorId.Value == 0)
                {
                    PerformanceDiagnostics.RecordNetworkActorInvalidTick();
                    return;
                }

                var currentPosition = transform.position;
                var currentRotation = transform.rotation;
                var currentVelocity = rigidbody ? rigidbody.velocity : Vector3.zero;
                var currentEmotions = EmotionsFloat;

                if (!ShouldSendUpdate(currentPosition, currentRotation, currentVelocity, currentEmotions))
                {
                    PerformanceDiagnostics.RecordNetworkActorUnchangedTick();
                    return;
                }

                RememberSentUpdate(currentPosition, currentRotation, currentVelocity, currentEmotions);
                PerformanceDiagnostics.RecordNetworkActorPacketCreated();

                var packet = new ActorUpdatePacket
                {
                    ActorId = actorId,
                    Position = currentPosition,
                    Rotation = currentRotation,
                    Velocity = currentVelocity,
                    Emotions = currentEmotions
                };

                Main.SendToAllOrServer(packet);
            }
            else
            {
                PerformanceDiagnostics.RecordNetworkActorRemoteRetarget();
                previousPosition = transform.position;
                previousRotation = transform.rotation;

                interpolationStart = UnityEngine.Time.unscaledTime;
                interpolationEnd = UnityEngine.Time.unscaledTime + Timers.ActorTimer;
            }
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"NetworkActor.Update error: {ex}", SrLogTarget.Both);
            isValid = false;
        }
    }

    private bool ShouldSendUpdate(Vector3 position, Quaternion rotation, Vector3 velocity, float4 emotions)
    {
        if (!hasSentUpdate)
            return true;

        if ((position - lastSentPosition).sqrMagnitude >= PositionUpdateThresholdSqr)
            return true;

        if ((velocity - lastSentVelocity).sqrMagnitude >= VelocityUpdateThresholdSqr)
            return true;

        if (Mathf.Abs(Quaternion.Dot(rotation, lastSentRotation)) < RotationDotThreshold)
            return true;

        return Math.Abs(emotions.x - lastSentEmotions.x) >= EmotionUpdateThreshold
            || Math.Abs(emotions.y - lastSentEmotions.y) >= EmotionUpdateThreshold
            || Math.Abs(emotions.z - lastSentEmotions.z) >= EmotionUpdateThreshold
            || Math.Abs(emotions.w - lastSentEmotions.w) >= EmotionUpdateThreshold;
    }

    private void RememberSentUpdate(Vector3 position, Quaternion rotation, Vector3 velocity, float4 emotions)
    {
        hasSentUpdate = true;
        lastSentPosition = position;
        lastSentRotation = rotation;
        lastSentVelocity = velocity;
        lastSentEmotions = emotions;
    }

    private void SetRigidbodyState(bool enableConstraints)
    {
        if (!rigidbody || isDestroyed)
            return;

        try
        {
            rigidbody.constraints =
                enableConstraints
                    ? RigidbodyConstraints.None
                    : RigidbodyConstraints.FreezeAll;
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"SetRigidbodyState error: {ex.Message}", SrLogTarget.Both);
        }
    }

    private void CacheAuthoritySensitiveBehaviours()
    {
        if (authoritySensitiveBehavioursCached)
            return;

        authoritySensitiveBehavioursCached = true;
        AddAuthoritySensitiveBehaviour(GetComponent<SlimeEat>());
        AddAuthoritySensitiveBehaviour(GetComponent<SlimeEatTrigger>());
        AddAuthoritySensitiveBehaviour(GetComponent<Reproduce>());
    }

    private void AddAuthoritySensitiveBehaviour(Behaviour behaviour)
    {
        if (!behaviour)
            return;

        authoritySensitiveBehaviours.Add(new AuthoritySensitiveBehaviourState(behaviour));
    }

    private void SetAuthoritySensitiveBehaviourState(bool locallyOwned)
    {
        if (isDestroyed)
            return;

        CacheAuthoritySensitiveBehaviours();

        foreach (var state in authoritySensitiveBehaviours)
        {
            if (!state.Component)
                continue;

            try
            {
                if (locallyOwned)
                {
                    if (state.IsSuppressed)
                    {
                        state.Component.enabled = state.RestoreEnabled;
                        state.IsSuppressed = false;
                    }
                }
                else
                {
                    if (!state.IsSuppressed)
                    {
                        state.RestoreEnabled = state.Component.enabled;
                        state.IsSuppressed = true;
                    }

                    state.Component.enabled = false;
                }
            }
            catch (Exception ex)
            {
                SrLogger.LogWarning($"SetAuthoritySensitiveBehaviourState error: {ex.Message}", SrLogTarget.Both);
            }
        }
    }

    private void OnDestroy()
    {
        isDestroyed = true;
        isValid = false;
    }

    private sealed class AuthoritySensitiveBehaviourState
    {
        public AuthoritySensitiveBehaviourState(Behaviour component)
        {
            Component = component;
            RestoreEnabled = component.enabled;
        }

        public Behaviour Component { get; }
        public bool RestoreEnabled { get; set; }
        public bool IsSuppressed { get; set; }
    }
}
