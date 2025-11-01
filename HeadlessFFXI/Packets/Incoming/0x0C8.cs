//GP_SERV_COMMAND_GROUP_TBL
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x00C8
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x0c8_group_tbl.cpp
using System;
using System.Runtime.InteropServices;
using HeadlessFFXI;

public class P0C8Handler : IPacketHandler
{
    public ushort PacketId => 0xC8;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        byte kind = dataReader.ReadByte();
        dataReader.Skip(3);
        PartyMemberEntry[] partyMembers = new PartyMemberEntry[20];
        for (int i = 0; i < 20; i++)
        {
            partyMembers[i] = dataReader.ReadStruct<PartyMemberEntry>();
        }
        // Packet with all 0 UniqueNo means I left the party or disbanded

        //Console.WriteLine("Party/Alliance Members:");
        //for (int i = 0; i < 20; i++)
        //{
        //    var member = partyMembers[i];
        //    if (member.UniqueNo != 0)
        //    {
        //        Console.WriteLine($"Member {i + 1}: UniqueNo={member.UniqueNo}, ActIndex={member.ActIndex}, PartyNo={member.PartyNo}, PartyLeaderFlg={member.PartyLeaderFlg}, AllianceLeaderFlg={member.AllianceLeaderFlg}, ZoneNo={member.ZoneNo}");
        //    }
        //}

    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PartyMemberEntry
    {
        public uint UniqueNo;           // 4 bytes (offset 0x00)
        public ushort ActIndex;         // 2 bytes (offset 0x04)

        // Bit fields are packed into a single byte at offset 0x06
        private byte _bitFields;        // 1 byte  (offset 0x06)

        public byte padding07;          // 1 byte  (offset 0x07)
        public ushort ZoneNo;           // 2 bytes (offset 0x08)
        public ushort padding0A;        // 2 bytes (offset 0x0A)

        // Total size: 12 bytes (0x0C)

        // Properties to access bit fields
        public byte PartyNo
        {
            get => (byte)(_bitFields & 0x03);  // Bits 0-1 (mask 0b00000011)
            set => _bitFields = (byte)((_bitFields & ~0x03) | (value & 0x03));
        }

        public bool PartyLeaderFlg
        {
            get => (_bitFields & 0x04) != 0;  // Bit 2 (mask 0b00000100)
            set => _bitFields = value ? (byte)(_bitFields | 0x04) : (byte)(_bitFields & ~0x04);
        }

        public bool AllianceLeaderFlg
        {
            get => (_bitFields & 0x08) != 0;  // Bit 3 (mask 0b00001000)
            set => _bitFields = value ? (byte)(_bitFields | 0x08) : (byte)(_bitFields & ~0x08);
        }

        public bool PartyQuartermasterFlg
        {
            get => (_bitFields & 0x10) != 0;  // Bit 4 (mask 0b00010000)
            set => _bitFields = value ? (byte)(_bitFields | 0x10) : (byte)(_bitFields & ~0x10);
        }

        public bool AllianceQuartermasterFlg
        {
            get => (_bitFields & 0x20) != 0;  // Bit 5 (mask 0b00100000)
            set => _bitFields = value ? (byte)(_bitFields | 0x20) : (byte)(_bitFields & ~0x20);
        }

        public bool Unknown06
        {
            get => (_bitFields & 0x40) != 0;  // Bit 6 (mask 0b01000000)
            set => _bitFields = value ? (byte)(_bitFields | 0x40) : (byte)(_bitFields & ~0x40);
        }

        public bool Unknown07
        {
            get => (_bitFields & 0x80) != 0;  // Bit 7 (mask 0b10000000)
            set => _bitFields = value ? (byte)(_bitFields | 0x80) : (byte)(_bitFields & ~0x80);
        }
    }
}