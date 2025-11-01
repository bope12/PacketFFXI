using System;
using System.Text;


namespace HeadlessFFXI.Networking.Packets
{
    public class P06EBuilder : IPacketBuilder
    {
        public ushort Type => 0x6E;
        public ushort Size => 0x06;
        private readonly uint CharId;
        private readonly ushort CharIndex; // zero if in different zone
        private readonly byte Kind;       // 0 party 5 alliance

        public P06EBuilder(uint charId, ushort charIndex, byte kind)
        {
            CharId = charId;
            CharIndex = charIndex;
            Kind = kind;
        }

        public OutgoingPacket Build()
        {
            byte[] data = new byte[0x0C];
            BitConverter.GetBytes(CharId).CopyTo(data, 0x04);
            BitConverter.GetBytes(CharIndex).CopyTo(data, 0x08);
            data[0x0A] = Kind;
            // Wrap it in your packet object
            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
}
