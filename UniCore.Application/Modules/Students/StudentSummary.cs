using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Students
{
    public sealed record StudentSummary(Guid Id, string StudentNumber, Guid PersonProfileId, Guid? UserId, StudentAcademicStatus Status, AcademicStanding? Standing, DateOnly AdmissionDate, DateOnly? ExpectedGraduationDate, DateOnly? GraduationDate, Guid? CurrentProgramId, Guid? CurrentStudyPlanVersionId, Guid? CurrentAcademicLevelId);


}
