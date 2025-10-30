//GP_SERV_COMMAND_MOUNT_DATA
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x00AE
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x0ae_mount_data.cpp
using System;
using HeadlessFFXI;

public class P0AEHandler : IPacketHandler
{
    public ushort PacketId => 0xAE;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // This packet is sent by the server to populate the clients mount information.
        //uint8_t MountDataTbl[8];
    }
}