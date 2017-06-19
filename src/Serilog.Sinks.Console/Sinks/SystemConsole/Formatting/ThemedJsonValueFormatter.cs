// Copyright 2017 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Globalization;
using System.IO;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.SystemConsole.Themes;

namespace Serilog.Sinks.SystemConsole.Formatting
{
    class ThemedJsonValueFormatter : ThemedValueFormatter
    {
        readonly ThemedDisplayValueFormatter _displayFormatter;
        readonly IFormatProvider _formatProvider;

        public ThemedJsonValueFormatter(ConsoleTheme theme, IFormatProvider formatProvider)
            : base(theme)
        {
            _displayFormatter = new ThemedDisplayValueFormatter(theme, formatProvider);
            _formatProvider = formatProvider;
        }

        public override ThemedValueFormatter SwitchTheme(ConsoleTheme theme)
        {
            return new ThemedJsonValueFormatter(theme, _formatProvider);
        }

        protected override int VisitScalarValue(ThemedValueFormatterState state, ScalarValue scalar)
        {
            if (scalar == null)
                throw new ArgumentNullException(nameof(scalar));
            return FormatLiteralValue(scalar, state.Output, state.Format);
        }

        protected override int VisitSequenceValue(ThemedValueFormatterState state, SequenceValue sequence)
        {
            if (sequence == null)
                throw new ArgumentNullException(nameof(sequence));

            var count = 0;

            using (ApplyStyle(state.Output, ConsoleThemeStyle.TertiaryText, ref count))
                state.Output.Write('[');

            var delim = "";
            for (var index = 0; index < sequence.Elements.Count; ++index)
            {
                if (delim.Length != 0)
                    using (ApplyStyle(state.Output, ConsoleThemeStyle.TertiaryText, ref count))
                        state.Output.Write(delim);

                delim = ", ";
                Visit(state, sequence.Elements[index]);
            }

            using (ApplyStyle(state.Output, ConsoleThemeStyle.TertiaryText, ref count))
                state.Output.Write(']');

            return count;
        }

        protected override int VisitStructureValue(ThemedValueFormatterState state, StructureValue structure)
        {
            var count = 0;

            using (ApplyStyle(state.Output, ConsoleThemeStyle.TertiaryText, ref count))
                state.Output.Write('{');

            var delim = "";
            for (var index = 0; index < structure.Properties.Count; ++index)
            {
                if (delim.Length != 0)
                    using (ApplyStyle(state.Output, ConsoleThemeStyle.TertiaryText, ref count))
                        state.Output.Write(delim);

                delim = ", ";

                var property = structure.Properties[index];

                using (ApplyStyle(state.Output, ConsoleThemeStyle.Name, ref count))
                    JsonValueFormatter.WriteQuotedJsonString(property.Name, state.Output);

                using (ApplyStyle(state.Output, ConsoleThemeStyle.TertiaryText, ref count))
                    state.Output.Write(": ");

                count += Visit(state, property.Value);
            }
            if (structure.TypeTag != null)
            {
                using (ApplyStyle(state.Output, ConsoleThemeStyle.TertiaryText, ref count))
                    state.Output.Write(delim);

                using (ApplyStyle(state.Output, ConsoleThemeStyle.Name, ref count))
                    JsonValueFormatter.WriteQuotedJsonString("$type", state.Output);

                using (ApplyStyle(state.Output, ConsoleThemeStyle.TertiaryText, ref count))
                    state.Output.Write(": ");

                using (ApplyStyle(state.Output, ConsoleThemeStyle.String, ref count))
                    JsonValueFormatter.WriteQuotedJsonString(structure.TypeTag, state.Output);
            }

            using (ApplyStyle(state.Output, ConsoleThemeStyle.TertiaryText, ref count))
                state.Output.Write('}');

            return count;
        }

