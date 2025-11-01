//GP_SERV_COMMAND_WEATHER
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0057
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x057_weather.cpp
using System;
using HeadlessFFXI;

public class P057Handler : IPacketHandler
{
    public ushort PacketId => 0x57;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        uint startTime = dataReader.ReadUInt32();
        ushort weatherNumber = dataReader.ReadUInt16();
        ushort weatherOffsetTime = dataReader.ReadUInt16();
        client.Player_Data.zone.Weather = weatherNumber;
        client.Player_Data.zone.Weather_time = startTime;
    }
}