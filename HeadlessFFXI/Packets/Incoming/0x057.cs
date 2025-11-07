//GP_SERV_COMMAND_WEATHER
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0057
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x057_weather.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P057Handler : IPacketHandler
{
    public ushort PacketId => 0x57;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        var startTime = dataReader.ReadUInt32();
        var weatherNumber = dataReader.ReadUInt16();
        var weatherOffsetTime = dataReader.ReadUInt16();
        client.PlayerData.zone.Weather = weatherNumber;
        client.PlayerData.zone.Weather_time = startTime;
    }
}