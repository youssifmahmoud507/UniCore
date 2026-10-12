using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Common.Entities
{
    // The entry point of an aggregate. Only aggregate roots get a repository.
    // It collects domain events while its methods run; the infrastructure reads and clears them after saving.
    public abstract class AggregateRoot : BaseEntity
    {
        private readonly List<IDomainEvent> _domainEvents = [];

        // EF Core must ignore this property in every aggregate's configuration: builder.Ignore(x => x.DomainEvents).
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
