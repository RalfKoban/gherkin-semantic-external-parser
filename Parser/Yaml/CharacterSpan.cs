using System;

using YamlDotNet.Serialization;

namespace MiKoSolutions.SemanticParsers.Gherkin.Yaml
{
    public struct CharacterSpan : IEquatable<CharacterSpan>
    {
        public CharacterSpan(int start, int end)
        {
            if (start > end && (start != 0 || end != -1))
            {
                // TODO: RKN
                // throw new ArgumentException($"{nameof(start)} should be less than {nameof(end)} but {start} is greater than {end}!", nameof(start));
                end = start;
            }

            Start = start;
            End = end;
        }

        public int Start { get; }

        public int End { get; }

        [YamlIgnore]
        public bool IsEmpty => Start == 0 && End == -1;

        public static bool operator ==(CharacterSpan left, CharacterSpan right) => Equals(left, right);

        public static bool operator !=(CharacterSpan left, CharacterSpan right) => !Equals(left, right);

        public bool Equals(CharacterSpan other) => Start == other.Start && End == other.End;

        public override bool Equals(object obj) => obj is CharacterSpan other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Start * 397) ^ End;
            }
        }

        public override string ToString() => $"Span: {Start}, {End}";
    }
}