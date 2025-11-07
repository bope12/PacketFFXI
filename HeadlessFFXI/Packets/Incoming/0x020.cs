//GP_SERV_COMMAND_ITEM_ATTR
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0020
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x020_item_attr.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P020Handler : IPacketHandler
{
    public ushort PacketId => 0x20;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        var quantity = dataReader.ReadUInt32();
        var price = dataReader.ReadUInt32();
        var itemId = dataReader.ReadUInt16();
        var container = dataReader.ReadByte();
        var itemIndex = dataReader.ReadByte();
        var lockFlag = dataReader.ReadByte();
        var exdata = dataReader.ReadBytes(24);
        client.PlayerData.Inv.Container[container].slots[itemIndex] = new InventorySlot();
        client.PlayerData.Inv.Container[container].slots[itemIndex].itemid = itemId;
        client.PlayerData.Inv.Container[container].slots[itemIndex].quantity = quantity;
        client.PlayerData.Inv.Container[container].slots[itemIndex].lockFlag = lockFlag;
        client.PlayerData.Inv.Container[container].slots[itemIndex].extra = exdata.ToArray();
    }
}