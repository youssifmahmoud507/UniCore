using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Grading
{
    // N-34: a dropped or withdrawn enrollment must not leave a live grade behind.
    // Removes the grade of the enrollment when it is not final (Draft, Submitted, Reviewed, Approved) and returns its id;
    // returns null when there is nothing to remove. A Published or Locked grade is part of the record and is left alone:
    // it changes only through a grade change request.
    // Nothing is saved here: the caller commits.
    public interface IOrphanGradeService
    {
        Task<Result<Guid?>> CancelForEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
    }
}
