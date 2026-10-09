using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace HighPerfUI.Reference
{
    public static class InventorySaveStore
    {
        private const int SchemaVersion = 1;
        private const long MaximumFileBytes = 64 * 1024 * 1024;
        private static readonly object SaveGate = new object();

        [DataContract]
        private sealed class SaveEnvelope
        {
            [DataMember(IsRequired = true)] public int SchemaVersion;
            [DataMember(IsRequired = true)] public string Payload;
            [DataMember(IsRequired = true)] public string Checksum;
        }

        public static void Save(string path, InventoryModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            string fullPath = NormalizePath(path);
            var snapshot = model.Capture();
            InventoryModel.Validate(snapshot);
            string payload = Encoding.UTF8.GetString(Serialize(snapshot));
            byte[] contents = Serialize(new SaveEnvelope { SchemaVersion = SchemaVersion, Payload = payload, Checksum = Checksum(payload) });
            if (contents.LongLength > MaximumFileBytes) throw new InvalidDataException("Inventory save exceeds the supported size.");

            lock (SaveGate)
            {
                // Do not turn corruption into a new primary or a corrupted backup on the next autosave.
                bool replace = File.Exists(fullPath);
                if (replace) ReadValid(fullPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                string temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(contents, 0, contents.Length);
                        stream.Flush(true);
                    }
                    ReadValid(temporary);
                    if (replace) File.Replace(temporary, fullPath, fullPath + ".bak");
                    else File.Move(temporary, fullPath);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }
        }

        public static InventoryModel Load(string path, out string warning)
        {
            warning = string.Empty;
            string fullPath;
            try { fullPath = NormalizePath(path); }
            catch (ArgumentException exception) { warning = "Invalid save path: " + exception.Message; return null; }

            if (!File.Exists(fullPath) && !File.Exists(fullPath + ".bak")) return null;
            InventoryModel model;
            string primaryError, backupError;
            if (TryRead(fullPath, out model, out primaryError)) return model;
            if (TryRead(fullPath + ".bak", out model, out backupError))
            {
                warning = "Recovered inventory from backup. Primary save was not changed: " + primaryError;
                return model;
            }
            warning = "Inventory could not be loaded. Existing files were preserved. Primary: " + primaryError + "; backup: " + backupError;
            return null;
        }

        public static string RecoverPrimary(string path)
        {
            string fullPath = NormalizePath(path);
            lock (SaveGate)
            {
                var recovered = ReadValid(fullPath + ".bak");
                if (TryRead(fullPath, out _, out _)) return string.Empty;
                string quarantine = File.Exists(fullPath) ? fullPath + ".corrupt-" + Guid.NewGuid().ToString("N") : string.Empty;
                if (quarantine.Length > 0) File.Move(fullPath, quarantine);
                try { Save(fullPath, recovered); }
                catch
                {
                    if (!File.Exists(fullPath) && quarantine.Length > 0) File.Move(quarantine, fullPath);
                    throw;
                }
                return quarantine;
            }
        }

        private static bool TryRead(string path, out InventoryModel model, out string error)
        {
            model = null;
            error = string.Empty;
            try { model = ReadValid(path); return true; }
            catch (InvalidDataException exception) { error = exception.Message; }
            catch (IOException exception) { error = exception.Message; }
            catch (UnauthorizedAccessException exception) { error = exception.Message; }
            return false;
        }

        private static InventoryModel ReadValid(string path)
        {
            byte[] contents;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > MaximumFileBytes) throw new InvalidDataException("Invalid inventory save size.");
                contents = new byte[(int)stream.Length];
                int read = 0;
                while (read < contents.Length)
                {
                    int bytes = stream.Read(contents, read, contents.Length - read);
                    if (bytes == 0) throw new InvalidDataException("Inventory save was truncated.");
                    read += bytes;
                }
            }
            try
            {
                var envelope = Deserialize<SaveEnvelope>(contents);
                if (envelope == null || envelope.SchemaVersion != SchemaVersion || string.IsNullOrEmpty(envelope.Payload)
                    || string.IsNullOrEmpty(envelope.Checksum) || !string.Equals(envelope.Checksum, Checksum(envelope.Payload), StringComparison.Ordinal))
                    throw new InvalidDataException("Unsupported schema or invalid inventory checksum.");
                return InventoryModel.Restore(Deserialize<InventorySnapshot>(Encoding.UTF8.GetBytes(envelope.Payload)));
            }
            catch (SerializationException exception) { throw new InvalidDataException("Malformed inventory save.", exception); }
            catch (XmlException exception) { throw new InvalidDataException("Malformed inventory JSON.", exception); }
            catch (ArgumentException exception) { throw new InvalidDataException("Invalid inventory business state.", exception); }
        }

        private static byte[] Serialize<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return stream.ToArray();
            }
        }

        private static T Deserialize<T>(byte[] bytes)
        {
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, XmlDictionaryReaderQuotas.Max))
            {
                var value = (T)new DataContractJsonSerializer(typeof(T)).ReadObject(reader);
                if (reader.MoveToContent() != XmlNodeType.None) throw new InvalidDataException("Unexpected trailing inventory data.");
                return value;
            }
        }

        private static string Checksum(string payload)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is required.", nameof(path));
            return Path.GetFullPath(path);
        }
    }
}
