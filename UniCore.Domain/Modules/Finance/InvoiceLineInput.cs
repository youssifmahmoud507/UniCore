using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Domain.Modules.Finance
{
    public sealed record InvoiceLineInput(string Description, int Quantity, Money UnitPrice, Guid? StudentChargeId);

}
