using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Horizon.Game;
using UnityEngine;

namespace Horizon
{
    [Serializable]
    public sealed class ArchiveEnvelope
    {
        public string format = "HORIZON-LIFE";
        public int version = 2;
        public string savedAt;
        public string checksum;
        public string payload;
    }

    // Rename writes keep either the old primary or a verified previous backup
    // available after interruption. Invalid files are preserved for recovery.
    public sealed class ArchiveStore
    {
        public const int MaximumBytes = 16 * 1024 * 1024;
        public readonly string Path;
        public string BackupPath { get { return Path + ".backup"; } }
        public string Notice { get; private set; }
        public bool WriteBlocked { get; private set; }
        private readonly string legacyKey;
        private string lastPayload;

        public ArchiveStore(string path, string legacyKey = null)
        { Path = path; this.legacyKey = legacyKey; }

        public ArchiveData Load()
        {
            Notice = null;
            ArchiveData data;
            string error;
            if (TryFile(Path, out data, out error)) { lastPayload = JsonUtility.ToJson(data); return data; }
            if (error == "newer") WriteBlocked = true;
            bool broken = File.Exists(Path);
            if (TryFile(BackupPath, out data, out error))
            { Notice = WriteBlocked ? "存档来自更新版本，已保留。当前以旧备份预览，暂不写入。" :
                "已从上一份安全存档恢复。最近一次未写完的变化可能需要重做。"; return data; }
            if (error == "newer") WriteBlocked = true;
            if (!string.IsNullOrEmpty(legacyKey) && PlayerPrefs.HasKey(legacyKey))
            {
                string json = PlayerPrefs.GetString(legacyKey);
                if (TryDecode(json, out data, out error))
                { if (broken) Notice = "已恢复本机的人生记录。损坏文件仍保留。"; return data; }
                if (error == "newer") WriteBlocked = true;
                // Keep the unreadable legacy value before any new mirror is saved.
                PlayerPrefs.SetString(legacyKey + ".unreadable", json);
            }
            if (broken || File.Exists(BackupPath))
                Notice = WriteBlocked ? "这份存档来自更新版本，已保留。请使用对应版本打开。" :
                    "现有存档未能读取，原文件已保留。可以在设置中导入人生备份。";
            return new ArchiveData();
        }

        private static string Digest(string payload)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", "").ToLowerInvariant();
        }

        public static string Encode(ArchiveData data)
        {
            string payload = JsonUtility.ToJson(data);
            return JsonUtility.ToJson(new ArchiveEnvelope { savedAt = DateTime.UtcNow.ToString("o"),
                payload = payload, checksum = Digest(payload) }, true);
        }

        public static bool TryDecode(string json, out ArchiveData data, out string error)
        {
            data = null; error = "invalid";
            if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumBytes) return false;
            try
            {
                string payload = json;
                // The old unwrapped archive remains importable for migration.
                if (!json.Contains("\"runs\""))
                {
                    ArchiveEnvelope envelope = JsonUtility.FromJson<ArchiveEnvelope>(json);
                    if (envelope == null || envelope.format != "HORIZON-LIFE" || envelope.payload == null) return false;
                    if (envelope.version > 2) { error = "newer"; return false; }
                    if (envelope.version < 1 || envelope.checksum != Digest(envelope.payload)) return false;
                    payload = envelope.payload;
                }
                if (!payload.TrimStart().StartsWith("{") || !payload.Contains("\"runs\"")) return false;
                ArchiveData candidate = JsonUtility.FromJson<ArchiveData>(payload);
                if (candidate == null) return false;
                if (candidate.active != null && (candidate.active.rulesVersion > GameSession.RulesVersion ||
                    candidate.active.catalogVersion > CardCatalog.CurrentVersion)) { error = "newer"; return false; }
                candidate.Repair();
                if (candidate.active != null) GameSession.Restore(candidate.active);
                foreach (RunRecord run in candidate.runs)
                {
                    if (run == null || run.number < 1 || run.boss == null || run.actions == null || run.actions.Count != GameSession.RunLength(run)) return false;
                    if (run.catalogVersion > CardCatalog.CurrentVersion) { error = "newer"; return false; }
                }
                data = candidate; error = null; return true;
            }
            catch (Exception) { return false; }
        }

        private static bool TryFile(string path, out ArchiveData data, out string error)
        {
            data = null; error = null;
            try
            {
                if (!File.Exists(path)) return false;
                if (new FileInfo(path).Length > MaximumBytes) { error = "invalid"; return false; }
                return TryDecode(File.ReadAllText(path, Encoding.UTF8), out data, out error);
            }
            catch (Exception) { error = "unreadable"; return false; }
        }

        public bool Save(ArchiveData data)
        {
            if (WriteBlocked) return false;
            string temporary = Path + ".tmp";
            try
            {
                string payload = JsonUtility.ToJson(data);
                // Background/resume, settings and a reopened receipt frequently
                // save the same state. Keep the last distinct rollback state.
                if (payload == lastPayload && File.Exists(Path)) return true;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                byte[] bytes = Encoding.UTF8.GetBytes(Encode(data));
                if (bytes.Length > MaximumBytes) throw new IOException("Archive exceeds storage limit.");
                using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
                ArchiveData verified; string error;
                if (!TryFile(temporary, out verified, out error)) throw new IOException("Archive verification failed.");
                if (File.Exists(Path))
                {
                    if (TryFile(Path, out verified, out error))
                    {
                        if (File.Exists(BackupPath)) File.Delete(BackupPath);
                        File.Move(Path, BackupPath);
                    }
                    else File.Move(Path, Path + ".unreadable." + DateTime.UtcNow.Ticks);
                }
                File.Move(temporary, Path);
                lastPayload = payload;
                if (!string.IsNullOrEmpty(legacyKey))
                { PlayerPrefs.SetString(legacyKey, JsonUtility.ToJson(data)); PlayerPrefs.Save(); }
                return true;
            }
            catch (Exception exception)
            { Notice = "这次存档没有写完。已有安全存档仍保留，请检查设备空间。";
                Debug.LogWarning("HORIZON save could not complete: " + exception.GetType().Name); return false; }
        }

        public bool Import(string json, out ArchiveData imported, out string error)
        {
            if (!TryDecode(json, out imported, out error)) return false;
            if (WriteBlocked) { imported = null; error = "newer"; return false; }
            // The current primary becomes the rollback copy only after validation.
            try
            {
                ArchiveData current; string currentError;
                if (TryFile(Path, out current, out currentError)) File.Copy(Path, Path + ".before-import", true);
            }
            catch (Exception) { imported = null; error = "storage"; return false; }
            if (!Save(imported)) { imported = null; error = "storage"; return false; }
            return true;
        }
    }
}
