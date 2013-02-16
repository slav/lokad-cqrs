using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace Lokad.Cqrs.TapeStorage
{
    public interface IAppendOnlyStore : IDisposable
    {
        /// <summary>
        /// <para>
        /// Appends data to the stream with the specified name. If <paramref name="expectedStreamVersion"/> is supplied and
        /// it does not match server version, then <see cref="AppendOnlyStoreConcurrencyException"/> is thrown.
        /// </para> 
        /// </summary>
        /// <param name="streamName">The name of the stream, to which data is appended.</param>
        /// <param name="data">The data to append.</param>
        /// <param name="expectedStreamVersion">The server version (supply -1 to append without check).</param>
        /// <exception cref="AppendOnlyStoreConcurrencyException">thrown when expected server version is
        /// supplied and does not match to server version</exception>
        void Append(string streamName, byte[] data, long expectedStreamVersion = -1);
        /// <summary>
        /// Reads the records by stream name.
        /// </summary>
        /// <param name="streamName">The key.</param>
        /// <param name="afterVersion">The after version.</param>
        /// <param name="maxCount">The max count.</param>
        /// <returns></returns>
        IEnumerable<DataWithVersion> ReadRecords(string streamName, long afterVersion, int maxCount);
        /// <summary>
        /// Reads the records across all streams.
        /// </summary>
        /// <param name="afterVersion">The after version.</param>
        /// <param name="maxCount">The max count.</param>
        /// <returns></returns>
        IEnumerable<DataWithKey> ReadRecords(long afterVersion, int maxCount); 

        void Close();
        void ResetStore();
        long GetCurrentVersion();
    }

    public sealed class DataWithVersion
    {
        public readonly long StreamVersion;
        public readonly long StoreVersion;
        public readonly byte[] Data;

        public DataWithVersion(long streamVersion, byte[] data, long storeVersion)
        {
            StreamVersion = streamVersion;
            Data = data;
            StoreVersion = storeVersion;
        }

        public byte[] ToBinary()
        {
            using (var mem = new MemoryStream())
            using (var bin = new BinaryWriter(mem))
            {
                bin.Write(StreamVersion);
                bin.Write(Data.Length);
                bin.Write(Data);
                bin.Write(StoreVersion);
                return mem.ToArray();
            }
        }

        public static DataWithVersion TryGetFromBinary(byte[] source)
        {
            using (var mem = new MemoryStream(source))
            using (var bin = new BinaryReader(mem))
            {
                var streamVersion = bin.ReadInt64();
                var dataLength = bin.ReadInt32();
                var data = bin.ReadBytes(dataLength);
                var storeVersion = bin.ReadInt64();

                return new DataWithVersion(streamVersion, data, storeVersion);
            }
        }
    }
    public sealed class DataWithKey
    {
        public readonly string Key;
        public readonly byte[] Data;
        public readonly long StreamVersion;
        public readonly long StoreVersion;

        public DataWithKey(string key, byte[] data, long streamVersion, long storeVersion)
        {
            Key = key;
            Data = data;
            StreamVersion = streamVersion;
            StoreVersion = storeVersion;
        }

        public byte[] ToBinary()
        {
            using (var mem = new MemoryStream())
            using (var bin = new BinaryWriter(mem))
            {
                bin.Write(Key);
                bin.Write(StreamVersion);
                bin.Write(Data.Length);
                bin.Write(Data);
                bin.Write(StoreVersion);
                return mem.ToArray();
            }
        }

        public static DataWithKey TryGetFromBinary(byte[] source)
        {
            using (var mem = new MemoryStream(source))
            using (var bin = new BinaryReader(mem))
            {
                var key = bin.ReadString();
                var streamVersion = bin.ReadInt64();
                var dataLength = bin.ReadInt32();
                var data = bin.ReadBytes(dataLength);
                var storeVersion = bin.ReadInt64();

                return new DataWithKey(key, data, streamVersion, storeVersion);
            }
        }
    }

    /// <summary>
    /// Is thrown internally, when storage version does not match the condition 
    /// specified in server request
    /// </summary>
    [Serializable]
    public class AppendOnlyStoreConcurrencyException : Exception
    {
        public long ExpectedStreamVersion { get; private set; }
        public long ActualStreamVersion { get; private set; }
        public string StreamName { get; private set; }

        protected AppendOnlyStoreConcurrencyException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context) { }

        public AppendOnlyStoreConcurrencyException(long expectedVersion, long actualVersion, string name)
            : base(
                string.Format("Expected version {0} in stream '{1}' but got {2}", expectedVersion, name, actualVersion))
        {
            StreamName = name;
            ExpectedStreamVersion = expectedVersion;
            ActualStreamVersion = actualVersion;
        }
    }

}