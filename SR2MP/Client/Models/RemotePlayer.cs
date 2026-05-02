namespace SR2MP.Client.Models;

public sealed class RemotePlayer
{
    public string PlayerId { get; }
    public string Username { get; set; }

    public Vector3 Position { get; set; }
    public int SceneGroup { get; set; } = -1;
    public float Rotation { get; set; }

    // Animation stuff
    public int AirborneState { get; set; }
    public bool Moving { get; set; }
    public float Yaw { get; set; }
    public float HorizontalMovement { get; set; }
    public float ForwardMovement { get; set; }
    public float HorizontalSpeed { get; set; }
    public float ForwardSpeed { get; set; }
    public bool Sprinting { get; set; }

    public float LookY { get; set; }
    public float LastLookY { get; set; }

    // Vacpack visible state
    public int VacpackHeldIdentType { get; set; }
    public int VacpackActiveSlot { get; set; }
    public byte VacpackWaterLevel { get; set; }

    public RemotePlayer(string playerId) => PlayerId = playerId;
}
