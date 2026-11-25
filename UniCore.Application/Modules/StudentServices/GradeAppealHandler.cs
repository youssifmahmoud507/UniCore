using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.StudentServices.Specifications;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.StudentServices;
using UniCore.Domain.Modules.Students;
using UniCore.Application.Modules.Grading;

namespace UniCore.Application.Modules.StudentServices
{
    // An appeal against a published grade of the student's own. An approved appeal changes no score: the instructor files the grade change request (see GradeAppeal).
    public sealed class GradeAppealHandler(
        ServiceRequestAccess access, IAggregateRepository<GradeAppeal> requests, IWorkflowService workflows, IGradeReadService grades,
        ICurrentUser user, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
        : ServiceRequestHandlerBase<GradeAppeal>(access, requests, workflows, user, unitOfWork, clock, audit)
    {
        private readonly IGradeReadService _grades = grades;

        protected override string Reference => ServiceRequestKinds.GradeAppealReference;
        protected override string Definition => ServiceRequestKinds.GradeAppealDefinition;
        protected override ServiceRequestSummary Map(GradeAppeal request) => ServiceRequestMapper.ToSummary(request);

        public Task<Result<ServiceRequestSummary>> SubmitAsync(Guid studentId, Guid? gradeId, string? reason, RequestInfo info, CancellationToken cancellationToken = default)
        {
            if (gradeId is not { } id || id == Guid.Empty)
            {
                return Task.FromResult<Result<ServiceRequestSummary>>(ServiceRequestErrors.Invalid);
            }

            return SubmitCoreAsync(studentId, new OpenAppealOfGradeSpecification(studentId, id),
                (userId, now) => GradeAppeal.Create(studentId, userId, id, reason, now),
                null, info, cancellationToken,
                async (_, ct) =>
                {
                    // Only a Published or Locked grade of this student can be appealed (the read service returns nothing else).
                    var published = await _grades.GetPublishedGradesAsync(studentId, null, ct);
                    if (!published.TryGetValue(out var list))
                    {
                        return Result.Fail(published.Errors);
                    }

                    return list.Any(g => g.GradeId == id) ? Result.Ok() : Result.Fail(ServiceRequestErrors.GradeNotFound);
                });
        }
    }
}
