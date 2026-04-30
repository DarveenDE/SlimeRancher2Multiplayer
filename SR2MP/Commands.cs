using SR2E;
using SR2E.Utils;
using SR2MP.Components.UI;
using SR2MP.Packets;
using SR2MP.Shared.Utils;

namespace SR2MP;

public sealed class HostCommand : SR2ECommand
{
    private static Server.Server? server;

    public override string ID => "host";
    public override string Usage => "host <port>";

    public override bool Execute(string[] args)
    {
        MenuEUtil.CloseOpenMenu();
        server = Main.Server;
        if (args.Length < 1 || !ushort.TryParse(args[0], out var port) || port == 0)
            return false;

        return server.Start(port, true);
    }
}
public sealed class ChatCommand : SR2ECommand
{
    public override string ID => "chat";
    public override string Usage => "chat <message>";

    public override bool Execute(string[] args)
    {
        if (args.Length < 1)
            SendError("Not enough arguments");

        var msg = string.Join(" ", args);

        var chatPacket = new ChatMessagePacket
        {
            Username = Main.Username,
            Message = msg,
        };

        Main.SendToAllOrServer(chatPacket);

        string messageId = $"{Main.Username}_{msg.GetHashCode()}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        MultiplayerUI.Instance.RegisterChatMessage(msg, Main.Username, messageId);

        return true;
    }
}

public sealed class ConnectCommand : SR2ECommand
{
    public override string ID => "connect";
    public override string Usage => "connect <host[:port]|[ipv6]:port> [port]";

    public override bool Execute(string[] args)
    {
        MenuEUtil.CloseOpenMenu();

        if (args.Length < 1)
            return false;

        if (!ServerAddressParser.TryParseCommand(args, out var address, out var parseError))
        {
            SrLogger.LogWarning(parseError, SrLogTarget.Both);
            return false;
        }

        if (!ServerAddressParser.TryResolve(address, out var resolvedAddress, out var resolveError))
        {
            SrLogger.LogWarning(resolveError, SrLogTarget.Both);
            return false;
        }

        return Main.Client.Connect(resolvedAddress.Host, resolvedAddress.Port);
    }
}
