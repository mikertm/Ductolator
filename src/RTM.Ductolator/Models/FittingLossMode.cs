namespace RTM.Ductolator.Models
{
    /// <summary>
    /// Policy for applying fitting losses in calculations.
    /// </summary>
    public enum FittingLossMode
    {
        /// <summary>
        /// Use sum of loss coefficients (ΣK) to calculate dynamic pressure loss.
        /// Ignores equivalent length values for pressure drop.
        /// </summary>
        UseSumK,

        /// <summary>
        /// Use equivalent length (Leq) added to straight run length for friction loss.
        /// Ignores ΣK values for pressure drop.
        /// </summary>
        UseEquivalentLength
    }
}
