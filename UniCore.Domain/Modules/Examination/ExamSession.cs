using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.Examination
{
    // An exam period of one semester (midterms or finals). Its dates are checked against the window of the semester
    // by the handler. Exams belong to it and can only be created while it is Open.
    public sealed class ExamSession : AggregateRoot
    {
        private ExamSession()
        {
        }

        public Guid AcademicSemesterId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public ExamType ExamType { get; private set; }
        public DateOnly StartDate { get; private set; }
        public DateOnly EndDate { get; private set; }
        public ExamSessionStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public bool IsOpen => Status == ExamSessionStatus.Open;

        public static Result<ExamSession> Create(Guid semesterId, string? name, ExamType type, DateOnly start, DateOnly end, DateTimeOffset now)
        {
            var clean = name?.Trim();
            if (semesterId == Guid.Empty || string.IsNullOrWhiteSpace(clean) || clean.Length > FieldRules.NameMaxLength || !Enum.IsDefined(type) || start == default || end < start)
            {
                return ExaminationErrors.SessionInvalid;
            }

            return new ExamSession
            {
                Id = Guid.NewGuid(),
                AcademicSemesterId = semesterId,
                Name = clean,
                ExamType = type,
                StartDate = start,
                EndDate = end,
                Status = ExamSessionStatus.Open,
                CreatedAt = now
            };
        }

        public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

        public Result Close(DateTimeOffset now)
        {
            if (!IsOpen)
            {
                return Result.Fail(ExaminationErrors.SessionClosed);
            }

            Status = ExamSessionStatus.Closed;
            UpdatedAt = now;
            return Result.Ok();
        }
    }
}
