using System;
using System.Collections.Generic;
using System.Text;
using Procument.Module.Sales.Enums;

namespace Procument.Module.Sales.DTOs
{
    public class QuoteTypeDTO
    {
        public long Id { get; set; }
        public int QuoteType { get; set; }
        public string? TypeAdditional { get; set; }
    }
}
