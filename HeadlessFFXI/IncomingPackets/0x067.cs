//GP_SERV_COMMAND_ENTITY_UPDATE1
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0067
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/entity_update.cpp
using System;
using HeadlessFFXI;

public class P067Handler : IPacketHandler
{
    public ushort PacketId => 0x67;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        // two types of packet based on first byte?
        ushort raw = dataReader.ReadUInt16();
        int mode = raw & 0b111111;          // bits 0–5
        int length = raw >> 6;              // bits 6–15
        if (mode == 0x02) // player update
        {
            ushort charIndex = dataReader.ReadUInt16();
            uint charId = dataReader.ReadUInt32();
            ushort fellowIndex = dataReader.ReadUInt16();
            dataReader.Skip(0x02); // padding
            uint nameFlag = dataReader.ReadUInt32();
            uint nameIcon = dataReader.ReadUInt32();
            uint CustomProperties0 = dataReader.ReadUInt32();
            uint unknown = dataReader.ReadUInt32();
            uint unioqueNoMog = dataReader.ReadUInt32();
            byte mogHouseFlag = dataReader.ReadByte();
            byte mjobLevel = dataReader.ReadByte();
            byte levelRestriction = dataReader.ReadByte(); // Might be used other things?
            byte mogExpansionFlag = dataReader.ReadByte(); ; // Is 2nd floor unlocked.

        }
        else if (mode == 0x03) // trust update
        {
            ushort entityIndex = dataReader.ReadUInt16();
            uint entityId = dataReader.ReadUInt32();
            ushort entityMasterIndex = dataReader.ReadUInt16();
            dataReader.Skip(0x02); // padding
            uint nameFlag = dataReader.ReadUInt32();
            string name = dataReader.ReadString(15); // do maths? or is this fixed lenght?
            Console.WriteLine("0x067 Trust " + name);
        }
         else if (mode == 0x04) // pet update
        {
            ushort entityIndex = dataReader.ReadUInt16();
            uint entityId = dataReader.ReadUInt32();
            ushort entityMasterIndex = dataReader.ReadUInt16();
            byte hpp = dataReader.ReadByte();
            byte mpp = dataReader.ReadByte();
            uint tp = dataReader.ReadUInt32();
            uint targetId = dataReader.ReadUInt32();
            string name = dataReader.ReadString(15); // do maths? or is this fixed lenght?
            Console.WriteLine("0x067 Pet " + name);
        }
    }
}