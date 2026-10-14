using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class FeeStructureTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

        private static Money Amount(decimal value)
        {
            Assert.True(Money.Of(value).TryGetValue(out var money));
            return money;
        }

        private static FeeStructure NewStructure(Guid? program = null, Guid? semester = null)
        {
            var result = FeeStructure.Create(program, semester, "Fall 2026 CS", Now);
            Assert.True(result.TryGetValue(out var structure));
            Assert.NotNull(structure);
            return structure;
        }

        private static FeeStructureItem AddItem(FeeStructure structure, decimal amount = 100m, Guid? definition = null)
        {
            var result = structure.AddItem(definition ?? Guid.NewGuid(), Amount(amount), Now);
            Assert.True(result.TryGetValue(out var item));
            Assert.NotNull(item);
            return item;
        }

        [Fact]
        public void A_new_structure_is_a_draft_with_no_items()
        {
            var program = Guid.NewGuid();
            var structure = NewStructure(program, null);

            Assert.NotEqual(Guid.Empty, structure.Id);
            Assert.Equal(program, structure.AcademicProgramId);
            Assert.Null(structure.AcademicSemesterId);
            Assert.Equal(FeeStructureStatus.Draft, structure.Status);
            Assert.Empty(structure.Items);
            Assert.True(structure.Total.IsZero);
        }

        [Fact]
        public void Bad_data_is_rejected()
        {
            Assert.Equal("FEE_STRUCTURE_DATA_INVALID", FeeStructure.Create(Guid.Empty, null, "Name", Now).Errors[0].Code);
            Assert.Equal("FEE_STRUCTURE_DATA_INVALID", FeeStructure.Create(null, Guid.Empty, "Name", Now).Errors[0].Code);
            Assert.Equal("FEE_STRUCTURE_DATA_INVALID", FeeStructure.Create(null, null, "  ", Now).Errors[0].Code);
            Assert.Equal("FEE_STRUCTURE_DATA_INVALID", FeeStructure.Create(null, null, new string('N', FeeStructure.MaxNameLength + 1), Now).Errors[0].Code);
        }

        [Fact]
        public void Items_are_added_and_the_total_is_their_sum()
        {
            var structure = NewStructure();

            AddItem(structure, 5000m);
            AddItem(structure, 250.50m);

            Assert.Equal(2, structure.Items.Count);
            Assert.Equal(5250.50m, structure.Total.Amount);
        }

        [Fact]
        public void The_same_fee_cannot_be_added_twice()
        {
            var structure = NewStructure();
            var definition = Guid.NewGuid();
            AddItem(structure, 100m, definition);

            var duplicate = structure.AddItem(definition, Amount(200m), Now);

            Assert.True(duplicate.IsFailure);
            Assert.Equal("FEE_ITEM_DUPLICATE", duplicate.Errors[0].Code);
            Assert.Single(structure.Items);
        }

        [Fact]
        public void An_item_needs_a_fee_and_a_positive_amount()
        {
            var structure = NewStructure();

            Assert.Equal("FEE_STRUCTURE_DATA_INVALID", structure.AddItem(Guid.Empty, Amount(10m), Now).Errors[0].Code);
            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", structure.AddItem(Guid.NewGuid(), Money.Zero, Now).Errors[0].Code);
            Assert.Empty(structure.Items);
        }

        [Fact]
        public void A_structure_cannot_have_more_than_the_maximum_number_of_items()
        {
            var structure = NewStructure();

            for (var i = 0; i < FeeStructure.MaxItems; i++)
            {
                AddItem(structure);
            }

            var tooMany = structure.AddItem(Guid.NewGuid(), Amount(1m), Now);

            Assert.Equal("FEE_STRUCTURE_TOO_MANY_ITEMS", tooMany.Errors[0].Code);
        }

        [Fact]
        public void An_item_amount_can_change_and_a_removed_item_is_gone()
        {
            var structure = NewStructure();
            var keep = AddItem(structure, 100m);
            var drop = AddItem(structure, 50m);

            Assert.True(structure.ChangeItemAmount(keep.Id, Amount(120m), Now).IsSuccess);
            Assert.True(structure.RemoveItem(drop.Id, Now).IsSuccess);

            Assert.Single(structure.Items);
            Assert.Equal(120m, structure.Total.Amount);
        }

        [Fact]
        public void Changing_or_removing_an_unknown_item_is_not_found_and_amounts_must_be_positive()
        {
            var structure = NewStructure();
            var item = AddItem(structure);

            Assert.Equal("FEE_ITEM_NOT_FOUND", structure.ChangeItemAmount(Guid.NewGuid(), Amount(5m), Now).Errors[0].Code);
            Assert.Equal("FEE_ITEM_NOT_FOUND", structure.RemoveItem(Guid.NewGuid(), Now).Errors[0].Code);
            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", structure.ChangeItemAmount(item.Id, Money.Zero, Now).Errors[0].Code);
        }

        [Fact]
        public void An_empty_structure_cannot_be_activated()
        {
            var structure = NewStructure();

            var result = structure.Activate(Now);

            Assert.Equal("FEE_STRUCTURE_EMPTY", result.Errors[0].Code);
            Assert.Equal(FeeStructureStatus.Draft, structure.Status);
        }

        [Fact]
        public void Activation_freezes_the_structure()
        {
            var structure = NewStructure();
            var item = AddItem(structure);

            var activated = structure.Activate(Now.AddDays(1));

            Assert.True(activated.IsSuccess);
            Assert.Equal(FeeStructureStatus.Active, structure.Status);
            Assert.Equal(Now.AddDays(1), structure.ActivatedAt);

            Assert.Equal("FEE_STRUCTURE_NOT_DRAFT", structure.AddItem(Guid.NewGuid(), Amount(1m), Now).Errors[0].Code);
            Assert.Equal("FEE_STRUCTURE_NOT_DRAFT", structure.ChangeItemAmount(item.Id, Amount(1m), Now).Errors[0].Code);
            Assert.Equal("FEE_STRUCTURE_NOT_DRAFT", structure.RemoveItem(item.Id, Now).Errors[0].Code);
            Assert.Equal("FEE_STRUCTURE_NOT_DRAFT", structure.Activate(Now).Errors[0].Code);
        }

        [Fact]
        public void Only_an_active_structure_can_be_archived_and_archived_is_final()
        {
            var structure = NewStructure();
            AddItem(structure);

            Assert.Equal("FEE_STRUCTURE_NOT_ACTIVE", structure.Archive(Now).Errors[0].Code);

            structure.Activate(Now);
            Assert.True(structure.Archive(Now.AddDays(2)).IsSuccess);

            Assert.Equal(FeeStructureStatus.Archived, structure.Status);
            Assert.Equal(Now.AddDays(2), structure.ArchivedAt);
            Assert.Equal("FEE_STRUCTURE_NOT_ACTIVE", structure.Archive(Now).Errors[0].Code);
            Assert.Equal("FEE_STRUCTURE_NOT_DRAFT", structure.Activate(Now).Errors[0].Code);
        }
    }

}
