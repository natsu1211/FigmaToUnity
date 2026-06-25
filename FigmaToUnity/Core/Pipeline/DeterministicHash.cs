using System;

namespace FigmaToUnity.Core
{
    /// <summary>
    /// FNV-1a based hash accumulator. Produces stable hash values across processes and
    /// runtimes, suitable for persisted manifests and cross-run comparison.
    /// Do not use System.HashCode or string.GetHashCode() when the hash needs to survive
    /// process boundaries - those are randomized per process on .NET Core 3+.
    /// </summary>
    public struct DeterministicHash
    {
        private const int OffsetBasis = unchecked((int)2166136261);
        private const int Prime = 16777619;

        private int _value;
        private bool _initialized;

        public void Add(int value)
        {
            if (!_initialized)
            {
                _value = OffsetBasis;
                _initialized = true;
            }

            _value = unchecked((_value ^ value) * Prime);
        }

        public void Add(bool value) => Add(value ? 1 : -1);

        public void Add(bool? value) => Add(value.HasValue ? (value.Value ? 1 : -1) : 0);

        public void Add(float value) => Add(BitConverter.SingleToInt32Bits(value));

        // Mix in the has-value flag separately so `null` and a concrete
        // value whose bit pattern happens to be 0 (i.e. +0f) do not hash
        // to the same result.
        public void Add(float? value)
        {
            Add(value.HasValue);
            Add(value.GetValueOrDefault());
        }

        public void Add(string? value)
        {
            if (value == null)
            {
                Add(0);
                return;
            }

            int h = OffsetBasis;
            foreach (char c in value)
            {
                h = unchecked((h ^ c) * Prime);
            }

            Add(h);
        }

        public int ToHashCode() => _initialized ? _value : 0;
    }
}
