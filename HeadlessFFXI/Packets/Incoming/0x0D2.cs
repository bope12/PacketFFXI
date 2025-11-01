//GP_SERV_COMMAND_TROPHY_LIST
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x00D2
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x0d2_trophy_list.cpp
using System;
using HeadlessFFXI;

public class P0D2Handler : IPacketHandler
{
    public ushort PacketId => 0xD2;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        uint quantity = dataReader.ReadUInt32();
        uint targetId = dataReader.ReadUInt32();
        ushort gold = dataReader.ReadUInt16();
        dataReader.Skip(2); // padding
        ushort itemId = dataReader.ReadUInt16();
        ushort targetIndex = dataReader.ReadUInt16();
        byte poolSlot = dataReader.ReadByte();
        byte entry = dataReader.ReadByte();; // 1 if old 0 if new
        byte IsContainer = dataReader.ReadByte();; // 1 if from NPC
        dataReader.Skip(1); // padding
        uint startTime = dataReader.ReadUInt32();
    }
}