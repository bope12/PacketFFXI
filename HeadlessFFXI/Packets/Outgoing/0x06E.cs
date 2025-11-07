using System;
using HeadlessFFXI.Packets;
using HeadlessFFXI.Packets.Outgoing;


namespace HeadlessFFXI.Packets.Outgoing
{
    public class P06EBuilder(uint charId, ushort charIndex, byte kind) : IPacketBuilder
    {
        public ushort Type => 0x6E;
        public ushort Size => 0x06;
        private readonly uint _charId = charId;
        private readonly ushort _charIndex = charIndex; // zero if in different zone
        private readonly byte _kind = kind;       // 0 party 5 alliance

        public OutgoingPacket Build()
        {
            byte[] data = new byte[0x0C];
            BitConverter.GetBytes(_charId).CopyTo(data, 0x04);
            BitConverter.GetBytes(_charIndex).CopyTo(data, 0x08);
            data[0x0A] = _kind;
            // Wrap it in your packet object
            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
}
