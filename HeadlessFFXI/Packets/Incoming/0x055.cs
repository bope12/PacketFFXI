//GP_SERV_COMMAND_SCENARIOITEM
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0055
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x055_scenarioitem.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P055Handler : IPacketHandler
{
    public ushort PacketId => 0x55;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // This packet is sent by the server to populate the clients key item information.
    }
}