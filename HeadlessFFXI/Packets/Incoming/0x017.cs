//GP_SERV_COMMAND_CHAT_STD
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0017
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x017_chat_std.cpp
using System;
using static HeadlessFFXI.Client;

namespace HeadlessFFXI.Packets.Incoming;

public class P017Handler : IPacketHandler
{
    public ushort PacketId => 0x17;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(4); //skip header
        var messageType = dataReader.ReadByte();
        var isGm = dataReader.ReadByte() == 0x01;
        var zoneId = dataReader.ReadUInt16(); // 0x06 can also be MentorRank or MasterRank depending on MessageType
        var name = dataReader.ReadString(15);
        var message = dataReader.ReadString(data.Length - dataReader.Position);

        client.ShowInfo("[Chat]" + name + ":" + message);

        client.OnIncomeChat(new IncomingChatEventArgs(name, message, messageType, isGm, zoneId));
    }
}