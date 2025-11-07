using System;
using System.Text;

namespace HeadlessFFXI.Packets.Outgoing
{
    public class P0B6Builder(string user, string message) : IPacketBuilder
    {
        public ushort Type => 0xB6;
        public ushort Size;
        private readonly string _user = user ?? throw new ArgumentNullException(nameof(user));
        private readonly string _message = message ?? throw new ArgumentNullException(nameof(message));

        public OutgoingPacket Build()
        {
            // Calculate packet size — same as your original logic.
            Size = (ushort)(12 + (Math.Floor(_message.Length / 4.0) * 2));
            byte[] data = new byte[Size * 2];

            // Type flag
            data[0x04] = 0x03;

            // Copy username (ASCII)
            byte[] userBytes = Encoding.ASCII.GetBytes(_user);
            Buffer.BlockCopy(userBytes, 0, data, 0x06, userBytes.Length);

            // Copy message (UTF8)
            byte[] msgBytes = Encoding.UTF8.GetBytes(_message);
            Buffer.BlockCopy(msgBytes, 0, data, 0x15, msgBytes.Length);

            // Wrap it in your packet object
            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
}
