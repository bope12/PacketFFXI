//GP_SERV_COMMAND_CONQUEST
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x005E
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x05e_conquest.cpp
using System;
using HeadlessFFXI;

public class P05EHandler : IPacketHandler
{
    public ushort PacketId => 0x5E;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // Conquest / besieged menu Data
    }
}