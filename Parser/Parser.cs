using System;
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
                               FooterSpan = new CharacterSpan(0, -1), // there is no footer
                               LocationSpan = new LocationSpan(finder.GetLineInfo(Math.Min(0, lastCharacter)), finder.GetLineInfo(lastCharacter)),
                           };

            try
            {
                var parser = new GherkinParser();
                var document = parser.Parse(filePath);

                // get locations ordered so that we know the position of the feature
                var locations = document.Comments.Select(_ => _.Location).ToList();
                var feature = document.Feature;
                if (feature != null)
                {
                    locations.Add(feature.Location);
                }

                var sortedLocations = locations.OrderBy(_ => _.Line).ThenBy(_ => _.Column).ToList();

                var root = new Container
                               {
                                   Type = nameof(GherkinDocument),
                                   Name = string.Empty,
                                   LocationSpan = file.LocationSpan,
                                   HeaderSpan = new CharacterSpan(0, 0), // there is no header
                                   FooterSpan = new CharacterSpan(Math.Max(0, lastCharacter), lastCharacter), // there is no footer
                               };

                file.Children.Add(root);

                if (feature != null)
                {
                    var positionAfterFeature = sortedLocations.IndexOf(feature.Location) + 1;

                    var location = positionAfterFeature < sortedLocations.Count
                                    ? GetLineInfo(locations[positionAfterFeature])
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
                    file.LocationSpan = new LocationSpan(new LineInfo(0, -1), new LineInfo(0, -1));
                }
                else
                {
                    file.ParsingErrors.Add(new ParsingError
                                               {
                                                   ErrorMessage = ex.Message,
                                                   Location = new LineInfo(0, -1),
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

            var container = new Container
                                {
                                    Type = nameof(Feature),
                                    Name = feature.Name,
                                    LocationSpan = new LocationSpan(start, end),
                                    HeaderSpan = new CharacterSpan(spanStart, spanEnd),
                                    FooterSpan = new CharacterSpan(0, -1), // TODO: FIX
                                };

            return container;
        }

//        private static TerminalNode ParseBlock(LeafBlock block, CharacterPositionFinder finder, TextProvider textProvider)
//        {
//            var name = GetName(block, textProvider);
//            var type = GetType(block);
//            var locationSpan = GetLocationSpan(block, finder);
//            var span = GetCharacterSpan(block);
//
//            return new TerminalNode
//                       {
//                           Type = type,
//                           Name = name,
//                           LocationSpan = locationSpan,
//                           Span = span,
//                       };
// check whether we can use a terminal node instead
// var child = FinalAdjustAfterParsingComplete(container);
// return child;
//        }
//    private static ContainerOrTerminalNode FinalAdjustAfterParsingComplete(Container container)
//        {
//            switch (container.Type)
//            {
//                case nameof(Table):
//                case nameof(TableRow):
//                    return container.ToTerminalNode();
//
//                default:
//                    return container;
//            }
//        }
    }
}