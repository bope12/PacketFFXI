//GP_SERV_COMMAND_WPOS
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x005B
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x05b_wpos.cpp
using System;
using HeadlessFFXI;

public class P05BHandler : IPacketHandler
{
    public ushort PacketId => 0x5B;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(4); // Skip header

        float x = dataReader.ReadFloat();
        float y = dataReader.ReadFloat();
        float z = dataReader.ReadFloat();
        uint uniqueNo = dataReader.ReadUInt32();
        ushort actIndex = dataReader.ReadUInt16();
        byte mode = dataReader.ReadByte();
        byte dir = dataReader.ReadByte();  //dir * 6.283185 * 0.00390625;

        if(client.Player_Data.ID == uniqueNo)
        {
            client.Player_Data.pos.X = x;
            client.Player_Data.pos.Y = y;
            client.Player_Data.pos.Z = z;
            client.Player_Data.pos.Rotation = (sbyte)dir;
            client.Player_Data.Index = actIndex;
        }

    }
}