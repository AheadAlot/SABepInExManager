using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Data;
using Microsoft.Data.Sqlite;
using SABepInExManager.Core.Models;

namespace SABepInExManager.Core.Services;

public sealed class AutoUpdaterSqliteStateStore
{
    public AutoUpdaterSyncState Load(string dbPath)
    {
        EnsureSchema(dbPath);

        var state = new AutoUpdaterSyncState();
        using var connection = CreateOpenConnection(dbPath);

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT key, value FROM auto_updater_meta;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var key = reader.GetString(0);
                var value = reader.GetString(1);
                switch (key)
                {
                    case "app_id":
                        state.AppId = value;
                        break;
                    case "last_run_at":
                        state.LastRunAt = ParseStoredDateTimeOffset(value);

                        break;
                }
            }
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT mod_id, signature, last_synced_at FROM auto_updater_mod;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var modId = reader.GetString(0);
                var signature = reader.GetString(1);
                var lastSyncedAt = ReadDateTimeOffset(reader, 2);

                var mod = new AutoUpdaterModState
                {
                    Signature = signature,
                    LastSyncedAt = lastSyncedAt,
                    Files = [],
                    CachedFiles = new Dictionary<string, AutoUpdaterCachedFileState>(StringComparer.OrdinalIgnoreCase),
                };

