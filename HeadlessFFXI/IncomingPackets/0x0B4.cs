//GP_SERV_COMMAND_CONFIG
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x00B4
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x0b4_config.cpp
using System;
using HeadlessFFXI;

public class P0B4Handler : IPacketHandler
{
    public ushort PacketId => 0xB4;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // Config Settings
    }
}