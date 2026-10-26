using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Domain.Modules.Documents
{
    // The file formats the platform accepts. A document type allows some of them.
    [Flags]
    public enum DocumentFormat
    {
        None = 0,
        Pdf = 1,
        Png = 2,
        Jpeg = 4,
        Docx = 8,
        Images = Png | Jpeg,
        All = Pdf | Png | Jpeg | Docx
    }



}
