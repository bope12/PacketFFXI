//GP_SERV_COMMAND_WPOS2
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0065
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x065_wpos2.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P065Handler : IPacketHandler
{
    public ushort PacketId => 0x65;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(4); // Skip header

        var x = dataReader.ReadFloat();
        var y = dataReader.ReadFloat();
        var z = dataReader.ReadFloat();
        var uniqueNo = dataReader.ReadUInt32();
        var actIndex = dataReader.ReadUInt16();
        var mode = dataReader.ReadByte();
        var dir = dataReader.ReadSByte();  //dir * 6.283185 * 0.00390625;

        if(client.PlayerData.ID == uniqueNo)
        {
            client.PlayerData.pos.X = x;
            client.PlayerData.pos.Y = y;
            client.PlayerData.pos.Z = z;
            client.PlayerData.pos.Rotation = dir;
            client.PlayerData.Index = actIndex;
        }
    }
}