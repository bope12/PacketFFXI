using System;
using System.Collections.Generic;
using System.IO;

namespace HeadlessFFXI
{
    class zlib
    {
        public uint[] jump;
        public void Init()
        {
            FileStream fs = new FileStream(@"C:\SupernovaFFXI\decompress.dat", FileMode.Open);
            uint[] dec = Load_File(fs);
            uint baseslot = dec[0] - sizeof(uint);
            uint[] jumps = new uint[dec.Length];
            for (int i = 0; i < dec.Length; ++i)
            {

                if (dec[i] > 0xff)
                {
                    // Everything over 0xff are pointers.
                    // These pointers will be traversed until we hit data.
                    jumps[i] = (dec[i] - baseslot) / sizeof(UInt32);
                    //ShowDebug("i:%u pointer offset by %u\n", i, ((dec[i] - base) / sizeof(base)));
                    //Console.WriteLine("i:{0:G} pointer offset by {1:G}",i, ((dec[i] - baseslot) / sizeof(UInt32)));
                }
                else
                {
                    // Everything equal or less to 0xff is 8bit data.
                    // The pointers at offsets -3 and -2 in table must be zero for each non-zero data entry
                    // This approach assumes pointers are at least 8bit on the system.
                    jumps[i] = dec[i];
                    //ShowDebug("i:%u data: %u\n", i, (static_cast<uint8>(reinterpret_cast<std::uintptr_t>(jump[i].ptr))));
                    //Console.WriteLine("i:{0:G} data: {1:G}", i, dec[i]);
                }
            }
            jump = new uint[jumps.Length];
            System.Buffer.BlockCopy(jumps, 0, jump, 0, jumps.Length);
        }
        private static uint[] Load_File(FileStream fs)
        {
            uint[] vec = new uint[fs.Length/4];
            byte[] buf = new byte[sizeof(uint)];
            int i = 0;
            while (fs.Position != fs.Length)
            {
                fs.Read(buf);
                vec[i] = (BitConverter.ToUInt32(buf));
                i++;
            }
            return vec;
        }
        public byte[] Decompress()
        {
            return new byte[16];
        }
        /*
        static void populate_jump_table(std::vector<struct zlib_jump> &jump, const std::vector<uint32> &dec)
        {
            jump.resize(dec.size());

            // Base address of dec table, if we substract pointer in dec table, we should should be
            // able to normalize them to offsets starting from 0.
            const uint32 base = dec[0] - sizeof(uint32);

            for (size_t i = 0; i<dec.size(); ++i)
            {
                if (dec[i] > 0xff)
                {
                    // Everything over 0xff are pointers.
                    // These pointers will be traversed until we hit data.
                    jump[i].ptr = jump.data() + (dec[i] - base) / sizeof(base);
                }
                else
                {
                    // Everything equal or less to 0xff is 8bit data.
                    // The pointers at offsets -3 and -2 in table must be zero for each non-zero data entry
                    // This approach assumes pointers are at least 8bit on the system.
                    static_assert(sizeof(std::uintptr_t) >= sizeof(uint8), "Pointer can't hold a 8bit value");
                    jump[i].ptr = reinterpret_cast<void*>(static_cast<std::uintptr_t>(dec[i]));
                    assert(!jump[i].ptr || (!jump[i - 2].ptr && !jump[i - 3].ptr));
                }
            }
        }
        */
    }
}
