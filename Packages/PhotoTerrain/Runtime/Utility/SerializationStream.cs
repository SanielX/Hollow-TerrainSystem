using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;

namespace Hollow
{
// Copypasted from my game's code, probably an overkill?
public ref struct SerializationArrayWriter<T> where T : unmanaged
{
    internal SerializationArrayWriter(SerializationStream stream)
    {
        this.stream = stream;
        begin       = stream.Position;
        count       = 0;

        stream.SerializeBytes(ref count);
    }

    private SerializationStream stream;
    private int                 begin;
    private int                 count;

    public void WriteBits(ref T value)
    {
        stream.SerializeBytes(ref value);
        ++count;
    }

    public void End() => Dispose();

    public void Dispose()
    {
        int end = stream.Position;

        stream.Position = begin;
        stream.SerializeBytes(ref count);

        stream.Position = end;
    }
}

public ref struct SerializationLengthWriter
{
    internal SerializationLengthWriter(SerializationStream stream)
    {
        this.stream = stream;
        begin       = stream.Position;

        int len = 0;
        stream.SerializeBytes(ref len);
    }

    private SerializationStream stream;
    private int                 begin;

    public int End()
    {
        int end = stream.Position;
        int len = end - begin - 4;
        Assert.IsTrue(len >= 0, "len >= 0");

        stream.Position = begin;
        stream.SerializeBytes(ref len);

        stream.Position = end;

        return len;
    }

    public void Dispose()
    {
        End();
    }
}

public unsafe struct SerializationStream : IDisposable
{
    public SerializationStream(int capacity, Allocator allocator)
    {
        stream    = (UnsafeSerializationStream*)UnsafeUtility.Malloc(sizeof(UnsafeSerializationStream), UnsafeUtility.AlignOf<UnsafeSerializationStream>(), allocator);
        stream[0] = new(capacity, allocator);

        this.allocator = allocator;
    }

    public SerializationStream(NativeArray<byte> array, Allocator allocator)
    {
        stream    = (UnsafeSerializationStream*)UnsafeUtility.Malloc(sizeof(UnsafeSerializationStream), UnsafeUtility.AlignOf<UnsafeSerializationStream>(), allocator);
        stream[0] = new(array);

        this.allocator = allocator;
    }

    public void Dispose()
    {
        stream->Dispose();
        UnsafeUtility.Free(stream, allocator);

        stream = null;
    }

    private UnsafeSerializationStream* stream;
    private Allocator                  allocator;

    public UnsafeSerializationStream* GetUnsafeStreamPtr() => stream;

    public bool IsCreated => stream is not null;

    public int Position
    {
        get => stream->Position;
        set => stream->Position = value;
    }

    /// <inheritdoc cref="UnsafeSerializationStream.IsWriting"/>
    public bool IsWriting
    {
        get => stream->IsWriting;
        set => stream->IsWriting = value;
    }

    public bool IsReading
    {
        get => !IsWriting;
        set => IsWriting = !value;
    }

    /// <inheritdoc cref="UnsafeSerializationStream.Length"/>
    public int Length => stream->Length;

    public int BytesLeft => stream->BytesLeft;

    public void WriteBytes<T>(T[] array) where T : unmanaged
    {
        stream->WriteBytes((ReadOnlySpan<T>)array.AsSpan());
    }

    public void ReadBytes<T>(ref T[] array) where T : unmanaged
    {
        stream->ReadBytes(out ReadOnlySpan<T> span);

        if (array is null || array.Length != span.Length)
            array = new T[span.Length];

        span.CopyTo(array);
    }

    public void WriteBytes(string     str) => WriteBytes(str.AsSpan());

    public void ReadBytes (out string str)
    {
        ReadBytes(out ReadOnlySpan<char> chars);
        str = chars.ToString();
    }

    public void WriteBytes<T>(ReadOnlySpan<T> array) where T : unmanaged
    {
        stream->WriteBytes(array);
    }

    public void ReadBytes<T>(out ReadOnlySpan<T> array) where T : unmanaged
    {
        stream->ReadBytes(out array);
    }

