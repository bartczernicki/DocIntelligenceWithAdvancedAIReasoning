using DocIntelligenceSemanticChunking.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Net.Mime.MediaTypeNames;

namespace DocIntelligenceSemanticChunking
{
    internal class Prompts
    {
        public static string GetFullPromptForTableOfContentsConsolidation(List<DocumentStructure> documentStructures)
        {
            var document1List = documentStructures[0].HeadingsMap.Where(h => h.Name.ToUpper() != "TABLE OF CONTENTS").Select(a => a.Name).ToList();
            string document1Text = string.Join(",\r\n", document1List);

            var document2List = documentStructures[1].HeadingsMap.Where(h => h.Name.ToUpper() != "TABLE OF CONTENTS").Select(a => a.Name).ToList();
            string document2Text = string.Join(",\r\n", document2List);

            // OpenAI o1 (reasoning) Prompt Guide: https://platform.openai.com/docs/guides/reasoning/advice-on-prompting 
            var fullPromptTemplate = $"""
            <Instructions>
            Below are Section Header Lists from two large Enterprise Risk Management Framework Documents.
            
            Your goal is to extract important risk factor changes between the two documents. 
            Group Section Headers together where similar risk topics are discussed and thus changes can be extracted.
            The names may not match exactly, that is okay. Use your best judgment to group similar Section Headers together.
            Try to be as exhaustive as possible in your groupings. There shouldn't be any Section Headers left out.

            Group as needed, but for inspiration note the Risk Management Framework important factors:
            Governance and Culture, Strategy and Objective-Setting, Performance Review and Revision and Information, Communication, and Reporting

            Output the groupings in a Markdown table with three columns: Group Name, Document1 Sections, Document2 Sections.
            Only return a Markdown Table. Ensure the Markdown Table is properly formatted.
            </Instructions>
            
            <Document 1 - Section Headers>
            {document1Text}
            </Document 1 - Section Headers>
            
            <Document 2 - Section Headers>
            {document2Text}
            </Document 2 - Section Headers>
            """;

            return fullPromptTemplate;
        }

        public static string GetFullPromptForTableOfContentsConsolidationSecondPass(List<DocumentStructure> documentStructures, string markdownTable)
        {
            var document1List = documentStructures[0].HeadingsMap.Where(h => h.Name.ToUpper() != "TABLE OF CONTENTS").Select(a => a.Name).ToList();
            string document1Text = string.Join(",\r\n", document1List);

            var document2List = documentStructures[1].HeadingsMap.Where(h => h.Name.ToUpper() != "TABLE OF CONTENTS").Select(a => a.Name).ToList();
            string document2Text = string.Join(",\r\n", document2List);

            // OpenAI o1 (reasoning) Prompt Guide: https://platform.openai.com/docs/guides/reasoning/advice-on-prompting 
            var fullPromptTemplate = $"""
            <Instructions>
            Below are Section Header Lists from two large Enterprise Risk Management Framework Documents.
            
            You performed a first pass, but may have missed some Section Headers. 
            The first pass markdown table is located in the <Markdown Table First Pass> tags. 
            Focus on the missed Section Headers. The others just need to be verified and consolidated into a final table

            Your goal is to extract important risk factor changes between the two documents. 
            Group Section Headers together where similar risk topics are discussed and thus changes can be extracted.
            The names may not match exactly, that is okay. Use your best judgment to group similar Section Headers together.
            Try to be as exhaustive as possible in your groupings. There shouldn't be any Section Headers left out.
            
            Group as needed, but for inspiration note the Risk Management Framework important factors:
            Governance and Culture, Strategy and Objective-Setting, Performance Review and Revision and Information, Communication, and Reporting
            

            Output the groupings in a Markdown table with three columns: Group Name, Document1 Sections, Document2 Sections.
            Only return a Markdown Table. Ensure the Markdown Table is properly formatted.
            </Instructions>
            
            <Markdown Table First Pass>
            {markdownTable}
            </Markdown Table First Pass>

            <Document 1 - Section Headers>
            {document1Text}
            </Document 1 - Section Headers>
            
            <Document 2 - Section Headers>
            {document2Text}
            </Document 2 - Section Headers>
            """;

            return fullPromptTemplate;
        }

        public static string GetFullPromptForMarkdownToJsonConversion(string markdownFinal)
        {
            // OpenAI o1 (reasoning) Prompt Guide: https://platform.openai.com/docs/guides/reasoning/advice-on-prompting 
            var fullPromptTemplate = $"""
            <Instructions>
            You have a Markdown table that you need to convert to JSON. 
            The Markdown table has three columns: Group Name, Document1 Sections, Document2 Sections.
            Convert the Markdown table to a JSON string and return it.

            ONLY return the JSON string. Do not include any other information.
            Do not include any additional ticks like ```.
            Only return the JSON string with nothing else!

            Remove any prefix dashes "-" from the section names. 
            For example, "- 1.1 Business" should be "1.1 Business".
            </Instructions>
            
            <Markdown Table>
            {markdownFinal}
            </Markdown Table>

            <Example Output Schema>
            {GetSampleRiskGroupingsForOneShotPrompt()}
            </Example Output Schema>
            """;
            return fullPromptTemplate;
        }

