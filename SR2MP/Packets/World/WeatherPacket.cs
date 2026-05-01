using System.Collections;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.Weather;
using Il2CppMonomiPark.SlimeRancher.World;
using SR2E.Utils;
using SR2MP.Client.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Packets.World;

public sealed class WeatherPacket : IPacket
{
    public Dictionary<string, WeatherZoneData> Zones;

    public PacketType Type { get; private init; }
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer) => writer.WriteDictionary(Zones, PacketWriterDels.String, PacketWriterDels.NetObject<WeatherZoneData>.Func);

    public void Deserialise(PacketReader reader) => Zones = reader.ReadDictionary(PacketReaderDels.String, PacketReaderDels.NetObject<WeatherZoneData>.Func);

    public static IEnumerator CreateFromModel(
        WeatherModel model,
        PacketType type,
        Action<WeatherPacket>? onComplete)
    {
        var packet = new WeatherPacket
        {
            Type = type,
            Zones = new Dictionary<string, WeatherZoneData>()
        };

        foreach (var zone in model._zoneDatas)
        {
            if (!zone.Key)
                continue;

            var zoneData = new WeatherZoneData
            {
                WeatherForecasts = new List<WeatherForecast>(),
                WindSpeed = zone.Value.Parameters.WindDirection
            };
            foreach (var forecast in zone.Value.Forecast)
            {
                if (!forecast.Started)
                    continue;

                zoneData.WeatherForecasts.Add(new WeatherForecast
                {
                    State = forecast.State.Cast<WeatherStateDefinition>(),
                    WeatherStarted = true,
                    StartTime = forecast.StartTime,
                    EndTime = forecast.EndTime
                });
            }

            packet.Zones[GetZoneKey(zone.Key)] = zoneData;

            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
        }

        onComplete?.Invoke(packet);
    }

    public static string GetZoneKey(ZoneDefinition zone) => zone ? zone.name : string.Empty;
}

public sealed class WeatherZoneData : INetObject
{
    public List<WeatherForecast> WeatherForecasts;
    public Vector3 WindSpeed;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteList(WeatherForecasts, PacketWriterDels.NetObject<WeatherForecast>.Func);
        writer.WriteVector3(WindSpeed);
    }

    public void Deserialise(PacketReader reader)
    {
        WeatherForecasts = reader.ReadList(PacketReaderDels.NetObject<WeatherForecast>.Func);
        WindSpeed = reader.ReadVector3();
    }
}

public sealed class WeatherForecast : INetObject
{
    public WeatherStateDefinition State;
    public bool WeatherStarted;
    public double StartTime;
    public double EndTime;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteInt(NetworkWeatherManager.GetPersistentID(State));
        writer.WriteBool(WeatherStarted);
        writer.WriteDouble(StartTime);
        writer.WriteDouble(EndTime);
    }

    public void Deserialise(PacketReader reader)
    {
        NetworkWeatherManager.CheckInitialized();
        State = NetworkWeatherManager.weatherStates[reader.ReadInt()];
        WeatherStarted = reader.ReadBool();
        StartTime = reader.ReadDouble();
        EndTime = reader.ReadDouble();
    }
}

// ZoomedOutUI -> zoneMarkers ->
