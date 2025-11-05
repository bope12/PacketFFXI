//GP_SERV_COMMAND_GROUP_ATTR
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x00DF
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x0df_group_attr.cpp
using System;
using HeadlessFFXI;

public class P0DFHandler : IPacketHandler
{
    public ushort PacketId => 0xDF;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // Party self and trust updates
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        uint entityId = dataReader.ReadUInt32();
        uint hp = dataReader.ReadUInt32();
        uint mp = dataReader.ReadUInt16();
        uint tp = dataReader.ReadUInt16();
        ushort targetIndex = dataReader.ReadUInt16();
        byte hpp = dataReader.ReadByte();
        byte mpp = dataReader.ReadByte();
        byte kind = dataReader.ReadByte();
        byte mhFlag = dataReader.ReadByte();
        ushort zoneId = dataReader.ReadUInt16();
        ushort monstrosityFlag = dataReader.ReadUInt16();
        ushort monstrosityNameId = dataReader.ReadUInt16();
        byte mjob = dataReader.ReadByte();
        byte mjobLevel = dataReader.ReadByte();
        byte sjob = dataReader.ReadByte();
        byte sjobLevel = dataReader.ReadByte();
        if (!dataReader.EndOfData)
        {
            byte masterLevel = dataReader.ReadByte();
        }
        if (!dataReader.EndOfData)
        {
            byte masterFlag = dataReader.ReadByte();
        }
        if (entityId == client.Player_Data.ID)
        {
            client.Player_Data.HP = hp;
            client.Player_Data.MP = mp;
            client.Player_Data.TP = tp;
            client.Player_Data.Job = mjob;
            client.Player_Data.SubJob = sjob;
            client.Player_Data.Level = mjobLevel;
            client.Player_Data.SubLevel = sjobLevel;
            client.Player_Data.Index = targetIndex;
        }
    }
}