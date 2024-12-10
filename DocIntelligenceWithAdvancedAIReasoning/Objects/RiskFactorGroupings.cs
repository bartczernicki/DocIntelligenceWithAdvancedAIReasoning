using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocIntelligenceSemanticChunking.Objects
{
    internal class RiskFactorGroupings
    {
        public string GroupName { get; set; }
        public List<string> Document1Sections { get; set; }
        public List<string> Document2Sections { get; set; }
    }
}
