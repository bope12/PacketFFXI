//GP_SERV_COMMAND_EQUIP_CLEAR
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x004F
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x04f_equip_clear.h
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P04FHandler : IPacketHandler
{
    public ushort PacketId => 0x4F;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        client.PlayerData.Equip = new Equipment[16];
        //client.ShowInfo("[0x04F] Equipment cleared.");
    }
}