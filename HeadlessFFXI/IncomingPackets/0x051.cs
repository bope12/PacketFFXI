//GP_SERV_COMMAND_GRAP_LIST
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0051
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x051_grap_list.cpp
using System;
using HeadlessFFXI;

public class P051Handler : IPacketHandler
{
    public ushort PacketId => 0x51;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // Tells client looks related info
    }
}