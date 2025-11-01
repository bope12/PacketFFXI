//GP_SERV_COMMAND_CLISTATUS
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0061
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x061_clistatus.cpp
using System;
using HeadlessFFXI;

public class P061Handler : IPacketHandler
{
    public ushort PacketId => 0x61;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // Stat menu infomation
    }
}