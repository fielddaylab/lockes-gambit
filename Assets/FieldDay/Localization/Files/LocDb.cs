#if UNITY_EDITOR
#define LOCDB_IMPLEMENT_DEDUPLICATE
#endif // UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay.Data;
using Unity.IL2CPP.CompilerServices;

namespace FieldDay.Localization {
    /// <summary>
    /// Localization database.
    /// </summary>
    public sealed class LocDb : IByteSerializable
    {
        private readonly Dictionary<uint, string> m_Entries;

        public LocDb(int capacity) {
            m_Entries = new Dictionary<uint, string>(capacity);
        }

        public LocDb(Dictionary<uint, string> premadeEntries) {
            Assert.NotNull(premadeEntries);
            m_Entries = premadeEntries;
        }

        /// <summary>
        /// Attempts to retrieve the entry for the given localization id.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Il2CppSetOption(Option.NullChecks, false)]
        public bool TryFind(LocId id, out string text) {
            return m_Entries.TryGetValue(GetKey(id), out text);
        }

        /// <summary>
        /// Retrieves the entry for the given localization id.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Il2CppSetOption(Option.NullChecks, false)]
        public string Find(LocId id, string defaultText) {
            string text;
            if (!m_Entries.TryGetValue(GetKey(id), out text)) {
                text = defaultText;
            }
            return text;
        }

        /// <summary>
        /// Returns the number of keys present.
        /// </summary>
        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [Il2CppSetOption(Option.NullChecks, false)]
            get { return m_Entries.Count; }
        }

        /// <summary>
        /// Clears the database.
        /// </summary>
        public void Clear() {
            m_Entries.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Il2CppSetOption(Option.NullChecks, false)]
        static private uint GetKey(LocId id) {
            return id.HashValue;
        }

        /// <summary>
        /// Adds a new localization entry.
        /// </summary>
        public void Add(LocId id, string text) {
            Assert.False(m_Entries.ContainsKey(id.HashValue), "Localization id {0} was already registered!", id);
            m_Entries.Add(id.HashValue, text);
        }

        #region IByteSerializable

        public void ReadFrom(ref ByteReader reader) {
            uint entryCount = reader.Read<uint>();
            m_Entries.Clear();
            m_Entries.EnsureCapacity((int) entryCount);

            uint directEntryCount = reader.Read<uint>();
            while(directEntryCount-- > 0) {
                uint hash = reader.Read<uint>();
                string val = reader.ReadUTF8();
                m_Entries.Add(hash, val);
            }

            uint aliasCount = reader.Read<uint>();
            while (aliasCount-- > 0) {
                uint hash = reader.Read<uint>();
                uint redirect = reader.Read<uint>();
                m_Entries.Add(hash, m_Entries[redirect]);
            }
        }

        public void WriteTo(ref ByteWriter writer) {
            writer.Write((uint) m_Entries.Count);

#if LOCDB_IMPLEMENT_DEDUPLICATE
            WriteWithDeduplication(ref writer);
#else
            writer.Write((uint)m_Entries.Count);
            foreach(var entry in m_Entries) {
                writer.Write(entry.Key);
                writer.WriteUTF8(entry.Value);
            }

            writer.Write(0u); // no aliases
#endif // LOCDB_IMPLEMENT_DEDUPLICATE
        }

        #endregion // IByteSerializable

#if LOCDB_IMPLEMENT_DEDUPLICATE

        private void WriteWithDeduplication(ref ByteWriter writer) {
            Dictionary<uint, uint> aliases = new Dictionary<uint, uint>(16);
            Dictionary<ulong, List<DeduplicateEntry>> dedupeMap = new Dictionary<ulong, List<DeduplicateEntry>>(m_Entries.Count);
            DeduplicateResults dedupeResults = default;

            uint directEntriesCount = 0;
            uint aliasEntriesCount = 0;

            uint directEntriesCountMarker = writer.GetMarker();
            writer.Write(0u);
            foreach (var entry in m_Entries) {
                ulong valueHash = StringHash64.Fast(entry.Value).HashValue;
                if (!dedupeMap.TryGetValue(valueHash, out List<DeduplicateEntry> matchingHashes)) {
                    matchingHashes = new List<DeduplicateEntry>(2);
                    dedupeMap.Add(valueHash, matchingHashes);
                }

                bool wasUnique = true;
                foreach (var potentialMatch in matchingHashes) {
                    if (potentialMatch.SourceValue.Equals(entry.Value, StringComparison.Ordinal)) {
                        wasUnique = false;
                        aliases.Add(entry.Key, potentialMatch.SourceKey);
                        break;
                    }
                }

                if (!wasUnique) {
                    aliasEntriesCount++;
                    dedupeResults.TotalEntriesCollapsed++;
                    dedupeResults.TotalCharactersCollapsed += (uint)entry.Value.Length;
                } else {
                    directEntriesCount++;
                    writer.Write(entry.Key);
                    writer.WriteUTF8(entry.Value);
                }
            }
            writer.Overwrite(directEntriesCount, directEntriesCountMarker);

            Assert.True(directEntriesCount + aliasEntriesCount == m_Entries.Count, "Direct and indirect entry counts do not line up with total entry count");

            writer.Write(aliasEntriesCount);
            foreach(var aliasEntry in aliases) {
                writer.Write(aliasEntry.Key);
                writer.Write(aliasEntry.Value);
            }

            if (aliasEntriesCount > 0) {
                Log.Msg("[LocDb] Deduplicated {0} entries with a combined total of {1} characters", dedupeResults.TotalEntriesCollapsed, dedupeResults.TotalCharactersCollapsed);
            }
        }

        private struct DeduplicateEntry {
            public uint SourceKey;
            public string SourceValue;
        }

        private struct DeduplicateResults {
            public uint TotalEntriesCollapsed;
            public uint TotalCharactersCollapsed;
        }

#endif // LOCDB_IMPLEMENT_DEDUPLICATE
    }
}