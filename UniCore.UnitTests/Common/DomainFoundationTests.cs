using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Events;
using UniCore.Domain.Common.ValueObjects;

namespace UniCore.UnitTests.Common
{
    public class DomainFoundationTests
    {
        // --- small test doubles -------------------------------------------------------------------------

        private sealed class TestEvent : IDomainEvent
        {
            public Guid EventId { get; } = Guid.NewGuid();
            public DateTimeOffset OccurredAt { get; } = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        }

        private sealed class TestAggregate : AggregateRoot
        {
            public TestAggregate(Guid id)
            {
                Id = id;
            }

            public void DoSomething() => RaiseDomainEvent(new TestEvent());
        }

        private sealed class OtherAggregate : AggregateRoot
        {
            public OtherAggregate(Guid id)
            {
                Id = id;
            }
        }

        private sealed class Money : ValueObject
        {
            public Money(decimal amount, string currency)
            {
                Amount = amount;
                Currency = currency;
            }

            public decimal Amount { get; }
            public string Currency { get; }

            protected override IEnumerable<object?> GetEqualityComponents()
            {
                yield return Amount;
                yield return Currency;
            }
        }

        // --- BaseEntity ---------------------------------------------------------------------------------

        [Fact]
        public void Entities_with_the_same_type_and_id_are_equal()
        {
            var id = Guid.NewGuid();

            Assert.Equal(new TestAggregate(id), new TestAggregate(id));
            Assert.True(new TestAggregate(id) == new TestAggregate(id));
        }

        [Fact]
        public void Entities_with_different_ids_are_not_equal()
        {
            Assert.NotEqual(new TestAggregate(Guid.NewGuid()), new TestAggregate(Guid.NewGuid()));
            Assert.True(new TestAggregate(Guid.NewGuid()) != new TestAggregate(Guid.NewGuid()));
        }

        [Fact]
        public void Entities_of_different_types_with_the_same_id_are_not_equal()
        {
            var id = Guid.NewGuid();

            Assert.False(new TestAggregate(id).Equals(new OtherAggregate(id)));
        }

        [Fact]
        public void Entities_without_an_id_are_never_equal_to_each_other()
        {
            Assert.False(new TestAggregate(Guid.Empty).Equals(new TestAggregate(Guid.Empty)));
        }

        // --- AggregateRoot ------------------------------------------------------------------------------

        [Fact]
        public void Raised_domain_events_are_collected_and_can_be_cleared()
        {
            var aggregate = new TestAggregate(Guid.NewGuid());
            Assert.Empty(aggregate.DomainEvents);

            aggregate.DoSomething();
            aggregate.DoSomething();
            Assert.Equal(2, aggregate.DomainEvents.Count);

            aggregate.ClearDomainEvents();
            Assert.Empty(aggregate.DomainEvents);
        }

        // --- ValueObject --------------------------------------------------------------------------------

        [Fact]
        public void Value_objects_with_the_same_values_are_equal_and_share_a_hash_code()
        {
            var a = new Money(10m, "EGP");
            var b = new Money(10m, "EGP");

            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Value_objects_with_different_values_are_not_equal()
        {
            Assert.True(new Money(10m, "EGP") != new Money(10m, "USD"));
        }

        // --- Address ------------------------------------------------------------------------------------

        [Fact]
        public void Address_Create_trims_and_keeps_optional_parts_null_when_blank()
        {
            var address = ResultAssert.Succeeded(Address.Create("  1 University St ", " Cairo ", "  ", null, " Egypt "));

            Assert.Equal("1 University St", address.Street);
            Assert.Equal("Cairo", address.City);
            Assert.Null(address.State);
            Assert.Null(address.PostalCode);
            Assert.Equal("Egypt", address.Country);
        }

        [Theory]
        [InlineData(null, "Cairo", "Egypt", "ADDRESS_STREET_INVALID")]
        [InlineData("  ", "Cairo", "Egypt", "ADDRESS_STREET_INVALID")]
        [InlineData("1 St", null, "Egypt", "ADDRESS_CITY_INVALID")]
        [InlineData("1 St", "Cairo", null, "ADDRESS_COUNTRY_INVALID")]
        public void Address_Create_rejects_missing_required_parts(string? street, string? city, string? country, string expectedCode)
        {
            var result = Address.Create(street, city, null, null, country);

            Assert.True(result.IsFailure);
            Assert.Equal(expectedCode, result.Error?.Code);
        }

        [Fact]
        public void Address_Create_rejects_too_long_parts()
        {
            Assert.Equal("ADDRESS_STREET_INVALID", Address.Create(new string('a', 201), "Cairo", null, null, "Egypt").Error?.Code);
            Assert.Equal("ADDRESS_STATE_INVALID", Address.Create("1 St", "Cairo", new string('a', 101), null, "Egypt").Error?.Code);
            Assert.Equal("ADDRESS_POSTAL_CODE_INVALID", Address.Create("1 St", "Cairo", null, new string('1', 21), "Egypt").Error?.Code);
        }

        [Fact]
        public void Addresses_with_the_same_values_are_equal()
        {
            var a = ResultAssert.Succeeded(Address.Create("1 St", "Cairo", null, null, "Egypt"));
            var b = ResultAssert.Succeeded(Address.Create(" 1 St ", "Cairo", null, null, "Egypt"));

            Assert.Equal(a, b);
        }
    }
}
