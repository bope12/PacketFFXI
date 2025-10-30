//GP_SERV_COMMAND_ABIL_RECAST
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0119
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x119_abil_recast.cpp
using System;
using HeadlessFFXI;

public class P019Handler : IPacketHandler
{
    public ushort PacketId => 0x119;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        //Array of abilitys on cooldown and their recast info
    }
}