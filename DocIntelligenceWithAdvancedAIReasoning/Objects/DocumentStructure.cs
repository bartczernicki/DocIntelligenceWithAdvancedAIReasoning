using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocIntelligenceSemanticChunking.Objects
{
    internal class DocumentStructure
    {
        public string DocumentName { get; set; }
        public List<Heading> HeadingsMap { get; set; }
    }
}
