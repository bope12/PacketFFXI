//GP_SERV_COMMAND_GROUP_LIST
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x00DD
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x0dd_group_list.cpp
using System;
using System.Runtime.InteropServices;

namespace HeadlessFFXI.Packets.Incoming;

public class P0DDHandler : IPacketHandler
{
    public ushort PacketId => 0xDD;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        uint charId = dataReader.ReadUInt32();
        uint hp = dataReader.ReadUInt32();
        uint mp = dataReader.ReadUInt32();
        uint tp = dataReader.ReadUInt32();
        GP_GROUP_ATTR gAttr = dataReader.ReadStruct<GP_GROUP_ATTR>();
        ushort actIndex = dataReader.ReadUInt16();
        byte memberNumber = dataReader.ReadByte();
        byte moghouseFlg = dataReader.ReadByte();
        byte kind = dataReader.ReadByte();
        byte hpp = dataReader.ReadByte();
        byte mpp = dataReader.ReadByte();
        byte padding1F = dataReader.ReadByte();
        ushort zoneNo = dataReader.ReadUInt16();
        byte mjob_no = dataReader.ReadByte();
        byte mjob_lv = dataReader.ReadByte();
        byte sjob_no = dataReader.ReadByte();
        byte sjob_lv = dataReader.ReadByte();
        byte masterjob_lv = dataReader.ReadByte();
        byte masterjob_flags = dataReader.ReadByte();
        string name = dataReader.ReadString(16);
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GP_GROUP_ATTR
    {
        private uint _bitFields;        // 4 bytes (all bit fields packed into single uint32)

        // Properties to access bit fields
        public uint PartyNo
        {
            get => _bitFields & 0x03;  // Bits 0-1 (mask 0b00000011)
            set => _bitFields = (_bitFields & ~0x03u) | (value & 0x03);
        }

        public bool PartyLeaderFlg
        {
            get => (_bitFields & 0x04) != 0;  // Bit 2
            set => _bitFields = value ? (_bitFields | 0x04) : (_bitFields & ~0x04u);
        }

        public bool AllianceLeaderFlg
        {
            get => (_bitFields & 0x08) != 0;  // Bit 3
            set => _bitFields = value ? (_bitFields | 0x08) : (_bitFields & ~0x08u);
        }

        public bool PartyRFlg
        {
            get => (_bitFields & 0x10) != 0;  // Bit 4
            set => _bitFields = value ? (_bitFields | 0x10) : (_bitFields & ~0x10u);
        }

        public bool AllianceRFlg
        {
            get => (_bitFields & 0x20) != 0;  // Bit 5
            set => _bitFields = value ? (_bitFields | 0x20) : (_bitFields & ~0x20u);
        }

        public bool Unknown06
        {
            get => (_bitFields & 0x40) != 0;  // Bit 6
            set => _bitFields = value ? (_bitFields | 0x40) : (_bitFields & ~0x40u);
        }

        public bool Unknown07
        {
            get => (_bitFields & 0x80) != 0;  // Bit 7
            set => _bitFields = value ? (_bitFields | 0x80) : (_bitFields & ~0x80u);
        }

        public bool LevelSyncFlg
        {
            get => (_bitFields & 0x100) != 0;  // Bit 8
            set => _bitFields = value ? (_bitFields | 0x100) : (_bitFields & ~0x100u);
        }

        // Bits 9-31 are unused (23 bits)
    }
}