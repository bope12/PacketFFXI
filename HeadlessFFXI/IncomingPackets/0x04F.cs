//GP_SERV_COMMAND_EQUIP_CLEAR
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x004F
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x04f_equip_clear.h
using System;
using HeadlessFFXI;

public class P04FHandler : IPacketHandler
{
    public ushort PacketId => 0x4F;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // Clears equipment table in client and rebuilds it
    }
}