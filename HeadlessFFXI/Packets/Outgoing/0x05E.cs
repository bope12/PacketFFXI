using System;
using HeadlessFFXI.Packets;
using HeadlessFFXI.Packets.Outgoing;

namespace HeadlessFFXI.Packets.Outgoing
{
    public class P05EBuilder(uint rectId, byte myRoomExitBit = 0, byte myRoomExitMode = 0) : IPacketBuilder
    {
        public ushort Type => 0x5E;
        public ushort Size => 0x0C;
        private readonly uint _rectID = rectId;
        private readonly byte _myRoomExitBit = myRoomExitBit;
        private readonly byte _myRoomExitMode = myRoomExitMode;

        public OutgoingPacket Build()
        {
            var data = new byte[0x18];
            BitConverter.GetBytes(_rectID).CopyTo(data, 0x04);

            //0x08 float    x;              // PS2: x
            //0x0C float    y;              // PS2: y
            //0x10 float    z;              // PS2: z
            //0x14 uint16_t ActIndex;       // PS2: ActIndex
            data[0x16] = _myRoomExitBit;  // PS2: MyRoomExitBit
            data[0x17] = _myRoomExitMode; // PS2: MyRoomExitMode

            // Wrap it in your packet object
            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
}
