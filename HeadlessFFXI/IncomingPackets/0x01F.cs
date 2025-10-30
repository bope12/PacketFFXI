//GP_SERV_COMMAND_ITEM_LIST
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x001F
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x01f_item_list.cpp
using System;
using HeadlessFFXI;

public class P01FHandler : IPacketHandler
{
    public ushort PacketId => 0x1F;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        //TODO check if this should be resetting item or if 0x20 does\
        var dataReader = new PacketReader(data);
        dataReader.ReadBytes(0x04); // skip header
        uint quantity = dataReader.ReadUInt32();
        UInt16 itemId = dataReader.ReadUInt16();
        byte container = dataReader.ReadByte();
        byte itemIndex = dataReader.ReadByte();
        byte lockFlag = dataReader.ReadByte();
        client.Player_Data.Inv.Container[container].slots[itemIndex].itemid = itemId;
        client.Player_Data.Inv.Container[container].slots[itemIndex].quantity = quantity;
        client.Player_Data.Inv.Container[container].slots[itemIndex].lockFlag = lockFlag;
    }
}