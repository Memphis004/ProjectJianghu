using System;
using System.Collections.Generic;
using ProjectJianghu.Core.Domain.Config;

namespace ProjectJianghu.Core.Domain.Character
{
    public enum ModifierSourceKind : byte { Base, Skill, Gear, Pill }

    public readonly struct AttributeModifier
    {
        public readonly EAttribute Attribute;
        public readonly int Value;
        public readonly ModifierSourceKind Kind;
        public readonly string SourceId;
        public AttributeModifier(EAttribute attribute, int value, ModifierSourceKind kind, string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) throw new ArgumentException("Modifier source id is required.", nameof(sourceId));
            Attribute = attribute; Value = value; Kind = kind; SourceId = sourceId;
        }
    }

    public readonly struct ModifierTotal
    {
        public readonly int Value, SourceCount;
        public ModifierTotal(int value, int sourceCount) { Value = value; SourceCount = sourceCount; }
    }

    /// <summary>Each contribution retains its source id for deterministic tooltip breakdowns.</summary>
    public sealed class ModifierStack
    {
        private readonly List<AttributeModifier> _entries = new List<AttributeModifier>();
        public int Count => _entries.Count;
        public IReadOnlyList<AttributeModifier> Entries => _entries;
        public AttributeModifier this[int index] => _entries[index];
        public void Clear() => _entries.Clear();
        public void Add(EAttribute attribute, int value, ModifierSourceKind kind, string sourceId) => _entries.Add(new AttributeModifier(attribute, value, kind, sourceId));

        public ModifierTotal Sum(EAttribute attribute)
        {
            int sum = 0, count = 0;
            for (int i = 0; i < _entries.Count; i++) if (_entries[i].Attribute == attribute) { sum = checked(sum + _entries[i].Value); count++; }
            return new ModifierTotal(sum, count);
        }
        public int CopySources(EAttribute attribute, Span<AttributeModifier> destination)
        {
            int written = 0;
            for (int i = 0; i < _entries.Count && written < destination.Length; i++) if (_entries[i].Attribute == attribute) destination[written++] = _entries[i];
            return written;
        }
        internal void RemoveSource(ModifierSourceKind kind, string sourceId)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (_entries[i].Kind == kind && string.Equals(_entries[i].SourceId, sourceId, StringComparison.Ordinal)) _entries.RemoveAt(i);
        }
    }
}
