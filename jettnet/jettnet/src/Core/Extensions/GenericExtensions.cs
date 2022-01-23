﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using jettnet.Core.Extensions;

namespace jettnet.mirage.bitpacking
{
    public static class Reader<T>
    {
        public static Func<JettReader, T> Read;
    }
    
    public static class Writer<T>
    {
        public static Action<JettWriter, T> Write;
    }

    public static class GenericExtensions
    {
        static GenericExtensions()
        {
            Reader<int>.Read = (reader) => reader.ReadPackedInt32();
            Writer<int>.Write = (writer, value) => writer.WritePackedInt32(value);
            
            Reader<uint>.Read = (reader) => reader.ReadPackedUInt32();
            Writer<uint>.Write = (writer, value) => writer.WritePackedUInt32(value);
            
            Reader<float>.Read = (reader) => reader.ReadSingle();
            Writer<float>.Write = (writer, value) => writer.Write(value);
            
            Reader<double>.Read = (reader) => reader.ReadDouble();
            Writer<double>.Write = (writer, value) => writer.Write(value);
            
            Reader<long>.Read = (reader) => reader.ReadPackedInt64();
            Writer<long>.Write = (writer, value) => writer.WritePackedInt64(value);
            
            Reader<ulong>.Read = (reader) => reader.ReadPackedUInt64();
            Writer<ulong>.Write = (writer, value) => writer.WriteUInt64(value);
            
            Reader<short>.Read = (reader) => reader.ReadInt16();
            Writer<short>.Write = (writer, value) => writer.Write(value);
            
            Reader<ushort>.Read = (reader) => reader.ReadUInt16();
            Writer<ushort>.Write = (writer, value) => writer.Write(value);
            
            Reader<sbyte>.Read = (reader) => reader.ReadSByte();
            Writer<sbyte>.Write = (writer, value) => writer.Write(value);
            
            Reader<byte>.Read = (reader) => reader.ReadByte();
            Writer<byte>.Write = (writer, value) => writer.Write(value);
            
            Reader<bool>.Read = (reader) => reader.ReadBoolean();
            Writer<bool>.Write = (writer, value) => writer.Write(value);
            
            Reader<string>.Read = (reader) => reader.ReadString();
            Writer<string>.Write = (writer, value) => writer.Write(value);
        }

        /// <summary>
        /// Writes any type that jettnet supports
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(this JettWriter writer, T value)
        {
            var serializer = Writer<T>.Write;
            
            if (serializer != null)
            {
                serializer(writer, value);
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
            var deserializer = Reader<T>.Read;
            
            if(deserializer != null)
            {
                return deserializer(reader);
            }
            
            ThrowIfReaderNotFound<T>();
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ThrowIfReaderNotFound<T>()
        {
            throw new
                KeyNotFoundException($"No reader found for {typeof(T)}.");
        }
    }
}