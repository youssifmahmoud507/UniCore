using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.StudentServices;

namespace UniCore.Application.Modules.StudentServices.Specifications
{
    public sealed class ServiceRequestByIdSpecification<T> : Specification<T> where T : ServiceRequest
    {
        public ServiceRequestByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(r => r.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class ServiceRequestsOfStudentSpecification<T> : Specification<T> where T : ServiceRequest
    {
        public ServiceRequestsOfStudentSpecification(Guid studentId)
        {
            SetCriteria(r => r.StudentId == studentId);
            ApplyOrderByDescending(r => r.SubmittedAt);
        }
    }

    public sealed class ServiceRequestsListSpecification<T> : Specification<T> where T : ServiceRequest
    {
        public ServiceRequestsListSpecification(PaginationQuery query, ServiceRequestStatus? status)
        {
            SetCriteria(r => status == null || r.Status == status);
            ApplyOrderByDescending(r => r.SubmittedAt);
            ApplyPaging(query);
        }
    }

    public sealed class ServiceRequestsCountSpecification<T> : Specification<T> where T : ServiceRequest
    {
        public ServiceRequestsCountSpecification(ServiceRequestStatus? status)
            => SetCriteria(r => status == null || r.Status == status);
    }

    // A Pending or Returned request of the same student: the student cannot ask twice while one is open.
    public sealed class OpenCertificateOfStudentSpecification : Specification<CertificateRequest>
    {
        public OpenCertificateOfStudentSpecification(Guid studentId, CertificateType type)
            => SetCriteria(r => r.StudentId == studentId && r.CertificateType == type && (r.Status == ServiceRequestStatus.Pending || r.Status == ServiceRequestStatus.Returned));
    }

    public sealed class OpenTranscriptOfStudentSpecification : Specification<TranscriptRequest>
    {
        public OpenTranscriptOfStudentSpecification(Guid studentId)
            => SetCriteria(r => r.StudentId == studentId && (r.Status == ServiceRequestStatus.Pending || r.Status == ServiceRequestStatus.Returned));
    }

    public sealed class OpenAppealOfGradeSpecification : Specification<GradeAppeal>
    {
        public OpenAppealOfGradeSpecification(Guid studentId, Guid gradeId)
            => SetCriteria(r => r.StudentId == studentId && r.GradeId == gradeId && (r.Status == ServiceRequestStatus.Pending || r.Status == ServiceRequestStatus.Returned));
    }
}
