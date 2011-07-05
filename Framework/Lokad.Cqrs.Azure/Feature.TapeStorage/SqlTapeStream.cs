﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace Lokad.Cqrs.Feature.TapeStorage
{
    public class SqlTapeStream : ITapeStream
    {
        readonly string _connectionString;
        readonly string _tableName;
        readonly string _name;

        public SqlTapeStream(string connectionString, string tableName, string name)
        {
            _connectionString = connectionString;
            _tableName = tableName;
            _name = name;
        }

        public IEnumerable<TapeRecord> ReadRecords(long version, int maxCount)
        {
            if (version <= 0)
                throw new ArgumentOutOfRangeException("version", "Must be more than zero.");

            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException("maxCount", "Must be more than zero.");

            // version + maxCount > long.MaxValue, but transformed to avoid overflow
            if (version > long.MaxValue - maxCount)
                throw new ArgumentOutOfRangeException("maxCount", "Record index will exceed long.MaxValue.");

            return Execute(c => ReadRecords(c, version, maxCount), Enumerable.Empty<TapeRecord>());
        }

        public long GetCurrentVersion()
        {
            return Execute(GetCurrentVersion, 0);
        }

        T Execute<T>(Func<SqlConnection, T> func, T defaultValue)
        {
            SqlConnection connection;
            try
            {
                connection = new SqlConnection(_connectionString);
                connection.Open();
            }
            catch (SqlException)
            {
                return defaultValue;
            }

            try
            {
                return func(connection);
            }
            finally
            {
                connection.Dispose();
            }
        }

        public bool TryAppend(byte[] buffer, TapeAppendCondition condition)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (buffer.Length == 0)
                throw new ArgumentException("Buffer must contain at least one byte.");

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                var version = GetCurrentVersion(connection);

                if (!condition.Satisfy(version))
                    return false;

                if (version > long.MaxValue - 1)
                    throw new IndexOutOfRangeException("Version is more than long.MaxValue.");
                version++;

                Append(connection, version, buffer);
            }

            return true;
        }

        public void AppendNonAtomic(IEnumerable<TapeRecord> records)
        {
            if (records == null)
                throw new ArgumentNullException("records");

            if (!records.Any())
                return;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                foreach (var record in records)
                {
                    if (record.Data.Length == 0)
                        throw new ArgumentException("Record must contain at least one byte.");

                    Append(connection, record.Version, record.Data);
                }
            }
        }

        void Append(SqlConnection connection, long index, byte[] record)
        {
            const string text = @"
INSERT INTO [{0}].[{1}] ([Stream], [Version], [Data])
VALUES (@Stream, @Version, @Data)";

            using (var command = new SqlCommand(string.Format(text, SqlTapeStorageFactory.TableSchema, _tableName), connection))
            {
                command.Parameters.AddWithValue("@Stream", _name);
                command.Parameters.AddWithValue("@Version", index);
                command.Parameters.AddWithValue("@Data", record);

                command.ExecuteNonQuery();
            }
        }

        long GetCurrentVersion(SqlConnection connection)
        {
            const string text = "SELECT Max([Version]) FROM [{0}].[{1}] WHERE [Stream] = @Stream";

            using (var command = new SqlCommand(string.Format(text, SqlTapeStorageFactory.TableSchema, _tableName), connection))
            {
                command.Parameters.AddWithValue("@Stream", _name);

                var result = command.ExecuteScalar();
                return result is DBNull ? 0 : (long)result;
            }
        }

        IEnumerable<TapeRecord> ReadRecords(SqlConnection connection, long offset, int count)
        {
            const string text = @"
SELECT TOP(@count) [Version], [Data]
FROM [{0}].[{1}]
WHERE [Stream] = @Stream AND [Version] >= (@version)
ORDER BY [Version]";

            using (var command = new SqlCommand(string.Format(text, SqlTapeStorageFactory.TableSchema, _tableName), connection))
            {
                command.Parameters.AddWithValue("@Stream", _name);
                command.Parameters.AddWithValue("@count", count);
                command.Parameters.AddWithValue("@version", offset);

                var reader = command.ExecuteReader();

                var records = new List<TapeRecord>();

                while (reader.Read())
                {
                    var index = (long) reader["Version"];
                    var data = (byte[]) reader["Data"];

                    records.Add(new TapeRecord(index, data));
                }

                return records;
            }
        }
    }
}