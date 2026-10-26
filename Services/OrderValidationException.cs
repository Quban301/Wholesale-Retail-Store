namespace WholesaleRetailStore.Services
{
    /// <summary>
    /// Raised when a quote or order request cannot be fulfilled for a reason the caller
    /// can act on (unknown product, bad quantity, not enough stock). Controllers turn this
    /// into a 400, or a 404 when <see cref="IsNotFound"/> is set.
    /// </summary>
    public sealed class OrderValidationException : Exception
    {
        /// <summary>True when the failure is a missing customer or product.</summary>
        public bool IsNotFound { get; }

        public OrderValidationException(string message, bool isNotFound = false) : base(message)
        {
            IsNotFound = isNotFound;
        }
    }

    /// <summary>A requested product and quantity.</summary>
    public readonly record struct OrderLine(int ProductId, int Quantity);
}
