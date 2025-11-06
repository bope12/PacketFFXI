//GP_SERV_COMMAND_EQUIP_LIST
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0050
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x050_equip_list.cpp
using System;
using HeadlessFFXI;

public class P050Handler : IPacketHandler
{
    public ushort PacketId => 0x50;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        byte invslot = dataReader.ReadByte();
        byte equipslot = dataReader.ReadByte();
        byte container = dataReader.ReadByte();
        client.Player_Data.Equip[equipslot].InventorySlot = invslot;
        client.Player_Data.Equip[equipslot].Container = container;
        //client.ShowInfo("[0x050] Equip Slot:{0} Item Slot:{1} Container:{2}", equipslot, invslot, container);
    }
}