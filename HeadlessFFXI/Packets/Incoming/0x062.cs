//GP_SERV_COMMAND_CLISTATUS2
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0062
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x062_clistatus2.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P062Handler : IPacketHandler
{
    public ushort PacketId => 0x62;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        //skill base information.
    }
}