                state.Mods[modId] = mod;
            }
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT mod_id, target_relative_path FROM auto_updater_mod_file ORDER BY target_relative_path COLLATE NOCASE;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var modId = reader.GetString(0);
                var target = reader.GetString(1);
                if (!state.Mods.TryGetValue(modId, out var mod))
                {
                    continue;
                }

                mod.Files.Add(target);
            }
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                "SELECT mod_id, target_relative_path, source_path, is_directory, length, last_write_time_utc, content_hash FROM auto_updater_cached_file;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var modId = reader.GetString(0);
                if (!state.Mods.TryGetValue(modId, out var mod))
                {
                    continue;
                }

                var target = reader.GetString(1);
                var sourcePath = reader.GetString(2);
                var isDirectory = reader.GetInt64(3) != 0;
                var length = reader.GetInt64(4);
                var lastWrite = ReadDateTimeOffset(reader, 5);
                var contentHash = reader.GetString(6);

                mod.CachedFiles[target] = new AutoUpdaterCachedFileState
                {
                    SourcePath = sourcePath,
                    IsDirectory = isDirectory,
                    Length = length,
                    LastWriteTimeUtc = lastWrite,
                    ContentHash = contentHash,
                };
            }
        }

        return state;
    }

    public void Save(string dbPath, AutoUpdaterSyncState state)
    {
        EnsureSchema(dbPath);

        using var connection = CreateOpenConnection(dbPath);
        using var transaction = connection.BeginTransaction();

        UpsertMeta(connection, transaction, "app_id", state.AppId ?? string.Empty);
        UpsertMeta(connection, transaction, "last_run_at", state.LastRunAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));

        ExecuteNonQuery(connection, transaction, "DELETE FROM auto_updater_cached_file;");
        ExecuteNonQuery(connection, transaction, "DELETE FROM auto_updater_mod_file;");
        ExecuteNonQuery(connection, transaction, "DELETE FROM auto_updater_mod;");

        foreach (var pair in state.Mods)
        {
            var modId = pair.Key;
            var mod = pair.Value;

            using (var insertMod = connection.CreateCommand())
            {
                insertMod.Transaction = transaction;
                insertMod.CommandText =
                    "INSERT INTO auto_updater_mod (mod_id, signature, last_synced_at) VALUES (@modId, @signature, @lastSyncedAt);";
                insertMod.Parameters.AddWithValue("@modId", modId);
                insertMod.Parameters.AddWithValue("@signature", mod.Signature ?? string.Empty);
                insertMod.Parameters.AddWithValue("@lastSyncedAt", mod.LastSyncedAt.ToUnixTimeMilliseconds());
                insertMod.ExecuteNonQuery();
            }

            var files = mod.Files ?? [];
            foreach (var file in files)
            {
                using var insertFile = connection.CreateCommand();
                insertFile.Transaction = transaction;
                insertFile.CommandText =
                    "INSERT INTO auto_updater_mod_file (mod_id, target_relative_path) VALUES (@modId, @target);";
                insertFile.Parameters.AddWithValue("@modId", modId);
                insertFile.Parameters.AddWithValue("@target", file ?? string.Empty);
                insertFile.ExecuteNonQuery();
            }

            var cachedFiles = mod.CachedFiles ?? new Dictionary<string, AutoUpdaterCachedFileState>(StringComparer.OrdinalIgnoreCase);
            foreach (var cachedPair in cachedFiles)
            {
                var target = cachedPair.Key;
                var cached = cachedPair.Value;

                using var insertCached = connection.CreateCommand();
                insertCached.Transaction = transaction;
                insertCached.CommandText =
                    "INSERT INTO auto_updater_cached_file (mod_id, target_relative_path, source_path, is_directory, length, last_write_time_utc, content_hash) " +
                    "VALUES (@modId, @target, @sourcePath, @isDirectory, @length, @lastWrite, @contentHash);";
                insertCached.Parameters.AddWithValue("@modId", modId);
                insertCached.Parameters.AddWithValue("@target", target ?? string.Empty);
                insertCached.Parameters.AddWithValue("@sourcePath", cached.SourcePath ?? string.Empty);
                insertCached.Parameters.AddWithValue("@isDirectory", cached.IsDirectory ? 1 : 0);
                insertCached.Parameters.AddWithValue("@length", cached.Length);
                insertCached.Parameters.AddWithValue("@lastWrite", cached.LastWriteTimeUtc.ToUnixTimeMilliseconds());
                insertCached.Parameters.AddWithValue("@contentHash", cached.ContentHash ?? string.Empty);
                insertCached.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    public void EnsureSchema(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var connection = CreateOpenConnection(dbPath);
        ExecuteNonQuery(connection, null, "PRAGMA foreign_keys = ON;");
        ExecuteNonQuery(connection, null,
            "CREATE TABLE IF NOT EXISTS auto_updater_meta (" +
            "key TEXT PRIMARY KEY, " +
            "value TEXT NOT NULL);"
        );
        ExecuteNonQuery(connection, null,
            "CREATE TABLE IF NOT EXISTS auto_updater_mod (" +
            "mod_id TEXT PRIMARY KEY COLLATE NOCASE, " +
            "signature TEXT NOT NULL, " +
            "last_synced_at INTEGER NOT NULL);"
        );
        ExecuteNonQuery(connection, null,
            "CREATE TABLE IF NOT EXISTS auto_updater_mod_file (" +
            "mod_id TEXT NOT NULL COLLATE NOCASE, " +
            "target_relative_path TEXT NOT NULL COLLATE NOCASE, " +
            "PRIMARY KEY (mod_id, target_relative_path), " +
            "FOREIGN KEY(mod_id) REFERENCES auto_updater_mod(mod_id) ON DELETE CASCADE);"
        );
        ExecuteNonQuery(connection, null,
            "CREATE TABLE IF NOT EXISTS auto_updater_cached_file (" +
            "mod_id TEXT NOT NULL COLLATE NOCASE, " +
            "target_relative_path TEXT NOT NULL COLLATE NOCASE, " +
            "source_path TEXT NOT NULL, " +
            "is_directory INTEGER NOT NULL, " +
            "length INTEGER NOT NULL, " +
            "last_write_time_utc INTEGER NOT NULL, " +
            "content_hash TEXT NOT NULL, " +
            "PRIMARY KEY (mod_id, target_relative_path), " +
            "FOREIGN KEY(mod_id) REFERENCES auto_updater_mod(mod_id) ON DELETE CASCADE);"
        );
        ExecuteNonQuery(connection, null,
            "CREATE INDEX IF NOT EXISTS idx_auto_updater_mod_file_mod_id ON auto_updater_mod_file (mod_id);"
        );
        ExecuteNonQuery(connection, null,
            "CREATE INDEX IF NOT EXISTS idx_auto_updater_cached_file_mod_id ON auto_updater_cached_file (mod_id);"
        );
    }

    private static SqliteConnection CreateOpenConnection(string dbPath)
    {
        var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared");
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    private static void UpsertMeta(SqliteConnection connection, SqliteTransaction transaction, string key, string value)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            "INSERT INTO auto_updater_meta (key, value) VALUES (@key, @value) " +
            "ON CONFLICT(key) DO UPDATE SET value=excluded.value;";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static DateTimeOffset ReadDateTimeOffset(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return DateTimeOffset.MinValue;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            long unixMs => DateTimeOffset.FromUnixTimeMilliseconds(unixMs),
            int unixMs => DateTimeOffset.FromUnixTimeMilliseconds(unixMs),
            string text => ParseStoredDateTimeOffset(text),
            _ => ParseStoredDateTimeOffset(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
        };
    }

    private static DateTimeOffset ParseStoredDateTimeOffset(string value)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixMs))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateTimeOffset.MinValue;
            }
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;
    }
}