        public static string GetFullPromptForSectionGroupRiskAnalysis(string groupName, string document1String, string document2String)
        {
            //var riskFactorSection = Data.GetMicrosoft2023RiskFactors().Keys.ElementAt(secDocumentSectionIndex);

            // OpenAI o1 (reasoning) Prompt Guide: https://platform.openai.com/docs/guides/reasoning/advice-on-prompting 
            var fullPromptTemplate = $"""
            <Context>
            Below is text from from two different Risk Management Framework Documents. 
            The text has been extracted and grouped together based on similar Risk Groupings.
            This particular Risk Grouping is called: {groupName}.  
            Each grouping consists of multiple sections that are similar and related to the same risk topic.
            Sections are separated by this tag: <!-- SectionBreak -->
            </Context>
            
            <Document 1 - {groupName} Risk Grouping>
            {document1String}
            </Document 1 - {groupName} Risk Grouping>
            
            <Document 2 - {groupName} Risk Grouping>
            {document2String}
            </Document 2 - {groupName} Risk Grouping>
            
            <Instructions>
            Important: Be very detailed here, take your time!
            Compare and contrast the differences between these two Risk Groupings on appraising risk exposure. 
            ONLY FOCUS ON THE HIGH IMPACT RISK CHANGES, not minor changes.  
            Your analysis should primarily focus on the following factor changes:
            Governance and Culture, Strategy and Objective-Setting, Performance Review and Revision and Information, Communication, and Reporting
            
            Please present your detailed findings in a clear and structured markdown table format grouping the key changes together.
            
            Output the analysis in a Markdown table with FOUR columns: Risk Grouping (repeat this group name: {groupName} for each row), Document 2 Risk Grouping Summary, 
            Document 2 Risk Grouping Summary, Key Risk Framework Changes (indicate specificity which document the risk change is from or to).
            Only return a Markdown Table. Ensure the Markdown Table is properly formatted. Do not include any other information.
            </Instructions>
            """;

            return fullPromptTemplate;
        }

        public static string GetFullPromptToConsolidateImportantRisks(List<string> markdownTables)
        {
            // Concatenate all the markdown tables into a single string
            var markdownTablesString = string.Join("\n\n", markdownTables);

            var promptTemplate = $"""
            <Context>
            You HAVE extracted risk factor changes from multiple sections of two large Risk Management Framework documents. 
            The structure is as follows: 
            1. Risk Grouping: Title of the group of similar risk factors.
            2. Document 1 Risk Grouping Summary: A brief summary of the risk factors from one document.
            2. Document 2 Risk Grouping Summary: A brief summary of the risk factors from one document.
            4. Key Risk Framework Changes: Description of the key changes between the two documents.  
            </Context>

            <Instructions>
            Below is a list of Markdown Tables you HAVE extracted from the two documents. 
            Please perform a comprehensive risk analysis by consolidating only the significant and 
            impactful risk factor changes into a single Markdown table. 
            Key risk factors to look out for: Identification, Assessment, Mitigation, Monitoring, and Reporting. 
            For each selected significant Risk Factor: 
            1. Assess the Potential Impact: Evaluate how the change may affect the company's risk profile, operations, financial performance, or strategic direction. 
            2. Highlight Key Insights: Provide a brief analysis explaining why this change is important and how it differs from the other document. 
            3. Prioritize Relevance: Focus on changes that introduce new risks, significantly alter existing risks, or remove previously critical risks. 

            DO NOT include the entire tables, only the relevant and selected medium and significant risk factor changes.
            Generate a markdown table without enclosing it in a code block. 

            Below the markdown table create an Executive Summary that provides an overview of the key risk factor changes and their potential impact.
            </Instructions>

            <Markdown Tables>
            {markdownTablesString}
            </Markdown Tables>
            """;

            return promptTemplate;
        }

        /// <summary>
        /// Used to generate a sample JSON string for the One Shot API  
        /// Can be used for GPT-4o with JSON strict mode to generate 100% JSON adherance
        /// </summary>
        /// <returns></returns>
        public static string GetSampleRiskGroupingsForOneShotPrompt()
        {
            var groupings = new List<Objects.RiskFactorGroupings>();
            groupings.Add(new RiskFactorGroupings
            {
                GroupName = "Risky Business",
                Document1Sections = new List<string> { "1 BUSINESS", "1.1 BUSINESS SNAPSHOT", "1.2 BUSINESS & RISK" },
                Document2Sections = new List<string> { "1.1 BUSINESS INTRO", "1.2 RISKY SESSIONS" }
            });
            groupings.Add(new RiskFactorGroupings
            {
                GroupName = "Risk Goals",
                Document1Sections = new List<string> { "4.1 ACHIEVE RISK PERFORMANCE", "4.2 PRESERVE CAPITAL", "4.3 MAINTAIN RISK STUFF" },
                Document2Sections = new List<string> { "2.1 TARGETED PERFORMANCE GOALS", "2.2 CAPITAL ADEQUACY", "2.3 MAINTAINING RISK", "2.4 ALL STUFF OF RISK" }
            });

            // return JSON string
            var jsonString = JsonSerializer.Serialize(groupings);
            return jsonString;
        }
    }
}
