using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class FeeDefinitionByIdSpecification : Specification<FeeDefinition>
    {
        public FeeDefinitionByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(d => d.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }



}
