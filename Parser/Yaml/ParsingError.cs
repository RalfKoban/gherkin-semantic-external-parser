﻿using YamlDotNet.Serialization;

namespace MiKoSolutions.SemanticParsers.Gherkin.Yaml
{
    public sealed class ParsingError
    {
        [YamlMember(Alias = "location")]
        public LineInfo Location { get; set; }

        [YamlMember(Alias = "message")]
        public string ErrorMessage { get; set; }
    }
}