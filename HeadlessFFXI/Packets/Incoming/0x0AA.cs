//GP_SERV_COMMAND_MAGIC_DATA
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x00AA
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x0aa_magic_data.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P0AAHandler : IPacketHandler
{
    public ushort PacketId => 0xAA;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // This packet is sent by the server to populate the clients list of available magic spells.
        var dataReader = new PacketReader(data);
        dataReader.Skip(4);
        client.PlayerData.SpellList = dataReader.ReadBytes(128).ToArray();
    }
}