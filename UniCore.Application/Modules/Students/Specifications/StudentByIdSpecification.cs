using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Students.Specifications
{
    public sealed class StudentByIdSpecification : Specification<Student>
    {
        public StudentByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(s => s.Id == id);
            AddInclude("Profile");
            AddInclude("ProgramHistory");
            AddInclude("StatusHistory");

            if (tracking)
            {
                UseTracking();
            }
        }
    }


}
