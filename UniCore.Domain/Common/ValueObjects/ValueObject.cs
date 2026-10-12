namespace UniCore.Domain.Common.ValueObjects
{
    // A value object has no identity: two instances with the same values are equal.
    // Subclasses list the values that make up their equality in GetEqualityComponents().
    public abstract class ValueObject : IEquatable<ValueObject>
    {
        protected abstract IEnumerable<object?> GetEqualityComponents();

        public bool Equals(ValueObject? other)
        {
            if (other is null)
            {
                return false;
            }

            if (GetType() != other.GetType())
            {
                return false;
            }

            return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
        }

        public override bool Equals(object? obj) => obj is ValueObject other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var component in GetEqualityComponents())
            {
                hash.Add(component);
            }

            return hash.ToHashCode();
        }

        public static bool operator ==(ValueObject? left, ValueObject? right) => left is null ? right is null : left.Equals(right);

        public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
    }
}