        protected override int VisitDictionaryValue(ThemedValueFormatterState state, DictionaryValue dictionary)
        {
            int count = 0;

            using (ApplyStyle(state.Output, ConsoleThemeStyle.TertiaryText, ref count))
                state.Output.Write('{');

            var delim = "";
            foreach (var element in dictionary.Elements)
            {
                if (delim.Length != 0)
                    using (ApplyStyle(state.Output, ConsoleThemeStyle.TertiaryText, ref count))
                        state.Output.Write(delim);

                delim = ", ";

                using (ApplyStyle(state.Output, ConsoleThemeStyle.String, ref count))
                    JsonValueFormatter.WriteQuotedJsonString((element.Key.Value ?? "null").ToString(), state.Output);

                using (ApplyStyle(state.Output, ConsoleThemeStyle.TertiaryText, ref count))
                    state.Output.Write(": ");

                count += Visit(state, element.Value);
            }

            using (ApplyStyle(state.Output, ConsoleThemeStyle.TertiaryText, ref count))
                state.Output.Write('}');

            return count;
        }

        int FormatLiteralValue(ScalarValue scalar, TextWriter output, string format)
        {
            // At the top level, if a format string is specified, non-JSON rendering is used.
            if (format != null)
                return _displayFormatter.FormatLiteralValue(scalar, output, format);

            var value = scalar.Value;
            var count = 0;

            if (value == null)
            {
                using (ApplyStyle(output, ConsoleThemeStyle.Null, ref count))
                    output.Write("null");
                return count;
            }

            if (value is string str)
            {
                using (ApplyStyle(output, ConsoleThemeStyle.String, ref count))
                    JsonValueFormatter.WriteQuotedJsonString(str, output);
                return count;
            }

            if (value is ValueType)
            {
                if (value is int || value is uint || value is long || value is ulong || value is decimal || value is byte || (value is sbyte || value is short) || value is ushort)
                {
                    using (ApplyStyle(output, ConsoleThemeStyle.Number, ref count))
                        output.Write(((IFormattable)value).ToString(null, CultureInfo.InvariantCulture));
                    return count;
                }

                if (value is double d)
                {
                    if (double.IsNaN(d) || double.IsInfinity(d))
                        using (ApplyStyle(output, ConsoleThemeStyle.String, ref count))
                            JsonValueFormatter.WriteQuotedJsonString(d.ToString(CultureInfo.InvariantCulture), output);
                    else
                        using (ApplyStyle(output, ConsoleThemeStyle.Number, ref count))
                            output.Write(d.ToString("R", CultureInfo.InvariantCulture));
                    return count;
                }

                if (value is float f)
                {
                    if (double.IsNaN(f) || double.IsInfinity(f))
                        using (ApplyStyle(output, ConsoleThemeStyle.String, ref count))
                            JsonValueFormatter.WriteQuotedJsonString(f.ToString(CultureInfo.InvariantCulture), output);
                    else
                        using (ApplyStyle(output, ConsoleThemeStyle.Number, ref count))
                            output.Write(f.ToString("R", CultureInfo.InvariantCulture));
                    return count;
                }

                if (value is bool b)
                {
                    using (ApplyStyle(output, ConsoleThemeStyle.Boolean, ref count))
                        output.Write(b ? "true" : "false");

                    return count;
                }

                if (value is char ch)
                {
                    using (ApplyStyle(output, ConsoleThemeStyle.String, ref count))
                        JsonValueFormatter.WriteQuotedJsonString(ch.ToString(), output);
                    return count;
                }

                if (value is DateTime || value is DateTimeOffset)
                {
                    using (ApplyStyle(output, ConsoleThemeStyle.String, ref count))
                    {
                        output.Write('"');
                        output.Write(((IFormattable)value).ToString("O", CultureInfo.InvariantCulture));
                        output.Write('"');
                    }
                    return count;
                }
            }

            using (ApplyStyle(output, ConsoleThemeStyle.String, ref count))
                JsonValueFormatter.WriteQuotedJsonString(value.ToString(), output);

            return count;
        }
    }
}