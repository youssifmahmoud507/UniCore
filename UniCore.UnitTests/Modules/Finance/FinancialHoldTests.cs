using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class FinancialHoldTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
        private static readonly Guid Student = Guid.NewGuid();
        private static readonly Guid Staff = Guid.NewGuid();

        private static Money Amount(decimal value)
        {
            Assert.True(Money.Of(value).TryGetValue(out var money));
            return money;
        }

        private static FinancialHold Manual(FinancialHoldArea areas = FinancialHoldArea.Registration)
        {
            var result = FinancialHold.PlaceManual(Student, areas, "  Library fine unpaid  ", Staff, Now);
            Assert.True(result.TryGetValue(out var hold));
            Assert.NotNull(hold);
            return hold;
        }

        private static FinancialHold Automatic()
        {
            var result = FinancialHold.PlaceAutomatic(Student, Amount(750.50m), Now);
            Assert.True(result.TryGetValue(out var hold));
            Assert.NotNull(hold);
            return hold;
        }

        [Fact]
        public void A_manual_hold_is_active_and_keeps_its_areas_and_note()
        {
            var hold = Manual(FinancialHoldArea.Registration | FinancialHoldArea.Services);

            Assert.NotEqual(Guid.Empty, hold.Id);
            Assert.Equal(Student, hold.StudentId);
            Assert.Equal(FinancialHoldReason.Manual, hold.Reason);
            Assert.Equal(FinancialHoldStatus.Active, hold.Status);
            Assert.Equal("Library fine unpaid", hold.Note);
            Assert.Equal(Staff, hold.PlacedByUserId);
            Assert.True(hold.OverdueAmount.IsZero);
            Assert.False(hold.IsAutomatic);
        }

        [Fact]
        public void A_hold_blocks_only_the_areas_it_was_placed_for_and_only_while_active()
        {
            var hold = Manual(FinancialHoldArea.Registration | FinancialHoldArea.Services);

            Assert.True(hold.Blocks(FinancialHoldArea.Registration));
            Assert.True(hold.Blocks(FinancialHoldArea.Services));
            Assert.False(hold.Blocks(FinancialHoldArea.Graduation));

            hold.Release(Staff, "Paid at the desk", Now.AddDays(1));

            Assert.False(hold.Blocks(FinancialHoldArea.Registration));
        }

        [Fact]
        public void A_bad_manual_hold_is_rejected()
        {
            Assert.Equal("FINANCIAL_HOLD_DATA_INVALID", FinancialHold.PlaceManual(Guid.Empty, FinancialHoldArea.All, "x", null, Now).Errors[0].Code);
            Assert.Equal("FINANCIAL_HOLD_DATA_INVALID", FinancialHold.PlaceManual(Student, FinancialHoldArea.None, "x", null, Now).Errors[0].Code);
            Assert.Equal("FINANCIAL_HOLD_DATA_INVALID", FinancialHold.PlaceManual(Student, (FinancialHoldArea)64, "x", null, Now).Errors[0].Code);
            Assert.Equal("FINANCIAL_HOLD_DATA_INVALID", FinancialHold.PlaceManual(Student, FinancialHoldArea.All, "   ", null, Now).Errors[0].Code);
            Assert.Equal("FINANCIAL_HOLD_DATA_INVALID", FinancialHold.PlaceManual(Student, FinancialHoldArea.All, new string('n', FinancialHold.MaxNoteLength + 1), null, Now).Errors[0].Code);
        }

        [Fact]
        public void An_automatic_hold_blocks_everything_and_remembers_the_overdue_amount()
        {
            var hold = Automatic();

            Assert.Equal(FinancialHoldReason.OverdueBalance, hold.Reason);
            Assert.Equal(FinancialHoldArea.All, hold.Areas);
            Assert.Equal(750.50m, hold.OverdueAmount.Amount);
            Assert.Equal("Overdue balance of 750.50", hold.Note);
            Assert.Null(hold.PlacedByUserId);
            Assert.True(hold.IsAutomatic);
            Assert.True(hold.Blocks(FinancialHoldArea.Registration));
            Assert.True(hold.Blocks(FinancialHoldArea.Graduation));
            Assert.True(hold.Blocks(FinancialHoldArea.Services));
        }

        [Fact]
        public void An_automatic_hold_needs_a_student_and_a_positive_amount()
        {
            Assert.Equal("FINANCIAL_HOLD_DATA_INVALID", FinancialHold.PlaceAutomatic(Guid.Empty, Amount(10m), Now).Errors[0].Code);
            Assert.Equal("FINANCIAL_HOLD_DATA_INVALID", FinancialHold.PlaceAutomatic(Student, Money.Zero, Now).Errors[0].Code);
        }

        [Fact]
        public void Placing_a_hold_raises_an_event()
        {
            var hold = Manual(FinancialHoldArea.Graduation);

            var raised = Assert.IsType<FinancialHoldPlaced>(Assert.Single(hold.DomainEvents));
            Assert.Equal(hold.Id, raised.HoldId);
            Assert.Equal(Student, raised.StudentId);
            Assert.Equal(FinancialHoldReason.Manual, raised.Reason);
            Assert.Equal(FinancialHoldArea.Graduation, raised.Areas);
        }

        [Fact]
        public void A_person_must_give_a_note_to_release_a_hold()
        {
            var hold = Manual();

            var result = hold.Release(Staff, "   ", Now.AddDays(1));

            Assert.Equal("FINANCIAL_HOLD_DATA_INVALID", result.Errors[0].Code);
            Assert.Equal(FinancialHoldStatus.Active, hold.Status);
        }

        [Fact]
        public void A_person_releases_with_a_note_and_the_event_says_it_was_not_automatic()
        {
            var hold = Manual();

            Assert.True(hold.Release(Staff, "  Paid at the desk  ", Now.AddDays(1)).IsSuccess);

            Assert.Equal(FinancialHoldStatus.Released, hold.Status);
            Assert.Equal(Now.AddDays(1), hold.ReleasedAt);
            Assert.Equal(Staff, hold.ReleasedByUserId);
            Assert.Equal("Paid at the desk", hold.ReleaseNote);

            var released = hold.DomainEvents.OfType<FinancialHoldReleased>().Single();
            Assert.False(released.ReleasedAutomatically);
        }

        [Fact]
        public void The_system_releases_without_a_person_and_with_its_own_note()
        {
            var hold = Automatic();

            Assert.True(hold.Release(null, null, Now.AddDays(2)).IsSuccess);

            Assert.Null(hold.ReleasedByUserId);
            Assert.Equal("Overdue balance cleared", hold.ReleaseNote);
            Assert.True(hold.DomainEvents.OfType<FinancialHoldReleased>().Single().ReleasedAutomatically);
        }

        [Fact]
        public void A_release_note_that_is_too_long_is_rejected_and_a_released_hold_cannot_be_released_again()
        {
            var hold = Manual();

            Assert.Equal("FINANCIAL_HOLD_DATA_INVALID", hold.Release(Staff, new string('n', FinancialHold.MaxNoteLength + 1), Now).Errors[0].Code);

            hold.Release(Staff, "Done", Now);

            Assert.Equal("FINANCIAL_HOLD_NOT_ACTIVE", hold.Release(Staff, "Again", Now).Errors[0].Code);
        }
    }


}
