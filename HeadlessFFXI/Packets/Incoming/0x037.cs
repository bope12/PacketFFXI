//GP_SERV_COMMAND_SERVERSTATUS
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0037
//
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P037Handler : IPacketHandler
{
    public ushort PacketId => 0x37;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(4); // Skip header
        ReadOnlySpan<byte> bufStatus = dataReader.ReadBytes(32);
        uint charId = dataReader.ReadUInt32();

        //flags0_t        Flags0;                 // PS2: <bits> (Nameless bitfield.)
        dataReader.Skip(4);
        //flags1_t        Flags1;                 // PS2: <bits> (Nameless bitfield.)
        dataReader.Skip(4);

        byte serverStatus = dataReader.ReadByte();
        byte r = dataReader.ReadByte();
        byte g = dataReader.ReadByte();
        byte b = dataReader.ReadByte();

        //flags2_t        Flags2;                 // PS2: <bits> (Nameless bitfield.)
        dataReader.Skip(4);
        //flags3_t        Flags3;                 // PS2: <bits> (New; did not exist.)
        dataReader.Skip(4);

        uint deadCounter1 = dataReader.ReadUInt32();
        uint deadCounter2 = dataReader.ReadUInt32();
        ushort costumeId = dataReader.ReadUInt16();
        ushort warpTargetIndex = dataReader.ReadUInt16();
        ushort fellowTargetIndex = dataReader.ReadUInt16();
        byte fishingTimer = dataReader.ReadByte();
        byte padding4B = dataReader.ReadByte();

        //status_bits_t   BufStatusBits;          // PS2: (New; did not exist.)
        dataReader.Skip(4);

        ushort monstrosityInfo = dataReader.ReadUInt16();
        byte monstrosityNameId1 = dataReader.ReadByte();
        byte monstrosityNameId2 = dataReader.ReadByte();

        //flags4_t        Flags4;                 // PS2: (New; did not exist.)
        dataReader.Skip(1);

        byte modelHitboxSize = dataReader.ReadByte();

        //flags5_t        Flags5;                 // PS2: (New; did not exist.)
        dataReader.Skip(1);

        byte mountId = dataReader.ReadByte();

        //flags6_t        Flags6;                 // PS2: (New; did not exist.)
        dataReader.Skip(4);
    }
}