using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocIntelligenceSemanticChunking.Objects
{
    internal class Heading
    {
        public required string MarkdownName { get; set; }
        public required string Name { get; set; }
        public int NameStart { get; set; }
        public int NameEnd { get; set; }
        public int TextStart { get; set; }
        public int TextEnd { get; set; }
        public int Index { get; set; }
        public string Text { get; set; }
    }
}
