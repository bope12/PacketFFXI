//GP_SERV_CHAR_PC
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x000D
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/char_update.cpp
using System;
using static HeadlessFFXI.Client;

namespace HeadlessFFXI.Packets.Incoming;

public class P00DHandler : IPacketHandler
{
    public ushort PacketId => 0xD;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(4); // Skip header
        var charId = dataReader.ReadUInt32();
        var charIndex = dataReader.ReadUInt16();
        var updateFlags = dataReader.ReadEnum<SendFlags>();

        var dir = dataReader.ReadByte();
        var x = dataReader.ReadFloat();
        var z = dataReader.ReadFloat();
        var y = dataReader.ReadFloat();
        var flags0 = dataReader.ReadStruct<Flags0>();
        var speed = dataReader.ReadByte();
        var speedBase = dataReader.ReadByte();
        var hpp = dataReader.ReadByte();
        var serverStatus = dataReader.ReadByte();
        var flags1 = dataReader.ReadStruct<StatusFlags>();
        //flags2_t    Flags2 flags2 = dataReader.ReadStruct<Flags2>();
        dataReader.Skip(4);
        //flags3_t    Flags3 flags3 = dataReader.ReadStruct<Flags3>();
        dataReader.Skip(4);
        var btTargetID = dataReader.ReadUInt32();
        var costumeId = dataReader.ReadUInt16();
        var ballistaInfo = dataReader.ReadByte();
        //flags4_t    Flags4 flags4 = dataReader.ReadStruct<Flags4>();
        dataReader.Skip(1);
        var customProperty1 = dataReader.ReadUInt32();
        var customProperty2 = dataReader.ReadUInt32();
        var petActIndex = dataReader.ReadUInt16();

        client.EntityList[charIndex] ??= new Entity();

        //Console.WriteLine("Char Update for ID:{0:G} Index:{1:G}", charID, updateIndex);
        //Console.WriteLine(updateFlags);
        if (updateFlags.HasFlag(SendFlags.Position))
        {
            client.EntityList[charIndex].IsValid = true;
            var entityType = EntityType.PC;
            client.EntityList[charIndex].Type = (byte)entityType;
            client.EntityList[charIndex].Pos.Rotation = (sbyte)dir;
            client.EntityList[charIndex].Pos.X = x;
            client.EntityList[charIndex].Pos.Y = z;
            client.EntityList[charIndex].Pos.Z = y;
            client.EntityList[charIndex].TargetIndex = flags0.FaceTarget;

            //client.Player_Data.pos.Rot = dir;
            //client.Player_Data.pos.X = x;
            //client.Player_Data.pos.Y = z;
            //client.Player_Data.pos.Z = y;
            //Player_Data.pos.moving = flags0.MovTime;
        }
        if (updateFlags.HasFlag(SendFlags.General))
        {
            client.EntityList[charIndex].ID = charId;
            client.EntityList[charIndex].IsValid = true;
            client.EntityList[charIndex].Hpp = hpp;
            client.EntityList[charIndex].Status = serverStatus;
        }
        if (updateFlags.HasFlag(SendFlags.Model))
        {
            // Look data who cares
            var monstrosityFlags = dataReader.ReadUInt16();
            var monstrosityNameId1 = dataReader.ReadByte();
            var monstrosityNameId2 = dataReader.ReadByte();
            //flags5_t    Flags5 flags5 = dataReader.ReadStruct<Flags5>();
            dataReader.Skip(1);
            var modelHitboxSize = dataReader.ReadByte();
            //flags6_t    Flags6 flags6 = dataReader.ReadStruct<Flags6>();
            dataReader.Skip(4);
            //uint16_t    GrapIDTbl[9];
            dataReader.Skip(18);
        }
        if (updateFlags.HasFlag(SendFlags.Name))
        {
            client.EntityList[charIndex].IsValid = true;
            var name = dataReader.ReadString(dataReader.Remaining);
            client.EntityList[charIndex].Name = name;
        }
        if (updateFlags.HasFlag(SendFlags.Name) || updateFlags.HasFlag(SendFlags.General))
        {
            //var Flag6 = new Flags6 { Raw = BitConverter.ToUInt32(packet, index + 0x44) };
            //Flag6.MountIndex
        }
        if (updateFlags.HasFlag(SendFlags.Despawn))
        {
            client.EntityList[charIndex] = new Entity();
        }
        else
        {
            // var CostumeId = BitConverter.ToUInt16(packet, index + 0x30);
            // var PetActIndex = BitConverter.ToUInt16(packet, index + 0x3C);
        }
    }
}