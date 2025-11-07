using System;

namespace HeadlessFFXI.Packets.Outgoing
{
    public class P015Builder(MyPlayer player) : IPacketBuilder
    {
        public ushort Type => 0x015;
        public ushort Size => 0x10;
        private MyPlayer _player = player;

        public OutgoingPacket Build()
        {
            byte[] data = new byte[0x20];

            // Position
            BitConverter.GetBytes(_player.pos.X).CopyTo(data, 0x04);
            BitConverter.GetBytes(_player.pos.Y).CopyTo(data, 0x08);
            BitConverter.GetBytes(_player.pos.Z).CopyTo(data, 0x0C);

            // Movement tracking
            if (_player.pos.HasChanged(_player.oldpos))
            {
                _player.pos.Moving = (ushort)(_player.pos.Moving + 7);
                _player.oldpos = _player.pos;
            }
            else
            {
                _player.pos.Moving = 0;
            }

            BitConverter.GetBytes(_player.pos.Moving).CopyTo(data, 0x12);
            data[0x14] = (byte)_player.pos.Rotation;

            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
}
