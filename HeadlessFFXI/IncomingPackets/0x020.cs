//GP_SERV_COMMAND_ITEM_ATTR
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0020
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x020_item_attr.cpp
using System;
using HeadlessFFXI;

public class P020Handler : IPacketHandler
{
    public ushort PacketId => 0x20;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        uint quantity = dataReader.ReadUInt32();
        uint price = dataReader.ReadUInt32();
        ushort itemId = dataReader.ReadUInt16();
        byte container = dataReader.ReadByte();
        byte itemIndex = dataReader.ReadByte();
        byte lockFlag = dataReader.ReadByte();
        ReadOnlySpan<byte> exdata = dataReader.ReadBytes(24);
        client.Player_Data.Inv.Container[container].slots[itemIndex] = new InventorySlot();
        client.Player_Data.Inv.Container[container].slots[itemIndex].itemid = itemId;
        client.Player_Data.Inv.Container[container].slots[itemIndex].quantity = quantity;
        client.Player_Data.Inv.Container[container].slots[itemIndex].lockFlag = lockFlag;
        client.Player_Data.Inv.Container[container].slots[itemIndex].extra = exdata.ToArray();
    }
}