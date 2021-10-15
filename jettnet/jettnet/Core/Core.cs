﻿using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

// pool pattern derived from mirror networking's pooled writers/readers

namespace jettnet
{
    public static class JettChannels
    {
        public const int Reliable = 0;
        public const int Unreliable = 1;
    }

    public struct ConnectionData
    {
        public int ClientId;
        public string Address;
        public ushort Port;
    }


    public enum Messages : byte
    {
        MessageReceived = 3,
        Message = 4
    }

    public static class IdExtenstions
    {
        private static MD5 _crypto = MD5.Create();

        public static int ToID(this string s)
        {
            var result = _crypto.ComputeHash(Encoding.UTF8.GetBytes(s));
            return BitConverter.ToInt32(result, 0);
        }
    }

    // https://docs.microsoft.com/en-us/dotnet/standard/collections/thread-safe/how-to-create-an-object-pool
    public class ObjectPool<T>
    {
        private readonly ConcurrentBag<T> _objects;
        private readonly Func<T> _objectGenerator;

        public ObjectPool(Func<T> objectGenerator)
        {
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _objects = new ConcurrentBag<T>();
        }

        public T Get() => _objects.TryTake(out T item) ? item : _objectGenerator();

        public void Return(T item) => _objects.Add(item);
    }

    public interface IJettMessage<T> : IJettMessage where T : struct
    {
        T Deserialize(JettReader reader);
    }
    
    public interface IJettMessage 
    {
        void Serialize(JettWriter writer);
    }

    public sealed class JettWriterPool
    {
        private static readonly ObjectPool<PooledJettWriter> _writers = new ObjectPool<PooledJettWriter>(() => new PooledJettWriter());

        public static PooledJettWriter Get()
        {
            return GetInternal();
        }

        private static PooledJettWriter GetInternal()
        {
            var writer = _writers.Get();
            writer.Position = 0;
            return writer;
        }

        public static PooledJettWriter Get(Messages header)
        {
            var writer = GetInternal();

            writer.WriteByte((byte)header);

            return writer;
        }

        public static void Return(PooledJettWriter jettWriter)
        {
            _writers.Return(jettWriter);
        }
    }

    public sealed class JettReaderPool
    {
        private static readonly ObjectPool<PooledJettReader> _readers = new ObjectPool<PooledJettReader>(() => new PooledJettReader());

        public static PooledJettReader Get(int pos, ArraySegment<byte> data)
        {
            var reader = _readers.Get();

            reader.Position = pos;
            reader.Buffer = data;

            return reader;
        }

        public static void Return(PooledJettReader jettReader)
        {
            _readers.Return(jettReader);
        }
    }

    public sealed class PooledJettWriter : JettWriter, IDisposable
    {
        public void Dispose()
        {
            JettWriterPool.Return(this);
        }
    }

    public sealed class PooledJettReader : JettReader, IDisposable
    {
        public void Dispose()
        {
            JettReaderPool.Return(this);
        }
    }

    public class JettWriter
    {
        public ArraySegment<byte> Buffer = new ArraySegment<byte>(new byte[1200]);
        public int Position = 0;
    }

    public class JettReader
    {
        public ArraySegment<byte> Buffer;
        public int Position = 0;
    }

    public static class JettReadWriteExtensions
    {
        public static void WriteByte(this JettWriter writer, byte value)
        {
            writer.Buffer.Array[writer.Position] = value;
            writer.Position += 1;
        }

        public static byte ReadByte(this JettReader reader)
        {
            byte value = reader.Buffer.Array[reader.Position];
            reader.Position += 1;
            return value;
        }

        public static void WriteBool(this JettWriter writer, bool value)
        {
            unsafe
            {
                fixed (byte* dataPtr = &writer.Buffer.Array[writer.Position])
                {
                    bool* valuePtr = (bool*)dataPtr;
                    *valuePtr = value;
                    writer.Position += 1;
                }
            }
        }

        public static bool ReadBool(this JettReader reader)
        {
            bool value = BitConverter.ToBoolean(reader.Buffer.Array, reader.Position);
            reader.Position += 1;
            return value;
        }

        public static void WriteString(this JettWriter writer, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                // Incase string is null or empty, just write nothing.
                writer.WriteInt(0);
            }
            else
            {
                writer.WriteInt(value.Length);

                for (int i = 0; i < value.Length; i++)
                    writer.WriteChar(value[i]);
            }
        }

        public static string ReadString(this JettReader reader)
        {
            string value = default;

            int stringSize = reader.ReadInt();

            for (int i = 0; i < stringSize; i++)
                value += reader.ReadChar();

            return value;
        }

        public static void WriteBytes(this JettWriter writer, byte[] value)
        {
            if(value == null)
            {
                writer.WriteInt(-1);
                return;
            }

            writer.WriteInt(value.Length);

            for (int i = 0; i < value.Length; i++)
                writer.WriteByte(value[i]);
        }

        public static byte[] ReadBytes(this JettReader reader)
        {
            int byteSize = reader.ReadInt();

            byte[] value = byteSize == -1 ? new byte[0] : new byte[byteSize];

            if (byteSize == -1)
                return value;

            for (int i = 0; i < byteSize; i++)
                value[i] = reader.ReadByte();

            return value;
        }

        public static void WriteChar(this JettWriter writer, char value)
        {
            unsafe
            {
                fixed (byte* dataPtr = &writer.Buffer.Array[writer.Position])
                {
                    char* valuePtr = (char*)dataPtr;
                    *valuePtr = value;
                    writer.Position += 2;
                }
            }
        }

        public static char ReadChar(this JettReader reader)
        {
            char value = BitConverter.ToChar(reader.Buffer.Array, reader.Position);
            reader.Position += 2;
            return value;
        }

        public static void WriteInt(this JettWriter writer, int value)
        {
            unsafe
            {
                fixed (byte* dataPtr = &writer.Buffer.Array[writer.Position])
                {
                    int* valuePtr = (int*)dataPtr;
                    *valuePtr = value;

                    writer.Position += 4;
                }
            }
        }

        public static int ReadInt(this JettReader reader)
        {
            int value = BitConverter.ToInt32(reader.Buffer.Array, reader.Position);
            reader.Position += 4;
            return value;
        }
    }
}