    public void WriteBytes(string[] array)
    {
        stream->WriteBytes(array.Length);
        for (int i = 0; i < array.Length; i++)
        {
            stream->WriteString(array[i].AsSpan());
        }
    }

    public void ReadBytes(out string[] array)
    {
        stream->ReadBytes(out int arrayLength);
        array = new string[arrayLength];

        for (int i = 0; i < arrayLength; i++)
        {
            stream->ReadString(out array[i]);
        }
    }

    public void WriteBytes<T>(in T value) where T : unmanaged
    {
        stream->WriteBytes(value);
    }

    public void ReadBytes<T>(out T value) where T : unmanaged
    {
        stream->ReadBytes(out value);
    }

    public void SerializeBytes<T>(ref T value) where T : unmanaged
    {
        stream->SerializeBytes(ref value);
    }

    public void SerializeBytes<T>(ref ReadOnlySpan<T> value) where T : unmanaged
    {
        stream->SerializeBytes(ref value);
    }

    public void SerializeBytes(ref string value)
    {
        ReadOnlySpan<char> str = value.AsSpan();
        stream->SerializeBytes(ref str);

        if (IsReading)
            value = str.ToString();
    }

    public void SerializeBytes<T>(ref T[] value) where T : unmanaged
    {
        ReadOnlySpan<T> str = value.AsSpan();
        stream->SerializeBytes(ref str);

        if (IsReading)
        {
            if (value.Length == str.Length)
                str.CopyTo(value.AsSpan());
            else
                value = str.ToArray();
        }
    }

    public void SerializeJson<T>(ref T value)
    {
        stream->SerializeJson(ref value);
    }

    public SerializationArrayWriter<T> BeginArrayWrite<T>() where T : unmanaged
    {
        Assert.IsTrue(IsWriting, "Stream must be writing to begin writing an array");
        return new(this);
    }

    public SerializationLengthWriter BeginLengthWrite()
    {
        Assert.IsTrue(IsWriting, "Stream must be writing to begin writing an array");
        return new(this);
    }

    public void Clear() => stream->Clear();

    public void CopyTo(ref byte[] storedBytes)
    {
        if (storedBytes is null || storedBytes.Length < Length)
            storedBytes = new byte[Length];

        fixed (byte* bytes = &storedBytes[0])
            stream->CopyBytes(bytes, storedBytes.Length);
    }

    public readonly ReadOnlySpan<byte> AsReadOnlyByteSpan() => stream->AsReadOnlyByteSpan();
}

[BurstCompile]
public unsafe struct UnsafeSerializationStream
{
    public UnsafeSerializationStream(int capacity, Allocator allocator)
    {
        bytes        = new(capacity, allocator);
        position     = 0;
        isWriting    = 1;
        streamLength = 0;
    }

    public UnsafeSerializationStream(NativeArray<byte> array)
    {
        bytes        = array;
        position     = 0;
        isWriting    = 0;
        streamLength = array.Length;
    }

    public int Position
    {
        readonly get => position;
        set
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (value < 0)
                throw new System.Exception("Stream position can't be less than 0");
            if (value > bytes.Length)
                throw new System.Exception("Stream position can't be greater than its capacity");
#endif

            position = value;

            if (isWriting != 0) // Position can be moved around, length only grows
                streamLength = Mathf.Max(streamLength, position);
        }
    }

    public readonly int BytesLeft => IsWriting ? bytes.Length - Position : streamLength - Position;

    /// <summary> Points to internal buffer, note that advancing position MAY MAKE THIS POINTER INVALID DUE TO RESIZING so please never store it anywhere </summary>
    private readonly byte* Ptr => (byte*)bytes.GetUnsafePtr() + Position;

    private NativeArray<byte> bytes;
    private int               position;
    private byte              isWriting;
    private int               streamLength;

    public bool IsWriting
    {
        readonly get => isWriting != 0;
        set => isWriting = value ? (byte)1 : (byte)0;
    }

    /// <summary> Total amount of bytes written </summary>
    public int Length => streamLength;

    public readonly bool CheckBytes(int needed)
    {
        if (isWriting != 0) return bytes.Length - Position >= needed;

        return streamLength - Position >= needed;
    }

