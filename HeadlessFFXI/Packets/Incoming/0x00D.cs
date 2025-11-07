//GP_SERV_CHAR_PC
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x000D
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/char_update.cpp
using System;
using HeadlessFFXI;
using static HeadlessFFXI.Client;

public class P00DHandler : IPacketHandler
{
    public ushort PacketId => 0xD;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(4); // Skip header
        uint charId = dataReader.ReadUInt32();
        ushort charIndex = dataReader.ReadUInt16();
        SendFlags updateFlags = dataReader.ReadEnum<SendFlags>();

        byte dir = dataReader.ReadByte();
        float x = dataReader.ReadFloat();
        float z = dataReader.ReadFloat();
        float y = dataReader.ReadFloat();
        Flags0 flags0 = dataReader.ReadStruct<Flags0>();
        byte speed = dataReader.ReadByte();
        byte speedBase = dataReader.ReadByte();
        byte hpp = dataReader.ReadByte();
        byte serverStatus = dataReader.ReadByte();
        StatusFlags flags1 = dataReader.ReadStruct<StatusFlags>();
        //flags2_t    Flags2 flags2 = dataReader.ReadStruct<Flags2>();
        dataReader.Skip(4);
        //flags3_t    Flags3 flags3 = dataReader.ReadStruct<Flags3>();
        dataReader.Skip(4);
        uint btTargetID = dataReader.ReadUInt32();
        ushort costumeId = dataReader.ReadUInt16();
        byte ballistaInfo = dataReader.ReadByte();
        //flags4_t    Flags4 flags4 = dataReader.ReadStruct<Flags4>();
        dataReader.Skip(1);
        uint customProperty1 = dataReader.ReadUInt32();
        uint customProperty2 = dataReader.ReadUInt32();
        ushort petActIndex = dataReader.ReadUInt16();

        if(client.Entity_List[charIndex] == null)
            client.Entity_List[charIndex] = new Entity();

        //Console.WriteLine("Char Update for ID:{0:G} Index:{1:G}", charID, updateIndex);
        //Console.WriteLine(updateFlags);
        if (updateFlags.HasFlag(SendFlags.Position))
        {
            client.Entity_List[charIndex].IsValid = true;
            EntityType entityType = EntityType.PC;
            client.Entity_List[charIndex].Type = (byte)entityType;
            client.Entity_List[charIndex].Pos.Rot = dir;
            client.Entity_List[charIndex].Pos.X = x;
            client.Entity_List[charIndex].Pos.Y = z;
            client.Entity_List[charIndex].Pos.Z = y;
            client.Entity_List[charIndex].TargetIndex = flags0.FaceTarget;

            //client.Player_Data.pos.Rot = dir;
            //client.Player_Data.pos.X = x;
            //client.Player_Data.pos.Y = z;
            //client.Player_Data.pos.Z = y;
            //Player_Data.pos.moving = flags0.MovTime;
        }
        if (updateFlags.HasFlag(SendFlags.General))
        {
            client.Entity_List[charIndex].ID = charId;
            client.Entity_List[charIndex].IsValid = true;
            client.Entity_List[charIndex].Hpp = hpp;
            client.Entity_List[charIndex].Status = serverStatus;
        }
        if (updateFlags.HasFlag(SendFlags.Model))
        {
            // Look data who cares
            ushort monstrosityFlags = dataReader.ReadUInt16();
            byte monstrosityNameId1 = dataReader.ReadByte();
            byte monstrosityNameId2 = dataReader.ReadByte();
            //flags5_t    Flags5 flags5 = dataReader.ReadStruct<Flags5>();
            dataReader.Skip(1);
            byte modelHitboxSize = dataReader.ReadByte();
            //flags6_t    Flags6 flags6 = dataReader.ReadStruct<Flags6>();
            dataReader.Skip(4);
            //uint16_t    GrapIDTbl[9];
            dataReader.Skip(18);
        }
        if (updateFlags.HasFlag(SendFlags.Name))
        {
            client.Entity_List[charIndex].IsValid = true;
            string name = dataReader.ReadString(dataReader.Remaining);
            client.Entity_List[charIndex].Name = name;
        }
        if (updateFlags.HasFlag(SendFlags.Name) || updateFlags.HasFlag(SendFlags.General))
        {
            //var Flag6 = new Flags6 { Raw = BitConverter.ToUInt32(packet, index + 0x44) };
            //Flag6.MountIndex
        }
        if (updateFlags.HasFlag(SendFlags.Despawn))
        {
            client.Entity_List[charIndex] = new Entity();
        }
        else
        {
            // var CostumeId = BitConverter.ToUInt16(packet, index + 0x30);
            // var PetActIndex = BitConverter.ToUInt16(packet, index + 0x3C);
        }
    }
}