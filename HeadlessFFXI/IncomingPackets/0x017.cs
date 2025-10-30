//GP_SERV_COMMAND_CHAT_STD
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0017
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x017_chat_std.cpp
using System;
using HeadlessFFXI;

public class P017Handler : IPacketHandler
{
    public ushort PacketId => 0x17;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        Console.WriteLine(data.Length);
        var dataReader = new PacketReader(data);
        dataReader.Skip(4); //skip header
        var MessageType = dataReader.ReadByte();
        var isGM = dataReader.ReadByte() == 0x01;
        var zoneID = dataReader.ReadUInt16(); // 0x06 can also be MentorRank or MasterRank depending on MessageType
        string name = dataReader.ReadString(15);
        if (!client.silient)
            Console.WriteLine("[Chat]" + name + ":" + dataReader.ReadString(data.Length - dataReader.Position));

        //TODO offer some kind of event fire
        //IncomingChat?.Invoke(this, EventArgs.Empty);
    }
}