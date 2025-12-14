using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RTM.Ductolator.Models
{
    public class CalcReport
    {
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<CalcReportSection> Sections { get; } = new();
        public List<string> Warnings { get; } = new();

        public CalcReport(string title)
        {
            Title = title;
        }

        public CalcReportSection AddSection(string name)
        {
            var section = new CalcReportSection(name);
            Sections.Add(section);
            return section;
        }

        public void AddLine(string sectionName, string key, string value)
        {
            var section = Sections.FirstOrDefault(s => s.Name == sectionName);
            if (section == null)
            {
                section = AddSection(sectionName);
            }
            section.Lines.Add(new CalcReportLine(key, value));
        }

        public string ToCsvBlock()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Report");
            sb.AppendLine($"Title,{Title}");
            sb.AppendLine($"Created,{CreatedAt:G}");
            if (Warnings.Any())
            {
                sb.AppendLine("Warnings," + string.Join("; ", Warnings));
            }

            foreach (var section in Sections)
            {
                sb.AppendLine();
                sb.AppendLine($"[{section.Name}]");
                foreach (var line in section.Lines)
                {
                    sb.AppendLine($"{CsvEscape(line.Key)},{CsvEscape(line.Value)}");
                }
            }
            return sb.ToString();
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }
    }

    public class CalcReportSection
    {
        public string Name { get; }
        public List<CalcReportLine> Lines { get; } = new();

        public CalcReportSection(string name)
        {
            Name = name;
        }
    }

    public class CalcReportLine
    {
        public string Key { get; }
        public string Value { get; }

        public CalcReportLine(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
}
