//GP_SERV_COMMAND_ENTITY_UPDATE1
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0067
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/entity_update.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P067Handler : IPacketHandler
{
    public ushort PacketId => 0x67;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        // two types of packet based on first byte?
        var raw = dataReader.ReadUInt16();
        var mode = raw & 0b111111;          // bits 0–5
        var length = raw >> 6;              // bits 6–15
        switch (mode)
        {
            // player update
            case 0x02:
            {
                var charIndex = dataReader.ReadUInt16();
                var charId = dataReader.ReadUInt32();
                var fellowIndex = dataReader.ReadUInt16();
                dataReader.Skip(0x02); // padding
                var nameFlag = dataReader.ReadUInt32();
                var nameIcon = dataReader.ReadUInt32();
                var CustomProperties0 = dataReader.ReadUInt32();
                var unknown = dataReader.ReadUInt32();
                var unioqueNoMog = dataReader.ReadUInt32();
                var mogHouseFlag = dataReader.ReadByte();
                var mjobLevel = dataReader.ReadByte();
                var levelRestriction = dataReader.ReadByte(); // Might be used other things?
                var mogExpansionFlag = dataReader.ReadByte(); ; // Is 2nd floor unlocked.
                break;
            }
            // trust update
            case 0x03:
            {
                var entityIndex = dataReader.ReadUInt16();
                var entityId = dataReader.ReadUInt32();
                var entityMasterIndex = dataReader.ReadUInt16();
                dataReader.Skip(0x02); // padding
                var nameFlag = dataReader.ReadUInt32();
                var name = dataReader.ReadString(15); // do maths? or is this fixed lenght?
                client.ShowInfo("0x067 Trust " + name);
                break;
            }
            // pet update
            case 0x04:
            {
                var entityIndex = dataReader.ReadUInt16();
                var entityId = dataReader.ReadUInt32();
                var entityMasterIndex = dataReader.ReadUInt16();
                var hpp = dataReader.ReadByte();
                var mpp = dataReader.ReadByte();
                var tp = dataReader.ReadUInt32();
                var targetId = dataReader.ReadUInt32();
                var name = dataReader.ReadString(15); // do maths? or is this fixed lenght?
                client.ShowInfo("0x067 Pet " + name);
                break;
            }
        }
    }
}