using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using jettnet.Core.Extensions;

namespace jettnet.mirage.bitpacking
{
    public class GenericSerializer<T> : ISerializer
    {
        public Func<JettReader, T>   Read;
        public Action<JettWriter, T> Write;
    }

    public interface ISerializer
    {
    }

    public static class GenericExtensions
    {
        private static readonly Dictionary<Type, ISerializer> _serializers = new Dictionary<Type, ISerializer>
        {
            {
                typeof(int),
                new GenericSerializer<int> 
                    {Read = (reader => reader.ReadInt32()), Write = (writer, i) => writer.Write(i)}
            },
            {
                typeof(bool),
                new GenericSerializer<bool>
                    {Read = (reader => reader.ReadBoolean()), Write = (writer, b) => writer.Write(b)}
            },
            {
                typeof(string),
                new GenericSerializer<string>
                    {Read = (reader => reader.ReadString()), Write = (writer, s) => writer.Write(s)}
            },
            {
                typeof(byte),
                new GenericSerializer<byte> 
                    {Read = (reader => reader.ReadByte()), Write = (writer, b) => writer.Write(b)}
            },
            {
                typeof(ushort),
                new GenericSerializer<ushort>
                    {Read = (reader => reader.ReadUInt16()), Write = (writer, s) => writer.Write(s)}
            },
            {
                typeof(float),
                new GenericSerializer<float>
                    {Read = (reader => reader.ReadSingle()), Write = (writer, f) => writer.Write(f)}
            },
            {
                typeof(short),
                new GenericSerializer<short>
                    {Read = (reader => reader.ReadInt16()), Write = (writer, s) => writer.Write(s)}
            },
            {
                typeof(uint),
                new GenericSerializer<uint>
                    {Read = (reader => reader.ReadUInt32()), Write = (writer, u) => writer.Write(u)}
            },
            {
                typeof(ulong),
                new GenericSerializer<ulong>
                    {Read = (reader => reader.ReadUInt64()), Write = (writer, u) => writer.Write(u)}
            },
            {
                typeof(ulong),
                new GenericSerializer<ulong>
                    {Read = (reader => reader.ReadUInt64()), Write = (writer, u) => writer.Write(u)}
            },
            {
                typeof(long),
                new GenericSerializer<long>
                    {Read = (reader => reader.ReadInt64()), Write = (writer, l) => writer.Write(l)}
            },
            {
                typeof(double),
                new GenericSerializer<double>
                    {Read = (reader => reader.ReadDouble()), Write = (writer, d) => writer.Write(d)}
            },
            {
                typeof(sbyte),
                new GenericSerializer<sbyte>
                    {Read = (reader => reader.ReadSByte()), Write = (writer, d) => writer.Write(d)}
            },
        };

        /// <summary>
        /// Writes any type that jettnet supports
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(this JettWriter writer, T value)
        {
            if (_serializers.TryGetValue(typeof(T), out ISerializer serializer))
            {
                GenericSerializer<T> typedSerializer = (GenericSerializer<T>) serializer;
                typedSerializer.Write(writer, value);
            }
            else
                ThrowIfWriterNotFound<T>();
        }

        static void ThrowIfWriterNotFound<T>()
        {
            throw new
                KeyNotFoundException($"No writer found for {typeof(T)}.");
        }

        /// <summary>
        /// Reads any data type that jettnet supports
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this JettReader reader)
        {
            if(_serializers.TryGetValue(typeof(T), out ISerializer serializer))
            {
                GenericSerializer<T> typedSerializer = (GenericSerializer<T>) serializer;
                return typedSerializer.Read(reader);
            }
            
            ThrowIfReaderNotFound<T>();
            return default;
        }

        static void ThrowIfReaderNotFound<T>()
        {
            throw new
                KeyNotFoundException($"No reader found for {typeof(T)}.");
        }
    }
}