    const string writing_keyword = "<color=#1aff00>Writing</color>";
    const string reading_keyword = "<color=#00fff7>Reading</color>";

    public readonly ReadOnlySpan<byte> AsReadOnlyByteSpan() => new((byte*)bytes.GetUnsafePtr(), streamLength);

    public void SerializeBytes<T>(ref T value) where T : unmanaged
    {
        if (isWriting != 0)
        {
            WriteBytes(value);
        }
        else
        {
            ReadBytes(out value);
        }
    }

    public void WriteBytes<T>(in T value) where T : unmanaged
    {
        // Debug.Log($"SerStream: {writing_keyword} t:{typeof(T).Name}, at {position}, size: {sizeof(T)}, value: {value}");
        fixed (T* ptr = &value)
        {
            WriteBytes(ref this, (byte*)ptr, sizeof(T));
        }
    }

    public void ReadBytes<T>(out T value) where T : unmanaged
    {
        fixed (T* ptr = &value)
        {
            ReadBytes(ref this, (byte*)ptr, sizeof(T));
        }
        // Debug.Log($"SerStream: {reading_keyword} t:{typeof(T).Name}, at <color=#1aff00>{pos}</color>, size: {sizeof(T)}, value: {value}");
    }

    public void SerializeBytes<T>(ref ReadOnlySpan<T> value) where T : unmanaged
    {
        if (isWriting != 0)
        {
            //  Debug.Log($"SerStream: {writing_keyword} t:{typeof(T).Name}, at <color=#1aff00>'{position}'</color>, size: {sizeof(T)}, array count: {value.Length}");
            WriteBytes(value);
        }
        else
        {
            ReadBytes(out value);
            // Debug.Log($"SerStream: {reading_keyword} t:{typeof(T).Name}, at {pos}, size: {sizeof(T)}, array count: {value.Length}");
        }
    }

    public void WriteBytes<T>(ReadOnlySpan<T> value) where T : unmanaged
    {
        WriteBytes(value.Length);
        fixed (T* ptr = value)
        {
            WriteBytes(ref this, (byte*)ptr, sizeof(T) * value.Length);
        }
    }

    public void ReadBytes<T>(out ReadOnlySpan<T> value) where T : unmanaged
    {
        ReadBytes(out int spanLength);
        value = GetTypedSpan<T>(spanLength);
    }

    public void SerializeJson<T>(ref T value, bool prettyPrint = false)
    {
        if (isWriting != 0)
        {
            var json = JsonUtility.ToJson(value, prettyPrint);
            WriteString(json, TextEncodingType.Unicode);
        }
        else
        {
            ReadString(out var json, TextEncodingType.Unicode);
            value = JsonUtility.FromJson<T>(json);
        }
    }

    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    private readonly void CheckBytesAndThrow(int needed)
    {
        if (!CheckBytes(needed))
            throw new System.OutOfMemoryException($"Serialization stream is out of space (needed: {needed}, bytesLeft: {BytesLeft}, position: {Position}, Capacity: {bytes.Length}");
    }

    public void WriteString(ReadOnlySpan<char> str, TextEncodingType encoding = TextEncodingType.Unicode)
    {
        int strLength = StringEncodingUtility.GetByteCount(str, encoding);

        var bytesSpan = GetSpan(strLength + sizeof(int));

        BitConverter         .TryWriteBytes(bytesSpan[0..4],  strLength);
        StringEncodingUtility.WriteBytes(str, bytesSpan[4..], encoding);
    }

    public void ReadString(out string result, TextEncodingType encoding = TextEncodingType.Unicode)
    {
        int length = BitConverter.ToInt32(new ReadOnlySpan<byte>(Ptr, sizeof(int)));

        ReadOnlySpan<byte> strBytes = new(Ptr + 4, length);
        result = StringEncodingUtility.Decode(strBytes, encoding);

        Position += 4 + length;
    }

    private Span<byte> GetSpan(int length)
    {
        CheckBytesAndThrow(length);

        Position += length;

        return new(Ptr - length, length);
    }

