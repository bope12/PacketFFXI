//GP_SERV_COMMAND_MERIT
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x008C
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x08c_merit.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P08CHandler : IPacketHandler
{
    public ushort PacketId => 0x8C;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // This is actual merit data of how much next will cost and how many you have for each merit
    }
}