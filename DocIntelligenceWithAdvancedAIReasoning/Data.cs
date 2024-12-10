using DocIntelligenceSemanticChunking.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocIntelligenceSemanticChunking
{
    public static class Data
    {
        internal static string GetSectionGroupingsWithTextParagraphs(List<string> documentSections, List<Heading> headingsMaps)
        {
            // Document1 Groupings and Text
            var groupTexts1 = (from section in documentSections
                               join heading in headingsMaps
                                    on section.ToUpper() equals heading.Name.ToUpper()
                               where heading.Text.Length > 25
                               select heading.Name.ToUpper() + "\r\n" + heading.Text)
                         .ToList();
            var groupTexts1String = string.Join("<!-- SectionBreak --> \r\n\r\n", groupTexts1);

            return groupTexts1String;
        }
    }
}
