using System;

namespace HeadlessFFXI.Networking.Packets
{
    public class P05EBuilder : IPacketBuilder
    {
        public ushort Type => 0x5E;
        public ushort Size => 0x0C;
        private readonly uint RectID;
        private readonly byte MyRoomExitBit;
        private readonly byte MyRoomExitMode;

        public P05EBuilder(uint rectID, byte myRoomExitBit = 0, byte myRoomExitMode = 0)
        {
            RectID = rectID;
            MyRoomExitBit = myRoomExitBit;
            MyRoomExitMode = myRoomExitMode;
        }

        public OutgoingPacket Build()
        {
            byte[] data = new byte[0x18];
            BitConverter.GetBytes(RectID).CopyTo(data, 0x04);

            //0x08 float    x;              // PS2: x
            //0x0C float    y;              // PS2: y
            //0x10 float    z;              // PS2: z
            //0x14 uint16_t ActIndex;       // PS2: ActIndex
            data[0x16] = MyRoomExitBit;  // PS2: MyRoomExitBit
            data[0x17] = MyRoomExitMode; // PS2: MyRoomExitMode

            // Wrap it in your packet object
            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
}
