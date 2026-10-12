namespace UniCore.Domain.Common.Entities
{
    // Base class for every entity: a Guid identity and equality by identity.
    // The Id is assigned by the entity's own factory (Guid.NewGuid()), never by EF.
    public abstract class BaseEntity : IEquatable<BaseEntity>
    {
        public Guid Id { get; protected set; }

        public bool Equals(BaseEntity? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            // A Customer and an Order with the same Guid are still different things.
            if (GetType() != other.GetType())
            {
                return false;
            }

            // Two objects that have no identity yet are never "the same entity".
            if (Id == Guid.Empty || other.Id == Guid.Empty)
            {
                return false;
            }

            return Id == other.Id;
        }

        public override bool Equals(object? obj) => obj is BaseEntity other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(GetType(), Id);

        public static bool operator ==(BaseEntity? left, BaseEntity? right) => left is null ? right is null : left.Equals(right);

        public static bool operator !=(BaseEntity? left, BaseEntity? right) => !(left == right);
    }
}
