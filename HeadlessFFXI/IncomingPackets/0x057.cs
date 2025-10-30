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
        //uint32_t StartTime;
        //Weather  WeatherNumber;
        //uint16_t WeatherOffsetTime;
    }
}