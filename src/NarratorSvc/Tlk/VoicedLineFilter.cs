namespace NarratorSvc.Tlk
{
    internal sealed class VoicedLineFilter
    {
        private readonly TlkIndex _index;

        public VoicedLineFilter(TlkIndex index)
        {
            _index = index;
        }

        public bool IsVoiced(int strRef)
        {
            if (strRef < 0)
            {
                return false;
            }

            TlkEntry entry;
            if (!_index.TryGetEntry(strRef, out entry))
            {
                return false;
            }

            return (entry.Flags & 0x02) != 0 && !string.IsNullOrWhiteSpace(entry.SoundResRef);
        }
    }
}
