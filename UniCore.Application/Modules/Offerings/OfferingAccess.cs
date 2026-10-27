using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Offerings;

namespace UniCore.Application.Modules.Offerings
{
    // The checks every change handler of this module starts with, in one place. Order: the permission alone
    // (Courses.Manage), then load, then the scope (the department of the offered course), so a caller without the
    // permission cannot learn whether an id exists. The course's department comes from the Courses contract.
    public sealed class OfferingAccess(
        IPermissionAuthorizer authorizer, IAggregateRepository<CourseOffering> offerings, IAggregateRepository<CourseSection> sections,
        IRepository<AcademicSemester> semesters, ICourseCatalogReadService catalog)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly IAggregateRepository<CourseOffering> _offerings = offerings;
        private readonly IAggregateRepository<CourseSection> _sections = sections;
        private readonly IRepository<AcademicSemester> _semesters = semesters;
        private readonly ICourseCatalogReadService _catalog = catalog;

        public sealed record OfferingContext(CourseOffering Offering, CourseVersionInfo Version);

        public sealed record SectionContext(CourseSection Section, CourseOffering Offering, CourseVersionInfo Version);

        public async Task<Result> RequireManagePermissionAsync(CancellationToken cancellationToken)
            => await _authorizer.AuthorizeAsync(Permissions.Courses.Manage, null, cancellationToken);

        // For a new offering: the version must exist, and the caller must manage courses of its department.
        public async Task<Result<CourseVersionInfo>> AuthorizeForVersionAsync(Guid courseVersionId, CancellationToken cancellationToken)
        {
            var permission = await RequireManagePermissionAsync(cancellationToken);
            if (permission.IsFailure)
            {
                return Result<CourseVersionInfo>.Fail(permission.Errors);
            }

            return await ScopeForVersionAsync(courseVersionId, cancellationToken);
        }

        public async Task<Result<OfferingContext>> LoadOfferingAsync(Guid offeringId, CancellationToken cancellationToken)
        {
            var permission = await RequireManagePermissionAsync(cancellationToken);
            if (permission.IsFailure)
            {
                return Result<OfferingContext>.Fail(permission.Errors);
            }

            var offering = await _offerings.GetByIdAsync(offeringId, cancellationToken);
            if (offering is null)
            {
                return OfferingsErrors.OfferingNotFound;
            }

            var version = await ScopeForVersionAsync(offering.CourseVersionId, cancellationToken);

            return version.TryGetValue(out var info) ? new OfferingContext(offering, info) : Result<OfferingContext>.Fail(version.Errors);
        }

        public async Task<Result<SectionContext>> LoadSectionAsync(Guid sectionId, CancellationToken cancellationToken)
        {
            var permission = await RequireManagePermissionAsync(cancellationToken);
            if (permission.IsFailure)
            {
                return Result<SectionContext>.Fail(permission.Errors);
            }

            var section = await _sections.GetByIdAsync(sectionId, cancellationToken);
            if (section is null)
            {
                return OfferingsErrors.SectionNotFound;
            }

            var offering = await _offerings.GetByIdAsync(section.CourseOfferingId, cancellationToken);
            if (offering is null)
            {
                return OfferingsErrors.OfferingNotFound;
            }

            var version = await ScopeForVersionAsync(offering.CourseVersionId, cancellationToken);

            return version.TryGetValue(out var info) ? new SectionContext(section, offering, info) : Result<SectionContext>.Fail(version.Errors);
        }

        // The semester must exist and not be closed.
        public async Task<Result<AcademicSemester>> OpenSemesterAsync(Guid semesterId, CancellationToken cancellationToken)
        {
            var semester = semesterId == Guid.Empty ? null : await _semesters.GetByIdAsync(semesterId, cancellationToken);
            if (semester is null)
            {
                return OfferingsErrors.SemesterNotFound;
            }

            return semester.IsClosed ? OfferingsErrors.SemesterClosed : semester;
        }

        private async Task<Result<CourseVersionInfo>> ScopeForVersionAsync(Guid courseVersionId, CancellationToken cancellationToken)
        {
            var found = await _catalog.GetVersionAsync(courseVersionId, cancellationToken);
            if (!found.TryGetValue(out var info))
            {
                return OfferingsErrors.CourseVersionNotFound;
            }

            var scope = await _authorizer.AuthorizeAsync(Permissions.Courses.Manage, ResourceContext.InScope(ScopeType.Department, info.DepartmentId), cancellationToken);

            return scope.IsFailure ? Result<CourseVersionInfo>.Fail(scope.Errors) : info;
        }
    }
}
