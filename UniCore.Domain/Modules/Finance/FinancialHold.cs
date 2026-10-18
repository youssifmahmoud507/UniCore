using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    public sealed class FinancialHold : AggregateRoot
    {
        public const int MaxNoteLength = 300;

        private const string AutomaticReleaseNote = "Overdue balance cleared";

        private FinancialHold()
        {
        }

        public Guid StudentId { get; private set; }
        public FinancialHoldReason Reason { get; private set; }
        public FinancialHoldArea Areas { get; private set; }
        public FinancialHoldStatus Status { get; private set; }

        // The overdue amount when the hold was placed automatically. Zero for a manual hold.
        public Money OverdueAmount { get; private set; }

        public string Note { get; private set; } = string.Empty;
        public Guid? PlacedByUserId { get; private set; }
        public DateTimeOffset PlacedAt { get; private set; }
        public DateTimeOffset? ReleasedAt { get; private set; }
        public Guid? ReleasedByUserId { get; private set; }
        public string? ReleaseNote { get; private set; }

        public bool IsAutomatic => Reason == FinancialHoldReason.OverdueBalance;

        public bool Blocks(FinancialHoldArea area) => Status == FinancialHoldStatus.Active && (Areas & area) != FinancialHoldArea.None;

        // A person places it, for one or more areas, and says why.
        public static Result<FinancialHold> PlaceManual(Guid studentId, FinancialHoldArea areas, string? note, Guid? placedByUserId, DateTimeOffset now)
        {
            var cleanNote = note?.Trim();

            if (studentId == Guid.Empty
                || areas == FinancialHoldArea.None
                || (areas & ~FinancialHoldArea.All) != FinancialHoldArea.None
                || string.IsNullOrEmpty(cleanNote)
                || cleanNote.Length > MaxNoteLength)
            {
                return FinanceErrors.FinancialHoldDataInvalid;
            }

            var hold = new FinancialHold
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                Reason = FinancialHoldReason.Manual,
                Areas = areas,
                Status = FinancialHoldStatus.Active,
                OverdueAmount = Money.Zero,
                Note = cleanNote,
                PlacedByUserId = placedByUserId,
                PlacedAt = now
            };

            hold.RaiseDomainEvent(new FinancialHoldPlaced(Guid.NewGuid(), now, hold.Id, studentId, hold.Reason, hold.Areas));

            return hold;
        }

        // The system places it from overdue invoices. It blocks everything.
        public static Result<FinancialHold> PlaceAutomatic(Guid studentId, Money overdueAmount, DateTimeOffset now)
        {
            if (studentId == Guid.Empty || !overdueAmount.IsPositive)
            {
                return FinanceErrors.FinancialHoldDataInvalid;
            }

            var hold = new FinancialHold
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                Reason = FinancialHoldReason.OverdueBalance,
                Areas = FinancialHoldArea.All,
                Status = FinancialHoldStatus.Active,
                OverdueAmount = overdueAmount,
                Note = $"Overdue balance of {overdueAmount}",
                PlacedByUserId = null,
                PlacedAt = now
            };

            hold.RaiseDomainEvent(new FinancialHoldPlaced(Guid.NewGuid(), now, hold.Id, studentId, hold.Reason, hold.Areas));

            return hold;
        }

        // A person who releases a hold must say why. The system releases with its own note.
        public Result Release(Guid? releasedByUserId, string? note, DateTimeOffset now)
        {
            if (Status != FinancialHoldStatus.Active)
            {
                return Result.Fail(FinanceErrors.FinancialHoldNotActive);
            }

            var cleanNote = note?.Trim();

            if (string.IsNullOrEmpty(cleanNote))
            {
                if (releasedByUserId is not null)
                {
                    return Result.Fail(FinanceErrors.FinancialHoldDataInvalid);
                }

                cleanNote = AutomaticReleaseNote;
            }
            else if (cleanNote.Length > MaxNoteLength)
            {
                return Result.Fail(FinanceErrors.FinancialHoldDataInvalid);
            }

            Status = FinancialHoldStatus.Released;
            ReleasedAt = now;
            ReleasedByUserId = releasedByUserId;
            ReleaseNote = cleanNote;

            RaiseDomainEvent(new FinancialHoldReleased(Guid.NewGuid(), now, Id, StudentId, releasedByUserId is null));

            return Result.Ok();
        }
    }





}