    private Span<T> GetTypedSpan<T>(int length) where T : unmanaged
    {
        int count = length;
        length *= sizeof(T);

        CheckBytesAndThrow(length);

        Position += length;

        return new(Ptr - length, count);
    }

    [BurstCompile]
    private static void ReadBytes(ref UnsafeSerializationStream stream, byte* bytes, int length)
    {
        Assert.IsTrue(!stream.IsWriting, "stream.IsReading");

        stream.CheckBytesAndThrow(length);

        UnsafeUtility.MemCpy(bytes, stream.Ptr, length);
        stream.Position += length;
    }

    [BurstCompile]
    private static void WriteBytes(ref UnsafeSerializationStream stream, byte* bytes, int length)
    {
        Assert.IsTrue(stream.IsWriting, "stream.IsWriting");

        stream.CheckBytesAndThrow(length);

        stream.Position += length;
        var ptr = stream.Ptr - length;

        UnsafeUtility.MemCpy(ptr, bytes, length);
    }

    public void CopyBytes(byte* dst, int length)
    {
        var pos = Position;
        Position = 0;
        ReadBytes(ref this, dst, length);
        Position = pos;
    }

    public void Clear()
    {
        streamLength = 0;
        position     = 0;
    }

    public void Dispose()
    {
        bytes.Dispose();
    }
}


public enum TextEncodingType
{
    ASCII,
    UTF7,
    UTF8,
    UTF32,

    /// <summary> UTF16, default encoding used by strings in C# </summary>
    Unicode
}

public static class StringEncodingUtility
{
    public static int GetByteCount(ReadOnlySpan<char> text, TextEncodingType type) => type switch
    {
        TextEncodingType.ASCII   => System.Text.Encoding.ASCII  .GetByteCount(text),
        TextEncodingType.UTF7    => System.Text.Encoding.UTF7   .GetByteCount(text),
        TextEncodingType.UTF8    => System.Text.Encoding.UTF8   .GetByteCount(text),
        TextEncodingType.UTF32   => System.Text.Encoding.UTF32  .GetByteCount(text),
        TextEncodingType.Unicode => System.Text.Encoding.Unicode.GetByteCount(text),
        _                        => throw new System.ArgumentException(nameof(type)),
    };

    public static void WriteBytes(ReadOnlySpan<char> text, Span<byte> span, TextEncodingType type)
    {
        switch (type)
        {
        default:
            throw new System.ArgumentException(nameof(type));

        case TextEncodingType.ASCII:
            System.Text.Encoding.ASCII.GetBytes(text, span);
            break;

        case TextEncodingType.UTF7:
            System.Text.Encoding.UTF7.GetBytes(text, span);
            break;

        case TextEncodingType.UTF8:
            System.Text.Encoding.UTF8.GetBytes(text, span);
            break;

        case TextEncodingType.UTF32:
            System.Text.Encoding.UTF32.GetBytes(text, span);
            break;

        case TextEncodingType.Unicode:
            System.Text.Encoding.Unicode.GetBytes(text, span);
            break;
        }
    }

    public static string Decode(this ReadOnlySpan<byte> bytes, TextEncodingType encoding) => encoding switch
    {
        TextEncodingType.ASCII   => System.Text.Encoding.ASCII  .GetString(bytes),
        TextEncodingType.UTF7    => System.Text.Encoding.UTF7   .GetString(bytes),
        TextEncodingType.UTF8    => System.Text.Encoding.UTF8   .GetString(bytes),
        TextEncodingType.UTF32   => System.Text.Encoding.UTF32  .GetString(bytes),
        TextEncodingType.Unicode => System.Text.Encoding.Unicode.GetString(bytes),
        _                        => throw new System.ArgumentException("Invalid encoding type", nameof(encoding)),
    };

    public static bool IsValidEncodingType(TextEncodingType type)
    {
        return (type) switch
        {
            TextEncodingType.ASCII   => true,
            TextEncodingType.UTF7    => true,
            TextEncodingType.UTF8    => true,
            TextEncodingType.UTF32   => true,
            TextEncodingType.Unicode => true,
            _                        => false
        };
    }
}
}