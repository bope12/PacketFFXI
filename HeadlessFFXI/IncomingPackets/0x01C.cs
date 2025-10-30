//GP_SERV_COMMAND_ITEM_MAX
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x001C
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x01c_item_max.cpp
using System;
using System.Buffers.Binary;
using HeadlessFFXI;

public class P01CHandler : IPacketHandler
{
    public ushort PacketId => 0x1C;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        client.Player_Data.Inv = new Inventory();
        client.Player_Data.Inv.Container = new Storage[18];
        for (int i = 0; i < 18; i++)
        {
            client.Player_Data.Inv.Container[i].size = data[0x04 + i];
            client.Player_Data.Inv.Container[i].available = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x24 + ( i * 2), 2));
            client.Player_Data.Inv.Container[i].slots = new InventorySlot[client.Player_Data.Inv.Container[i].size];
        }
    }
}