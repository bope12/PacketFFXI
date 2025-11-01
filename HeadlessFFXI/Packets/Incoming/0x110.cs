//GP_SERV_COMMAND_UNITY
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0110
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x110_unity.cpp
using System;
using HeadlessFFXI;

public class P010Handler : IPacketHandler
{
    public ushort PacketId => 0x110;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // Unity Data
    }
}