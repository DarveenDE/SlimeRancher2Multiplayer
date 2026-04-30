using System.Net;

namespace SR2MP.Shared.Utils;

public readonly struct ServerAddress
{
    public ServerAddress(string host, ushort port)
    {
        Host = host;
        Port = port;
    }

    public string Host { get; }

    public ushort Port { get; }

    public string Display => $"{ServerAddressParser.FormatHost(Host)}:{Port}";
}

public static class ServerAddressParser
{
    public static bool TryParse(string addressInput, string portInput, out ServerAddress address, out string error)
    {
        address = default;
        error = string.Empty;

        var input = addressInput.Trim();
        var fallbackPort = portInput.Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Enter an IP address or hostname.";
            return false;
        }

        if (!TrySplitHostAndPort(input, out var host, out var parsedPort, out error))
            return false;

        if (host.Any(char.IsWhiteSpace))
        {
            error = "Server address cannot contain spaces.";
            return false;
        }

        var portText = parsedPort ?? fallbackPort;
        if (string.IsNullOrWhiteSpace(portText))
        {
            error = "Enter a port or include it in the server address.";
            return false;
        }

        if (!ushort.TryParse(portText, out var port) || port == 0)
        {
            error = "Invalid port: Must be a number from 1 to 65535.";
            return false;
        }

        address = new ServerAddress(host, port);
        return true;
    }

    public static bool TryParseCommand(string[] args, out ServerAddress address, out string error)
    {
        address = default;
        error = string.Empty;

        if (args.Length < 1)
        {
            error = "Not enough arguments.";
            return false;
        }

        string fallbackPort = args.Length > 1 ? args[1] : string.Empty;
        return TryParse(args[0], fallbackPort, out address, out error);
    }

    public static bool TryResolve(ServerAddress address, out ServerAddress resolvedAddress, out string error)
    {
        resolvedAddress = default;
        error = string.Empty;

        try
        {
            var addresses = Dns.GetHostAddresses(address.Host);
            if (addresses.Length == 0)
            {
                error = "Could not resolve that IP address or hostname.";
                return false;
            }

            resolvedAddress = new ServerAddress(addresses[0].ToString(), address.Port);
            return true;
        }
        catch
        {
            error = "Could not resolve that IP address or hostname.";
            return false;
        }
    }

    public static string FormatHost(string host)
    {
        return host.Contains(':') && !host.StartsWith('[')
            ? $"[{host}]"
            : host;
    }

    private static bool TrySplitHostAndPort(string input, out string host, out string? port, out string error)
    {
        host = input;
        port = null;
        error = string.Empty;

        if (input.StartsWith('['))
        {
            int closingBracket = input.IndexOf(']');
            if (closingBracket < 0)
            {
                error = "Close the IPv6 address with ].";
                return false;
            }

            host = input[1..closingBracket].Trim();
            string remaining = input[(closingBracket + 1)..].Trim();

            if (remaining.Length == 0)
                return ValidateHost(host, out error);

            if (!remaining.StartsWith(':'))
            {
                error = "Use [IPv6]:port for IPv6 server addresses.";
                return false;
            }

            port = remaining[1..].Trim();
            if (string.IsNullOrWhiteSpace(port))
            {
                error = "Enter a port after the server address.";
                return false;
            }

            return ValidateHost(host, out error);
        }

        int firstColon = input.IndexOf(':');
        int lastColon = input.LastIndexOf(':');

        if (firstColon >= 0 && firstColon == lastColon)
        {
            host = input[..firstColon].Trim();
            port = input[(firstColon + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(port))
            {
                error = "Enter a port after the server address.";
                return false;
            }
        }

        return ValidateHost(host, out error);
    }

    private static bool ValidateHost(string host, out string error)
    {
        error = string.Empty;
        if (!string.IsNullOrWhiteSpace(host))
            return true;

        error = "Enter an IP address or hostname.";
        return false;
    }
}
