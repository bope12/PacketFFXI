using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

public ref struct PacketReader
{
    private ReadOnlySpan<byte> _data;
    private int _offset;

    public int Remaining => _data.Length - _offset;
    public int Position => _offset;

    public PacketReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _offset = 0;
    }

    // --- Primitive Readers ---
    public byte ReadByte()
    {
        byte value = _data[_offset];
        _offset += 1;
        return value;
    }

    public sbyte ReadSByte()
    {
        sbyte value = (sbyte)_data[_offset];
        _offset += 1;
        return value;
    }

    public ushort ReadUInt16()
    {
        ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(_offset, 2));
        _offset += 2;
        return value;
    }

    public short ReadInt16()
    {
        short value = BinaryPrimitives.ReadInt16LittleEndian(_data.Slice(_offset, 2));
        _offset += 2;
        return value;
    }

    public uint ReadUInt32()
    {
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_offset, 4));
        _offset += 4;
        return value;
    }

    public int ReadInt32()
    {
        int value = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_offset, 4));
        _offset += 4;
        return value;
    }

    public float ReadFloat()
    {
        float value = MemoryMarshal.Read<float>(_data.Slice(_offset, 4));
        _offset += 4;
        return value;
    }

    public double ReadDouble()
    {
        double value = MemoryMarshal.Read<double>(_data.Slice(_offset, 8));
        _offset += 8;
        return value;
    }

    // --- Utility methods ---
    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        var slice = _data.Slice(_offset, count);
        _offset += count;
        return slice;
    }

    public void Skip(int count)
    {
        _offset += count;
    }

    public bool EndOfData => _offset >= _data.Length;

    public void Reset()
    {
        _offset = 0;
    }

    // --- Extended helpers ---

    /// <summary>
    /// Reads a fixed-length string (optionally null-terminated).
    /// Default encoding is UTF8.
    /// </summary>
    public string ReadString(int length, Encoding? encoding = null)
    {
        var slice = _data.Slice(_offset, length);
        _offset += length;

        encoding ??= Encoding.UTF8;

        // Trim any trailing nulls
        int terminator = slice.IndexOf((byte)0);
        if (terminator >= 0)
            slice = slice.Slice(0, terminator);

        return encoding.GetString(slice);
    }

    /// <summary>
    /// Reads an unmanaged struct directly from the buffer.
    /// Example: var header = reader.ReadStruct&lt;MyStruct&gt;();
    /// </summary>
    public T ReadStruct<T>() where T : unmanaged
    {
        int size = Marshal.SizeOf<T>();
        if (Remaining < size)
            throw new InvalidOperationException($"Not enough data to read struct {typeof(T).Name} ({size} bytes).");

        T value = MemoryMarshal.Read<T>(_data.Slice(_offset, size));
        _offset += size;
        return value;
    }

    public T ReadEnum<T>() where T : Enum
    {
        int size = Marshal.SizeOf(Enum.GetUnderlyingType(typeof(T)));
        return size switch
        {
            1 => (T)(object)ReadByte(),
            2 => (T)(object)ReadUInt16(),
            4 => (T)(object)ReadUInt32(),
            _ => throw new NotSupportedException($"Unsupported enum size {size}")
        };
    }
}
