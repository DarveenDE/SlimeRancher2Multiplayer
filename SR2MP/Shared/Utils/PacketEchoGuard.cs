namespace SR2MP.Shared.Utils;

public static class PacketEchoGuard
{
    public static void RunWithHandlingPacket(Action action)
    {
        var wasHandlingPacket = handlingPacket;
        handlingPacket = true;

        try
        {
            action();
        }
        finally
        {
            handlingPacket = wasHandlingPacket;
        }
    }
}
