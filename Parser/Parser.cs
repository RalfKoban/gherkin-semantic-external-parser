using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Gherkin.Ast;

using MiKoSolutions.SemanticParsers.Gherkin.Yaml;

using Container = MiKoSolutions.SemanticParsers.Gherkin.Yaml.Container;
using File = MiKoSolutions.SemanticParsers.Gherkin.Yaml.File;
using GherkinParser = Gherkin.Parser;
using SystemFile = System.IO.File;

namespace MiKoSolutions.SemanticParsers.Gherkin
{
    public static class Parser
    {
        // we have issues with UTF-8 encodings in files that should have an encoding='iso-8859-1'
        public static File Parse(string filePath) => Parse(filePath, "iso-8859-1");

        public static File Parse(string filePath, string encoding)
        {
            var encodingToUse = Encoding.GetEncoding(encoding);

            File file;
            using (var finder = CharacterPositionFinder.CreateFrom(filePath, encodingToUse))
            {
                file = ParseCore(filePath, finder, encodingToUse);

                Resorter.Resort(file);

                GapFiller.Fill(file, finder);
            }

            return file;
        }

        public static File ParseCore(string filePath, CharacterPositionFinder finder, Encoding encoding)
        {
            var text = SystemFile.ReadAllText(filePath, encoding);
            var lastCharacter = text.Length - 1;

            var file = new File
                           {
                               Name = filePath,
                               FooterSpan = CharacterSpan.None, // there is no footer
                               LocationSpan = lastCharacter >= 0
                                               ? new LocationSpan(finder.GetLineInfo(0), finder.GetLineInfo(lastCharacter))
                                               : new LocationSpan(LineInfo.None, LineInfo.None),
                           };

            try
            {
                var parser = new GherkinParser();
                var document = parser.Parse(filePath);

                var root = new Container
                               {
                                   Type = nameof(GherkinDocument),
                                   Name = string.Empty,
                                   LocationSpan = file.LocationSpan,
                                   HeaderSpan = new CharacterSpan(0, 0), // there is no header
                                   FooterSpan = new CharacterSpan(Math.Max(0, lastCharacter), lastCharacter), // there is no footer
                               };

                file.Children.Add(root);

                var feature = document.Feature;
                if (feature != null)
                {
                    // get locations ordered so that we know the position of the feature
                    var locations = document.Comments.Select(_ => _.Location).ToList();
                    locations.Add(feature.Location);

                    var sortedLocations = locations.OrderBy(_ => _.Line).ThenBy(_ => _.Column).ToList();

                    var positionAfterFeature = sortedLocations.IndexOf(feature.Location) + 1;

                    var location = positionAfterFeature < sortedLocations.Count - 1
                                    ? GetLineInfo(locations[positionAfterFeature + 1])
                                    : file.LocationSpan.End;

                    var parsedChild = ParseFeature(feature, finder, location);
                    root.Children.Add(parsedChild);
                }
            }
            catch (Exception ex)
            {
                // try to adjust location span to include full file content
                // but ignore empty files as parsing errors
                var lines = SystemFile.ReadLines(filePath).Count();
                if (lines == 0)
                {
                    file.LocationSpan = new LocationSpan(LineInfo.None, LineInfo.None);
                }
                else
                {
                    file.ParsingErrors.Add(new ParsingError
                                               {
                                                   ErrorMessage = ex.Message,
                                                   Location = LineInfo.None,
                                               });

                    file.LocationSpan = new LocationSpan(new LineInfo(1, 0), new LineInfo(lines + 1, 0));
                }
            }

            return file;
        }

        private static LineInfo GetLineInfo(Location location) => new LineInfo(location.Line, location.Column);

        private static ContainerOrTerminalNode ParseFeature(Feature feature, CharacterPositionFinder finder, LineInfo locationAfterFeature)
        {
            var start = GetLineInfo(feature.Location);
            var end = locationAfterFeature;

            var spanStart = finder.GetCharacterPosition(start);
            var spanEnd = spanStart + finder.GetLineLength(start);

            var locationInside = feature.Tags.Select(_ => _.Location).Concat(feature.Children.Select(_ => _.Location)).OrderBy(_ => _.Line).ThenBy(_ => _.Column).FirstOrDefault();
            if (locationInside != null)
            {
                var lineInfo = GetLineInfo(locationInside);
                var position = finder.GetCharacterPosition(lineInfo) - 1;
                spanEnd = position;
            }

            var container = new Container
                                {
                                    Type = nameof(Feature),
                                    Name = feature.Name,
                                    LocationSpan = new LocationSpan(start, end),
                                    HeaderSpan = new CharacterSpan(spanStart, spanEnd),
                                    FooterSpan = CharacterSpan.None, // TODO: FIX
                                };

            container.Children.AddRange(ParseScenarioDefinitions(feature, finder, locationAfterFeature));
            container.Children.AddRange(ParseTags(feature, finder, locationAfterFeature));

            return container;
        }

