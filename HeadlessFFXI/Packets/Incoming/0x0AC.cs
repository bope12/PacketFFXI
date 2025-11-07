//GP_SERV_COMMAND_COMMAND_DATA
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x00AC
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x0ac_command_data.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P0ACHandler : IPacketHandler
{
    public ushort PacketId => 0xAC;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // This packet is sent by the server to populate the clients command information. (Weapon Skills, Job Abilities, Pet Abilities, Traits)
    }
}