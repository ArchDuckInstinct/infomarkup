using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace InfoMarkup
{
    // ========================================================================================================================================
    // NodeType
    // ========================================================================================================================================

    public enum NodeType
    {
        None,
        ElementStart,   // After reading an element, the element's tag and parameters are available
        ElementClose,   // After leaving an element's scope, the element's tag, parameters, and attributes are available
        Attribute,      // After reading an attribute, its key and values are available
        Option          // After reading an option, its value is available
    }

    // ========================================================================================================================================
    // InfoMarkupReader
    // ========================================================================================================================================

    public partial class InfoMarkupReader
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
        private int allowed;

        // ====================================================================================================================================
        // Data
        // ====================================================================================================================================

        internal class AttributeStack
        {
            public string key;
            public Stack<InfoValue> values;
        }

        internal class Scope
        {
            public Scope parent;
            public int depth;
            public string tag;
            public InfoValue parameters;
            public Stack<AttributeStack> attributes;
            public int subElementCount;
            public int optionStart;

            public Scope(Scope p)
            {
                parent = p;
            }
        }

        private Dictionary<string, AttributeStack> dataMap;
        private HashSet<string> existingAttributes;
        private InfoValue parameters;
        internal Scope scope;
        private Stack<int> scopeLock;
        private InfoMarkupValidator activeValidator;
        private List<InfoValue> options;
        private int optionStart;

        // ====================================================================================================================================
        // Accessor
        // ====================================================================================================================================

        public NodeType currentNodeType {get; private set;}
        public string currentTag        {get; private set;}
        public string currentKey        {get; private set;}
        public InfoValue currentValue   {get; private set;}
        public int currentDepth         {get; private set;}
        public int subElementCount      {get; private set;}

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
            options             = new List<InfoValue>();
            scope               = null;
            scopeLock           = new Stack<int>();

            currentNodeType = NodeType.None;
            currentTag      = "";
            currentKey      = "";
            currentValue    = InfoValue.empty;
            currentDepth    = 0;
            subElementCount = 0;
        }

        ~InfoMarkupReader()
        {
            Close();
        }

        // ====================================================================================================================================
        // Utility
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
            if (--allowed < 0)
                Error(string.Format("File size limit ({0} mbs) exceeded.", config.readLimit / 1048576));

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
        // Scope
        // ====================================================================================================================================

        private void ScopePush(int depth)
        {
            scope                   = new Scope(scope);
            scope.attributes        = new Stack<AttributeStack>();
            scope.depth             = depth;
            scope.subElementCount   = 0;
            scope.optionStart       = options.Count;
            currentTag              = null;
            currentDepth            = scope.depth;
            parameters              = InfoValue.empty;

            subElementCount = 0;
            optionStart     = options.Count;

            existingAttributes.Clear();
        }

        private void ScopePop()
        {
            // Pop attribute stacks being used by the current scope
            foreach (AttributeStack attrStack in scope.attributes)
                attrStack.values.Pop();

            // Rewind scope back to previous scope
            scope = scope.parent;

            // Reset existing tag, depth, and attributes to newly active scope
            if (scope != null)
            {
                currentTag      = scope.tag;
                currentDepth    = scope.depth;
                parameters      = scope.parameters;

                if (options.Count > optionStart)
                    options.RemoveRange(optionStart, options.Count - optionStart);

                subElementCount = ++scope.subElementCount;
                optionStart     = scope.optionStart;

                existingAttributes.Clear();
                foreach (AttributeStack attrStack in scope.attributes)
                    existingAttributes.Add(attrStack.key);
            }
            else
            {
                currentTag      = "";
                currentDepth    = 0;
                subElementCount = 0;
                optionStart     = 0;
                options.Clear();
            }
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

        public InfoValue GetAttribute(string key)
        {
            AttributeStack attr;
            
            if (dataMap.TryGetValue(key, out attr) && attr.values.Count > 0)
                return attr.values.Peek();
            
            return InfoValue.empty;
        }

        public InfoValue GetAttributeUnderived(string key)
        {
            AttributeStack attr;

            if (existingAttributes.Contains(key) && dataMap.TryGetValue(key, out attr) && attr.values.Count > 0)
                return attr.values.Peek();

            return InfoValue.empty;
        }

        public IEnumerable<(string key, InfoValue value)> Attributes()
        {
            if (scope != null)
                foreach (AttributeStack attrStack in scope.attributes)
                    yield return (attrStack.key, attrStack.values.Peek());
        }

        public IEnumerable<string> AttributeKeys()
        {
            return existingAttributes;
        }

        // ====================================================================================================================================
        // Options
        // ====================================================================================================================================

        private void AddOption(InfoValue value)
        {
            options.Add(value);
        }

        public int GetOptionCount()
        {
            return options.Count - optionStart;
        }

        public InfoValue GetOption(int index)
        {
            index += optionStart;

            return (index >= optionStart && index < options.Count) ? options[index] : InfoValue.empty;
        }

        public bool OptionsContain(string match)
        {
            for (int i = optionStart, s = options.Count; i < s; ++i)
                if (options[i].GetString() == match)
                    return true;

            return false;
        }

        public IEnumerable<InfoValue> Options()
        {
            for (int i = optionStart, s = options.Count; i < s; ++i)
                yield return options[i];
        }

        public string[] OptionsToArray()
        {
            if (optionStart < options.Count)
            {
                string[] result = new string[options.Count - optionStart];
                for (int i = optionStart, s = options.Count, j = 0; i < s; ++i, ++j)
                    result[j] = options[i].GetString();

                return result;
            }

            return null;
        }

        public void CopyOptionsTo(string[] dst, int start = 0)
        {
            if (optionStart < options.Count)
            {
                dst = new string[options.Count - optionStart];
                for (int i = optionStart, s = options.Count, j = start; i < s; ++i, ++j)
                    dst[j] = options[i].GetString();
            }
        }

        public void CopyOptionsTo(Dictionary<string, string> dst)
        {
            if (optionStart < options.Count)
            {
                for (int i = optionStart, s = options.Count, j = 0; i < s; ++i, ++j)
                    dst.Add(options[i].GetStringAt(0), options[i].GetStringAt(1));
            }
            else
                dst = null;
        }

        // ====================================================================================================================================
        // Parameters
        // ====================================================================================================================================

        public int GetParameterCount()
        {
            return parameters.GetCount();
        }

        public InfoValue GetParameter(int index)
        {
            return parameters.GetSubValue(index);
        }

        public IEnumerable<(int index, InfoValue value)> Parameters()
        {
            for (int i = 0, s = parameters.GetCount(); i < s; ++i)
                yield return (i, parameters.GetSubValue(i));
        }

        public IEnumerable<InfoValue> ParameterValues()
        {
            for (int i = 0, s = parameters.GetCount(); i < s; ++i)
                yield return parameters.GetSubValue(i);
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

            // Reset data before parsing new file
            chr             = '\0';
            pos             = 0;
            len             = 0;
            eof             = false;
            depthCurr       = 0;
            lineNumber      = 1;
            chrNumber       = 1;
            scope           = null;
            currentNodeType = NodeType.None;
            currentTag      = "";
            currentKey      = "";
            currentValue    = InfoValue.empty;
            currentDepth    = 0;
            subElementCount = 0;
            allowed         = config.readLimit;
            optionStart     = 0;
            dataMap.Clear();
            scopeLock.Clear();
            options.Clear();

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
            if (currentNodeType != NodeType.ElementClose)
                NextContentLine();
            else
                ScopePop();

            // Detect if an element is being closed
            if (scope != null && depthCurr <= scope.depth)
            {
                currentNodeType = NodeType.ElementClose;

                if (scopeLock.Count > 0 && depthCurr <= scopeLock.Peek())
                {
                    scopeLock.Pop();
                    return false;
                }

                return true;
            }
            else if (eof)
                return false;

            // Reset type and depth
            currentNodeType = NodeType.None;

            // Reading element, attribute, or option
            if (chr == '[')
                ParseElement();
            else if (chr >= '!')
                ParseAttribute();
            else
                Error("Invalid line start");

            return true;
        }

        public void SetValidator(InfoMarkupValidator validator)
        {
            activeValidator = validator;
        }

        public bool ReadValidated()
        {
            bool result = Read();
            string errorMsg = "";

            if (!activeValidator.Validate(this, ref errorMsg))
                Error(errorMsg);

            return result;
        }

        public bool ReadElement()
        {
            while (Read())
                if (currentNodeType == NodeType.ElementStart || currentNodeType == NodeType.ElementClose)
                    return true;

            return false;
        }

        public bool LockScope()
        {
            if (scope != null)
            {
                scopeLock.Push(scope.depth);
                return true;
            }

            return false;
        }

        public bool LockScope(string tag)
        {
            if (scope != null && scope.tag == tag)
            {
                scopeLock.Push(scope.depth);
                return true;
            }

            return false;
        }
    }
}
