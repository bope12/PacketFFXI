using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace HeadlessFFXI;

public ref struct PacketReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;

    public readonly int Remaining => _data.Length - Position;
    public int Position { get; private set; } = 0;

    public byte ReadByte()
    {
        var value = _data[Position];
        Position += 1;
        return value;
    }

    public sbyte ReadSByte()
    {
        var value = (sbyte)_data[Position];
        Position += 1;
        return value;
    }

    public ushort ReadUInt16()
    {
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(Position, 2));
        Position += 2;
        return value;
    }

    public short ReadInt16()
    {
        var value = BinaryPrimitives.ReadInt16LittleEndian(_data.Slice(Position, 2));
        Position += 2;
        return value;
    }

    public uint ReadUInt32()
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(Position, 4));
        Position += 4;
        return value;
    }

    public int ReadInt32()
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(Position, 4));
        Position += 4;
        return value;
    }

    public float ReadFloat()
    {
        var value = MemoryMarshal.Read<float>(_data.Slice(Position, 4));
        Position += 4;
        return value;
    }

    public double ReadDouble()
    {
        var value = MemoryMarshal.Read<double>(_data.Slice(Position, 8));
        Position += 8;
        return value;
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        var slice = _data.Slice(Position, count);
        Position += count;
        return slice;
    }

    public void Skip(int count)
    {
        Position += count;
    }

    public readonly bool EndOfData => Position >= _data.Length;

    public void Reset()
    {
        Position = 0;
    }

    // --- Extended helpers ---

    /// <summary>
    /// Reads a fixed-length string (optionally null-terminated).
    /// Default encoding is UTF8.
    /// </summary>
    public string ReadString(int length, Encoding? encoding = null)
    {
        var slice = _data.Slice(Position, length);
        Position += length;

        encoding ??= Encoding.UTF8;

        // Trim any trailing nulls
        var terminator = slice.IndexOf((byte)0);
        if (terminator >= 0)
            slice = slice[..terminator];

        return encoding.GetString(slice);
    }

    /// <summary>
    /// Reads an unmanaged struct directly from the buffer.
    /// Example: var header = reader.ReadStruct&lt;MyStruct&gt;();
    /// </summary>
    public T ReadStruct<T>() where T : unmanaged
    {
        var size = Marshal.SizeOf<T>();
        if (Remaining < size)
            throw new InvalidOperationException($"Not enough data to read struct {typeof(T).Name} ({size} bytes).");

        var value = MemoryMarshal.Read<T>(_data.Slice(Position, size));
        Position += size;
        return value;
    }

    public T ReadEnum<T>() where T : Enum
    {
        var size = Marshal.SizeOf(Enum.GetUnderlyingType(typeof(T)));
        return size switch
        {
            1 => (T)(object)ReadByte(),
            2 => (T)(object)ReadUInt16(),
            4 => (T)(object)ReadUInt32(),
            _ => throw new NotSupportedException($"Unsupported enum size {size}")
        };
    }
}