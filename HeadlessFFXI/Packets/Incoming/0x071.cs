//GP_SERV_COMMAND_INFLUENCE
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0071
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x071_influence_colonization.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P071Handler : IPacketHandler
{
    public ushort PacketId => 0x71;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {

    }
}