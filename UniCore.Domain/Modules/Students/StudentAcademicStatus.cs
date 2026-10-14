using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Domain.Modules.Students
{
    public enum StudentAcademicStatus
    {
        Active = 0,
        OnLeave = 1,
        Suspended = 2,
        Withdrawn = 3,
        Dismissed = 4,
        Graduated = 5
    }
}
