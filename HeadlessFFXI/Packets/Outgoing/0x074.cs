using System;
using System.Text;


namespace HeadlessFFXI.Networking.Packets
{
    public class P074Builder : IPacketBuilder
    {
        public ushort Type => 0x74;
        public ushort Size => 0x02;
        private readonly bool Accept;

        public P074Builder(bool accept)
        {
            Accept = accept;
        }

        public OutgoingPacket Build()
        {
            byte[] data = new byte[6];
            data[0x04] = (byte)(Accept ? 0x01 : 0x00);
            
            // Wrap it in your packet object
            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
}