        private static IEnumerable<ContainerOrTerminalNode> ParseScenarioDefinitions(Feature feature, CharacterPositionFinder finder, LineInfo locationAfterFeature)
        {
            var locations = feature.Children.Select(_ => _.Location).ToList();
            var sortedLocations = locations.OrderBy(_ => _.Line).ThenBy(_ => _.Column).ToList();

            var children = new List<ContainerOrTerminalNode>();
            foreach (Scenario scenarioDefinition in feature.Children)
            {
                var positionAfterDefinition = sortedLocations.IndexOf(scenarioDefinition.Location) + 1;

                var location = positionAfterDefinition < sortedLocations.Count - 1
                    ? GetLineInfo(locations[positionAfterDefinition + 1])
                    : locationAfterFeature;

                var parsedChild = ParseScenario(scenarioDefinition, finder, location);
                children.Add(parsedChild);
            }

            return children;
        }

        private static ContainerOrTerminalNode ParseScenario(Scenario scenario, CharacterPositionFinder finder, LineInfo locationAfterDefinition)
        {
            var start = GetLineInfo(scenario.Location);
            var end = locationAfterDefinition;

            var spanStart = finder.GetCharacterPosition(start);
            var spanEnd = spanStart + finder.GetLineLength(start);

            var locationInside = scenario.Steps.Select(_ => _.Location).OrderBy(_ => _.Line).ThenBy(_ => _.Column).FirstOrDefault();
            if (locationInside != null)
            {
                var lineInfo = GetLineInfo(locationInside);
                var position = finder.GetCharacterPosition(lineInfo) - 1;
                spanEnd = position;
            }

            var container = new Container
                                {
                                    Type = nameof(Scenario),
                                    Name = scenario.Name,
                                    LocationSpan = new LocationSpan(start, end),
                                    HeaderSpan = new CharacterSpan(spanStart, spanEnd),
                                    FooterSpan = CharacterSpan.None, // TODO: FIX
                                };

            container.Children.AddRange(ParseSteps(scenario, finder, locationAfterDefinition));

            return container;
        }

        private static IEnumerable<ContainerOrTerminalNode> ParseSteps(Scenario scenario, CharacterPositionFinder finder, LineInfo locationAfterDefinition)
        {
            var locations = scenario.Steps.Select(_ => _.Location).ToList();
            var sortedLocations = locations.OrderBy(_ => _.Line).ThenBy(_ => _.Column).ToList();

            var children = new List<ContainerOrTerminalNode>();
            foreach (var step in scenario.Steps)
            {
                var positionAfterStep = sortedLocations.IndexOf(step.Location) + 1;

                var location = positionAfterStep < sortedLocations.Count - 1
                    ? GetLineInfo(locations[positionAfterStep + 1])
                    : locationAfterDefinition;

                var parsedChild = ParseStep(step, finder, location);
                children.Add(parsedChild);
            }

            return children;
        }

        private static ContainerOrTerminalNode ParseStep(Step step, CharacterPositionFinder finder, LineInfo locationAfterDefinition)
        {
            var start = GetLineInfo(step.Location);
            var end = locationAfterDefinition;

            var spanStart = finder.GetCharacterPosition(start);
            var spanEnd = spanStart + finder.GetLineLength(start);

            var node = new TerminalNode
                                {
                                    Type = nameof(Step),
                                    Name = step.Keyword + step.Text,
                                    LocationSpan = new LocationSpan(start, end),
                                    Span = new CharacterSpan(spanStart, spanEnd), // TODO: FIX
                                };

            return node;
        }

        private static IEnumerable<ContainerOrTerminalNode> ParseTags(Feature feature, CharacterPositionFinder finder, LineInfo locationAfterFeature)
        {
            var locations = feature.Tags.Select(_ => _.Location).ToList();
            var sortedLocations = locations.OrderBy(_ => _.Line).ThenBy(_ => _.Column).ToList();

            var children = new List<ContainerOrTerminalNode>();
            foreach (var tag in feature.Tags)
            {
                var positionAfterDefinition = sortedLocations.IndexOf(tag.Location) + 1;

                var location = positionAfterDefinition < sortedLocations.Count - 1
                    ? GetLineInfo(locations[positionAfterDefinition + 1])
                    : locationAfterFeature;

                var parsedChild = ParseTag(tag, finder, location);
                children.Add(parsedChild);
            }

            return children;
        }

        private static ContainerOrTerminalNode ParseTag(Tag tag, CharacterPositionFinder finder, LineInfo locationAfterDefinition)
        {
            var start = GetLineInfo(tag.Location);
            var end = locationAfterDefinition;

            var spanStart = finder.GetCharacterPosition(start);
            var spanEnd = spanStart + finder.GetLineLength(start);

            var container = new Container
                                {
                                    Type = nameof(Tag),
                                    Name = tag.Name,
                                    LocationSpan = new LocationSpan(start, end),
                                    HeaderSpan = new CharacterSpan(spanStart, spanEnd),
                                    FooterSpan = CharacterSpan.None, // TODO: FIX
                                };

            return container;
        }
    }
}