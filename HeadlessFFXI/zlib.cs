using System;
using System.Collections.Generic;
using System.IO;

namespace HeadlessFFXI
{
    class zlib
    {
        public uint[] jump;
        public uint[] enc = null;
        public void Init()
        {
            //FileStream fs = new FileStream(@"D:\Supernova\SupernovaFFXI\res\decompress.dat", FileMode.Open);
            uint[] dec = null;
            Load_File(@"D:\Supernova\SupernovaFFXI\res\decompress.dat", ref dec);
            Load_File(@"D:\Supernova\SupernovaFFXI\res\compress.dat", ref enc);
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
                    //Console.WriteLine("i:{0:G} pointer: {1:G} base:{2:G}",i, ((dec[i] - baseslot) / sizeof(UInt32)), sizeof(UInt32));
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
            //System.Buffer.BlockCopy(jumps, 0, jump, 0, dec.Length);
            for (int i = 0; i < jump.Length; i++)
            {
                jump[i] = jumps[i];
                //Console.WriteLine("i:{0:G} data: {1:G}", i, jump[i]);
            }
            //fs.Close();
        }
        private static void Load_File(string filename, ref uint[] array)
        {
            byte[] bytes = File.ReadAllBytes(filename);
            if (bytes.Length % 4 != 0)
                throw new InvalidDataException("File size not multiple of 4 bytes.");

            array = new uint[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
        }

        static int ZlibCompressedSize(int bits) => (bits + 7) / 8;

        // Extract JMPBIT(table, i) where table is bytes and returns bit 0/1
        static int JMPBIT(byte[] table, uint i)
        {
            return (table[i / 8] >> (int)(i & 7)) & 1;
        }

        // Packs `elem` bits from b32 into output starting at bit offset `read`.
        // outOffset: starting index in output where packing begins (C++ used out + 1)
        // Returns 0 on success, -1 on error
        static int ZlibCompressSub(byte[] b32, uint read, uint elem, byte[] output, int outOffset)
        {
            if (b32 == null || output == null) return -1;

            // In C++ they compared compressed_size(elem) > sizeof(uint32) (i.e., >4 bytes)
            if (ZlibCompressedSize((int)elem) > sizeof(uint))
            {
                Console.WriteLine($"zlib_compress_sub: element exceeds 4 bytes ({elem})");
                return -1;
            }

            // Check space: need (read + elem) bits -> bytes = (read+elem+7)/8
            int neededBytes = ZlibCompressedSize((int)(read + elem));
            if (neededBytes + outOffset > output.Length)
            {
                Console.WriteLine($"zlib_compress_sub: ran out of space ({read} : {elem} : {output.Length - outOffset})");
                return -1;
            }

            for (uint i = 0; i < elem; ++i)
            {
                uint shift = (read + i) & 7;
                int v = (int)((read + i) / 8);
                // inv_mask = ~(1 << shift) but we will keep only lower 8 bits for byte operations
                byte invMask = (byte)~(1u << (int)shift);

                // JMPBIT(b32, i) -- note b32 is the bytes of the codeword, where bit i is read
                int bitVal = JMPBIT(b32, i); // 0 or 1
                byte bitToWrite = (byte)(bitVal << (int)shift);

                int outIndex = outOffset + v;
                // ensure index is valid (should be by previous checks)
                output[outIndex] = (byte)((output[outIndex] & invMask) | bitToWrite);
            }

            return 0;
        }

        // Main compress function:
        // input: input bytes
        // output: output buffer (must be large enough)
        // enc: encoding table (uint[] from C++ zlib.enc)
        // Returns number of bytes written (like C++ read+8) or -1 on error or in_sz on "garbage" path
        public int ZlibCompress(byte[] input, ref byte[] output)
        {
            if (input == null || output == null) throw new ArgumentNullException();
            if (enc == null || enc.Length == 0) throw new InvalidOperationException("zlib.enc is empty");

            uint read = 0;
            uint outSz = (uint)output.Length;
            // max bits we can write (out buffer less one leading byte) -> (outSz - 1) * 8
            if (outSz == 0) return -1;
            uint maxBits = (outSz - 1) * 8;

            for (int i = 0; i < input.Length; ++i)
            {
                int sym = (sbyte)input[i]; // C++ used int8 index
                uint elemIndex = (uint)(sym + 0x180);
                if (elemIndex >= enc.Length)
                {
                    Console.WriteLine($"zlib_compress: enc index out of range: {elemIndex}");
                    return -1;
                }

                uint elem = enc[elemIndex];

                if (elem + read < maxBits)
                {
                    int index = sym + 0x80;
                    if (index < 0 || index >= enc.Length)
                    {
                        Console.WriteLine($"zlib_compress: enc index out of range: {index}");
                        return -1;
                    }

                    uint v = enc[index];
                    byte[] b32 = BitConverter.GetBytes(v); // little-endian bytes of v

                    // write into output starting at out + 1 (so outOffset = 1)
                    int rc = ZlibCompressSub(b32, read, elem, output, 1);
                    if (rc != 0) return rc;

                    read += elem;
                }
                else if (input.Length + 1 >= output.Length)
                {
                    // C++ "garbage" path
                    Console.WriteLine($"zlib_compress: ran out of space, outputting garbage(?) ({read} : {elem} : {maxBits} : {input[i]})");

                    // Behavior in C++:
                    // memset(out, 0, (out_sz / 4) + (in_sz & 3));
                    // memset(out + 1, in_sz, in_sz / 4);
                    // memset(out + 1 + in_sz / 4, (in_sz + 1) * 8, in_sz & 3);
                    int part1 = (output.Length / 4) + (input.Length & 3);
                    Array.Clear(output, 0, Math.Min(part1, output.Length));

                    int part2Count = input.Length / 4;
                    int part2Start = 1;
                    for (int k = 0; k < part2Count && (part2Start + k) < output.Length; ++k)
                        output[part2Start + k] = (byte)input.Length;

                    int part3Start = 1 + (input.Length / 4);
                    int part3Count = input.Length & 3;
                    byte fillVal = (byte)((input.Length + 1) * 8);
                    for (int k = 0; k < part3Count && (part3Start + k) < output.Length; ++k)
                        output[part3Start + k] = fillVal;

                    return input.Length;
                }
                else
                {
                    Console.WriteLine($"zlib_compress: ran out of space ({read} : {elem} : {maxBits} : {input[i]})");
                    return -1;
                }
            }

            // success path: set output[0] = 1 and return bitsWritten+8 as in C++
            output[0] = 1;
            return (int)(read + 8);
        }
    }
}
