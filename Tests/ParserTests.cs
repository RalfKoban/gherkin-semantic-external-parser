using System;
using System.IO;
using System.Text;

using MiKoSolutions.SemanticParsers.Gherkin.Yaml;

using NUnit.Framework;

using File = MiKoSolutions.SemanticParsers.Gherkin.Yaml.File;

namespace MiKoSolutions.SemanticParsers.Gherkin
{
    [TestFixture]
    public class ParserTests
    {
        private string _resourceDirectory;

        [SetUp]
        public void PrepareTest()
        {
            var directory = Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).LocalPath);
            _resourceDirectory = Path.Combine(directory, "Resources");
        }

        [Test]
        public void Parse_EmptyDocument()
        {
            var file = Parser.Parse(Path.Combine(_resourceDirectory, "EmptyDocument.feature"));

            Assert.Multiple(() =>
            {
                var yaml = CreateYaml(file);

                Assert.That(yaml, Is.Not.Null.And.Not.Empty);

                Assert.That(file.LocationSpan, Is.EqualTo(new LocationSpan(new LineInfo(0, -1), new LineInfo(0, -1))));
            });
        }

        private static string CreateYaml(File file)
        {
            var builder = new StringBuilder();
            using (var writer = new StringWriter(builder))
            {
                YamlWriter.Write(writer, file);
            }

            var yaml = builder.ToString();
            return yaml;
        }
    }
}