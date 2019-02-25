namespace Ymf825
{
    internal static class TargetChipExtensions
    {
        public static bool IsSingleChip(this TargetChip chip)
            => (chip & (chip - 1)) == 0;
    }
}
