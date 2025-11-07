using System;

namespace HeadlessFFXI.Networking.Packets
{
    public class P0E8Builder : IPacketBuilder
    {
        public ushort Type => 0xE8;
        public ushort Size => 0x04;
        public uint Mode;

        public P0E8Builder(HealMode mode)
        {
            Mode = (uint)mode;
        }

        public OutgoingPacket Build()
        {
            byte[] data = new byte[8];
            BitConverter.GetBytes(Mode).CopyTo(data, 0x04);
            // Wrap it in your packet object
            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
    public enum HealMode
    {
        Toggle = 0x00,
        On = 0x01,
        Off = 0x02
    }
}
