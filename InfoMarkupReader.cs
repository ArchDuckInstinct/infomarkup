using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace InfoMarkup
{
    // ========================================================================================================================================
    // InfoMarkupReader
    // ========================================================================================================================================

    public class InfoMarkupReader
    {
        // ====================================================================================================================================
        // Components
        // ====================================================================================================================================

        private InfoMarkupConfiguration config;
        private StreamReader stream;
        private StringBuilder builder;

        // ====================================================================================================================================
        // Reading
        // ====================================================================================================================================

        private char[] buffer;
        private char chr;
        private int pos;
        private int len;
        private bool eof;
        private int depthCurr;
        private int chrNumber;
        private int lineNumber;

        // ====================================================================================================================================
        // Data
        // ====================================================================================================================================

        private class AttributeStack
        {
            public string key;
            public Stack<InfoValue> values;
        }

        private class Scope
        {
            public Scope parent;
            public int depth;
            public string tag;
            public InfoValue parameters;
            public Stack<AttributeStack> attributes;

            public Scope(Scope p)
            {
                parent = p;
            }
        }

        private Dictionary<string, AttributeStack> dataMap;
        private HashSet<string> existingAttributes;
        private Scope scope;

        // ====================================================================================================================================
        // Accessor
        // ====================================================================================================================================

        public InfoType type            {get; private set;}
        public string currentTag        {get {return scope?.tag ?? "";}}
        public string currentKey        {get; private set;}
        public InfoValue currentValue   {get; private set;}
        public int currentDepth         {get {return scope?.depth ?? 0;}}

        // ====================================================================================================================================
        // Constructor / Destructor
        // ====================================================================================================================================

        public InfoMarkupReader(InfoMarkupConfiguration pConfig = null)
        {
            config              = pConfig != null ? pConfig : new InfoMarkupConfiguration();
            stream              = null;
            builder             = new StringBuilder(config.builderInitial, config.builderSize);
            buffer              = new char[config.bufferSize];
            dataMap             = new Dictionary<string, AttributeStack>();
            existingAttributes  = new HashSet<string>();
            scope               = null;
        }

        ~InfoMarkupReader()
        {
            Close();
        }

        // ====================================================================================================================================
        // Error Throwing
        // ====================================================================================================================================

        private void Error(string msg)
        {
            throw new Exception(string.Format("Info Markup Exception on line {0} at {1}: {2}", lineNumber, chrNumber, msg));
        }

        // ====================================================================================================================================
        // Transversal
        // ====================================================================================================================================

        private bool Next()
        {
            if (pos >= len)
            {
                len = stream.Read(buffer, 0, buffer.Length);
                pos = 0;

                if (len < 1)
                {
                    eof = true;
                    chr = '\0';

                    return false;
                }
            }

            chr = buffer[pos];
            ++pos;

            if (chr == '\n')
            {
                chrNumber = 1;
                ++lineNumber;
            }
            else
                ++chrNumber;

            return true;
        }

        private void Expect()
        {
            if (!Next())
                Error("Unexpected end of file");
        }

        private void Skip(char match)
        {
            if (chr == match)
                Next();
        }

        private void SkipUnless(char match)
        {
            if (chr != match)
                Next();
        }

        private void SkipLine()
        {
            while (chr != '\n' && Next());
        }

        private void NextContentLine()
        {
            int tabCount    = 0;
            int spaces      = 0;

            while (Next())
            {
                // Read until a printable character
                if (chr >= '!' && chr <= '~')
                {
                    if (chr == '#')
                        SkipLine();
                    else
                        break;
                }

                // Count tabs or reset tab count after line end
                if (chr == ' ')
                {
                    if (++spaces == 4)
                    {
                        ++tabCount;
                        spaces = 0;
                    }
                }
                else if (chr == '\t')
                    ++tabCount;
                else if (chr == '\n')
                    tabCount = 0;
            }

            depthCurr = !eof ? tabCount : -1;
        }

        private void NextChr()
        {
            // Skip spacing and control characters except for new lines
            while (chr < '!')
                if (chr == '\n' || !Next())
                    break;
        }

        // ====================================================================================================================================
        // Parsing
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

            string numStr   = builder.ToString();
            float value     = float.Parse(numStr);

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
                    value = GetAttributeFloat(currentKey, 0.0f) * (float) (value * 0.01);
                else if (!config.ApplyUnitScale(postfix, ref value))
                    return new InfoString(numStr + postfix);
            }

            return new InfoNumber(value);
        }

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
                InfoValue val   = ParseNumber();
                InfoNumber num  = val as InfoNumber;

                if (num != null)
                    num.value += (attrStack.values.Count > 0) ? attrStack.values.Peek().GetFloat() : 0.0f;

                return val;
            }
            
            // Treat this as a regular number if there is no attribute stack
            return ParseNumber();
        }

        private InfoValue ParseGeneral()
        {
            while (chr >= '!' && chr != ']' && chr != ')')
            {
                builder.Append(chr);
                Expect();
            }

            return new InfoString(builder.ToString());
        }

        private InfoValue ParseGroup(InfoValue first, char delimiter, char start = '\0')
        {
            List<InfoValue> values = new List<InfoValue>();

            if (first != null)
                values.Add(first);

            Skip(start);

            while (chr != delimiter)
            {
                if (values.Count >= config.groupLimit)
                    Error("Group maximum count exceeded");
                if (chr == '\n')
                    Error("Multiline groups not yet implemented.");

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

            // Get optional element parameters
            NextChr();
            scope.parameters = (chr != ']') ? ParseGroup(']') : null;

            type = InfoType.ElementStart;
        }

        private void ParseAttribute()
        {
            builder.Clear();

            currentValue = ParseToken();
            NextChr();

            if (chr != '\n')
            {
                type = InfoType.Attribute;
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
                type = InfoType.Option;
                currentKey = null;
            }
        }

        // ====================================================================================================================================
        // Scope
        // ====================================================================================================================================

        private void ScopePush(int depth)
        {
            scope               = new Scope(scope);
            scope.attributes    = new Stack<AttributeStack>();
            scope.depth         = depth;

            existingAttributes.Clear();
        }

        private void ScopePop()
        {
            // Pop attribute stacks being used by the current scope
            foreach (AttributeStack attrStack in scope.attributes)
                attrStack.values.Pop();

            // Rewind scope back to previous scope
            scope = scope.parent;

            // Reset existing attributes to newly active scope
            existingAttributes.Clear();
            if (scope != null)
                foreach (AttributeStack attrStack in scope.attributes)
                    existingAttributes.Add(attrStack.key);
        }

        // ====================================================================================================================================
        // Attributes
        // ====================================================================================================================================

        private void AddAttribute(string key, InfoValue value)
        {
            AttributeStack attr;

            // Get attribute stack that is being added to
            if (!dataMap.TryGetValue(key, out attr))
            {
                attr = new AttributeStack();
                attr.key = key;
                attr.values = new Stack<InfoValue>();

                dataMap.Add(key, attr);
            }

            // Check existing attributes for this current scope to avoid having duplicate
            if (!existingAttributes.Contains(key))
            {
                attr.values.Push(value);
                scope.attributes.Push(attr);
                existingAttributes.Add(key);
            }
            else
            {
                attr.values.Pop();
                attr.values.Push(value);
            }
        }

        public string GetAttributeString(string key, string fallback = "")
        {
            AttributeStack attr;
            if (dataMap.TryGetValue(key, out attr) && attr.values.Count > 0)
                return attr.values.Peek().GetString(fallback);
            else
                return fallback;
        }

        public int GetAttributeInteger(string key, int fallback = 0)
        {
            AttributeStack attr;
            if (dataMap.TryGetValue(key, out attr) && attr.values.Count > 0)
                return attr.values.Peek().GetInteger(fallback);
            else
                return fallback;
        }

        public float GetAttributeFloat(string key, float fallback = 0.0f)
        {
            AttributeStack attr;
            if (dataMap.TryGetValue(key, out attr) && attr.values.Count > 0)
                return attr.values.Peek().GetFloat(fallback);
            else
                return fallback;
        }

        public IEnumerator<KeyValuePair<string, InfoValue>> Attributes()
        {
            if (scope != null)
                foreach (AttributeStack attrStack in scope.attributes)
                    yield return new KeyValuePair<string, InfoValue>(attrStack.key, attrStack.values.Peek());
        }

        // ====================================================================================================================================
        // Parameters
        // ====================================================================================================================================

        public int GetParameterCount()
        {
            return scope?.parameters?.GetCount() ?? 0;
        }

        public Type GetParameterType(int index)
        {
            if (index >= 0 && index < scope.parameters.GetCount())
                (scope.parameters as InfoGroup).values.GetType();

            return null;
        }

        public string GetParameterString(int index, string fallback = "")
        {
            return scope?.parameters?.GetStringAt(index, fallback) ?? fallback;
        }

        public int GetParameterInteger(int index, int fallback = 0)
        {
            return scope?.parameters?.GetIntegerAt(index, fallback) ?? fallback;
        }

        public float GetParameterFloat(int index, float fallback = 0.0f)
        {
            return scope?.parameters?.GetFloatAt(index, fallback) ?? fallback;
        }

        // ====================================================================================================================================
        // API
        // ====================================================================================================================================

        public bool Open(string path)
        {
            Close();

            // Open file as stream
            if (!File.Exists(path))
                return false;

            stream = new StreamReader(path);

            // Reset data
            chr         = '\0';
            pos         = 0;
            len         = 0;
            eof         = false;
            depthCurr   = 0;
            lineNumber  = 1;
            chrNumber   = 1;
            scope       = null;
            type        = InfoType.None;

            dataMap.Clear();

            return true;
        }

        public void Close()
        {
            if (stream != null)
                stream.Close();
        }

        public bool Read()
        {
            // Get next content line or pop an element scope
            if (type != InfoType.ElementClose)
                NextContentLine();
            else
                ScopePop();

            // Detect if an element is being closed
            if (scope != null && depthCurr <= scope.depth)
            {
                type = InfoType.ElementClose;
                return true;
            }
            else if (eof)
                return false;

            // Reset type and depth
            type = InfoType.None;

            // Reading element, attribute, or option
            if (chr == '[')
                ParseElement();
            else if (chr >= '!')
                ParseAttribute();
            else
                Error("Invalid line start");

            return true;
        }
    }
}
