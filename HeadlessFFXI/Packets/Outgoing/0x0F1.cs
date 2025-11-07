using System;

namespace HeadlessFFXI.Packets.Outgoing
{
    public class P0F1Builder(ushort buffId) : IPacketBuilder
    {
        public ushort Type => 0xF1;
        public ushort Size => 0x04;
        public ushort BuffId = buffId;

        public OutgoingPacket Build()
        {
            var data = new byte[8];
            BitConverter.GetBytes(BuffId).CopyTo(data, 0x04);
            // Wrap it in your packet object
            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
}
