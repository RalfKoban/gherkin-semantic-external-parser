using System.Diagnostics;

using YamlDotNet.Serialization;

namespace MiKoSolutions.SemanticParsers.Gherkin.Yaml
{
    [DebuggerDisplay("Type={Type}, Name={Name}")]
    public abstract class ContainerOrTerminalNode
    {
        [YamlIgnore]
        private string _type;

        [YamlIgnore]
        private string _name;

        [YamlMember(Alias = "type", Order = 1)]
        public string Type
        {
            get => _type;
            set => _type = value is null ? null : string.Intern(WorkaroundForRegexIssue(value)); // performance optimization for large files
        }

        [YamlMember(Alias = "name", Order = 2)]
        public string Name
        {
            get => _name;
            set => _name = value is null ? null : string.Intern(WorkaroundForRegexIssue(value)); // performance optimization for large files
        }

        [YamlMember(Alias = "locationSpan", Order = 3)]
        public LocationSpan LocationSpan { get; set; }

        public abstract CharacterSpan GetTotalSpan();

        public abstract TerminalNode ToTerminalNode();

        // workaround for Semantic/GMaster RegEx parsing exception that is not aware of special backslash character sequences
        private static string WorkaroundForRegexIssue(string value) => value
                                                                        .Replace("\\", " \\ ")
                                                                        .Replace("++", "+ +")
                                                                        .Replace("**", "* *")
                                                                        .Replace("[]", string.Empty);
    }
}