using System.Collections.Generic;

namespace InfoMarkup
{
    // ========================================================================================================================================
    // InfoMarkupReader (Parsing)
    // ========================================================================================================================================

    public partial class InfoMarkupReader
    {
        // ====================================================================================================================================
        // ParseText
        // ====================================================================================================================================

        private InfoValue ParseText(char delimiter)
        {
            Skip(delimiter);

            while (chr != delimiter)
            {
                if (chr == '\\')
                {
                    // Found a backslash, check if it is escaping a control character or the delimiter

                    Expect();

                    if (chr == '\\')
                        builder.Append('\\');
                    else if (chr == 't')
                        builder.Append('\t');
                    else if (chr == 'n')
                        builder.Append('\n');
                    else if (chr == delimiter)
                        builder.Append(delimiter);
                    else
                    {
                        builder.Append('\\');
                        builder.Append(chr);
                    }
                }
                else
                    builder.Append(chr);

                Expect();
            }

            SkipUnless('\n');

            return new InfoString(builder.ToString());
        }

        // ====================================================================================================================================
        // ParseNumber
        // ====================================================================================================================================

        private InfoValue ParseNumber()
        {
            // Get digits of integer value
            while (chr >= '0' && chr <= '9')
            {
                builder.Append(chr);
                Expect();
            }

            // Get remaining digits of a float value if there is a decimal
            if (chr == '.')
            {
                builder.Append(chr);

                while (chr >= '0' && chr <= '9')
                {
                    builder.Append(chr);
                    Expect();
                }
            }

            string numStr = builder.ToString();
            float value = float.Parse(numStr);

            // Attempt to apply optional unit scale or percentage
            if (chr >= '!' && chr != ']')
            {
                builder.Clear();
                while (chr >= '!' && chr != ']' && chr != ')')
                {
                    builder.Append(chr);
                    Expect();
                }

                string postfix = builder.ToString();

                if (postfix == "%")
                    value = GetAttribute(currentKey).GetFloat(0.0f) * (value * 0.01f);
                else if (!config.ApplyUnitScale(postfix, ref value))
                    return new InfoString(numStr + postfix);
            }

            return new InfoNumber(value);
        }

        // ====================================================================================================================================
        // ParseModifier
        // ====================================================================================================================================

        private InfoValue ParseModifier()
        {
            AttributeStack attrStack;

            Skip('+');

            if (chr == '-' || chr == '.')
            {
                // append the starting minus or decimal
                builder.Append(chr);
                Expect();
            }

            if (!(chr >= '0' && chr <= '9'))
                return ParseGeneral();

            if (dataMap.TryGetValue(currentKey, out attrStack))
            {
                InfoValue val = ParseNumber();
                InfoNumber num = val as InfoNumber;

                if (num != null)
                    num.value += (attrStack.values.Count > 0) ? attrStack.values.Peek().GetFloat() : 0.0f;

                return val;
            }

            // Treat this as a regular number if there is no attribute stack
            return ParseNumber();
        }

        // ====================================================================================================================================
        // ParseGeneral
        // ====================================================================================================================================

        private InfoValue ParseGeneral()
        {
            while (chr >= '!' && chr != ']' && chr != ')')
            {
                builder.Append(chr);
                Expect();
            }

            return new InfoString(builder.ToString().ToLower());
        }

        // ====================================================================================================================================
        // ParseGroup
        // ====================================================================================================================================

        private InfoValue ParseGroup(InfoValue first, char delimiter, char start = '\0')
        {
            List<InfoValue> values = new List<InfoValue>();

            if (first != null)
                values.Add(first);

            Skip(start);
            NextChr();

            while (chr != delimiter)
            {
                if (values.Count >= config.groupLimit)
                    Error("Group maximum count exceeded");
                if (chr == '\n')
                    Error("Multiline groups are not currently supported");

                values.Add(ParseToken());
                NextChr();
            }

            SkipUnless('\n');

            return new InfoGroup(values.ToArray());
        }

        private InfoValue ParseGroup(char delimiter, char start = '\0')
        {
            return ParseGroup(null, delimiter, start);
        }

        // ====================================================================================================================================
        // ParseToken
        // ====================================================================================================================================

        private InfoValue ParseToken()
        {
            builder.Clear();

            if (chr == '\'')
                return ParseText('\'');
            else if (chr == '"')
                return ParseText('"');
            else if (chr == '(')
                return ParseGroup(')', '(');
            else
            {
                if (chr == '+')
                    return ParseModifier();
                else if (chr == '-' || chr == '.')
                {
                    // append the starting minus or decimal
                    builder.Append(chr);
                    Expect();

                    if ((chr >= '0' && chr <= '9'))
                        return ParseNumber();
                    else
                        return ParseGeneral();
                }
                else if ((chr >= '0' && chr <= '9'))
                    return ParseNumber();
                else
                    return ParseGeneral();
            }
        }

        // ====================================================================================================================================
        // ParseElement
        // ====================================================================================================================================

        private void ParseElement()
        {
            Skip('[');
            NextChr();

            // Create the scope for this element
            ScopePush(depthCurr);

            // Get element tag
            builder.Clear();
            while (chr >= '!')
            {
                if (chr == ']')
                    break;

                builder.Append(chr);
                Expect();
            }

            scope.tag = builder.ToString().ToLower();
            currentTag = scope.tag;

            // Get optional element parameters
            NextChr();
            parameters = (chr != ']') ? ParseGroup(']') : new InfoValue();
            scope.parameters = parameters;

            currentNodeType = NodeType.ElementStart;
        }

        // ====================================================================================================================================
        // ParseAttribute
        // ====================================================================================================================================

        private void ParseAttribute()
        {
            builder.Clear();

            currentValue = ParseToken();
            NextChr();

            if (chr != '\n')
            {
                currentNodeType = NodeType.Attribute;
                currentKey = currentValue.GetString().ToLower();

                builder.Clear();
                currentValue = ParseToken();

                NextChr();

                if (chr >= '!')
                    currentValue = ParseGroup(currentValue, '\n');

                AddAttribute(currentKey, currentValue);
            }
            else
            {
                currentNodeType = NodeType.Option;
                currentKey = null;

                AddOption(currentValue);
            }
        }
    }
}