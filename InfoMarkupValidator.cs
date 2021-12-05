using System.Collections.Generic;

namespace InfoMarkup
{
    // ========================================================================================================================================
    // InfoMarkupValidator
    // ========================================================================================================================================

    public class InfoMarkupValidator
    {
        // ====================================================================================================================================
        // Data
        // ====================================================================================================================================

        private class ParameterRules
        {
            public bool allowNumber;
            public bool allowString;
            public bool allowGroup;
        }

        private class ElementRules
        {
            public HashSet<string> allowedContainers;
            public HashSet<string> allowedAttributes;
            public ParameterRules[] allowedParameters;
            public int optionalStart;

            public ElementRules()
            {
                allowedContainers   = null;
                allowedAttributes   = null;
                allowedParameters   = null;
                optionalStart       = -1;
            }
        }

        private Dictionary<string, ElementRules> elementRules;

        // ====================================================================================================================================
        // Constructor
        // ====================================================================================================================================

        public InfoMarkupValidator()
        {
            elementRules = new Dictionary<string, ElementRules>();
        }

        // ====================================================================================================================================
        // SetElementRules
        // ====================================================================================================================================

        public void SetElementRules(string tag, string allowedParameters, string allowedContainers, string allowedAttributes)
        {
            ElementRules rules;
            HashSet<string> set;
            string[] tokens, subtokens;
            int count = 0;

            if (!elementRules.TryGetValue(tag, out rules))
            {
                rules = new ElementRules();
                elementRules.Add(tag, rules);
            }

            // Add allowed parameters
            tokens                  = allowedParameters.Split(',');
            rules.allowedParameters = new ParameterRules[tokens.Length];

            foreach (string token in tokens)
            {
                string p = token.ToLower();

                rules.allowedParameters[count] = new ParameterRules();

                if (p.StartsWith("?"))
                {
                    p = p.Substring(1);

                    if (rules.optionalStart < 0)
                        rules.optionalStart = count;
                }

                subtokens = p.Split('|');

                foreach (string t in subtokens)
                {
                    switch (t)
                    {
                        case "number":
                        case "int":
                        case "float":
                            rules.allowedParameters[count].allowNumber = true;
                            break;

                        case "string":
                        case "text":
                            rules.allowedParameters[count].allowString = true;
                            break;

                        case "group":
                        case "array":
                            rules.allowedParameters[count].allowGroup = true;
                            break;
                    }
                }

                ++count;
            }

            // Add allowed containers
            set     = new HashSet<string>();
            tokens  = allowedContainers.Split(',');
            foreach (string token in tokens)
                set.Add(token.Trim());

            rules.allowedContainers = set;

            // Add allowed attributes
            set     = new HashSet<string>();
            tokens  = allowedAttributes.Split(',');
            foreach (string token in tokens)
                set.Add(token.Trim());

            rules.allowedAttributes = set;
        }

        // ====================================================================================================================================
        // Validate
        // ====================================================================================================================================

        public bool Validate(InfoMarkupReader reader, ref string errorMsg)
        {
            ElementRules element;

            if (reader.scope == null)
                return true;

            switch (reader.currentNodeType)
            {
                case NodeType.ElementStart:
                    if (!elementRules.TryGetValue(reader.currentTag, out element))
                    {
                        errorMsg = $"Element '{reader.currentTag}' does not exist in ruleset.";
                        return false;
                    }

                    if (reader.scope.parent != null)
                    {
                        if (!element.allowedContainers.Contains(reader.scope.parent.tag))
                        {
                            errorMsg = $"Element '{reader.currentTag}' cannot be within element '{reader.scope.parent.tag}'.";
                            return false;
                        }
                    }
                    else if (!element.allowedContainers.Contains("/"))
                    {
                        errorMsg = $"Element '{reader.currentTag}' cannot be a top level element.";
                        return false;
                    }

                    InfoValue parameters    = reader.scope.parameters;
                    int pCount              = parameters.GetCount();

                    if (pCount < element.optionalStart || pCount > element.allowedParameters.Length)
                    {
                        errorMsg = $"Element '{reader.currentTag}' should have {element.optionalStart} to {element.allowedParameters.Length} parameters, but was given {pCount}";
                        return false;
                    }

                    for (int p = 0; p < pCount; ++p)
                    {
                        switch (parameters.GetSubValue(p).GetValueType())
                        {
                            case ValueType.Number:
                                if (!element.allowedParameters[p].allowNumber)
                                {
                                    errorMsg = $"Parameter type mismatch for Element '{reader.currentTag}'";
                                    return false;
                                }
                                break;
                                
                            case ValueType.Text:
                                if (!element.allowedParameters[p].allowString)
                                {
                                    errorMsg = $"Parameter type mismatch for Element '{reader.currentTag}'";
                                    return false;
                                }
                                break;

                            case ValueType.Group:
                                if (!element.allowedParameters[p].allowGroup)
                                {
                                    errorMsg = $"Parameter type mismatch for Element '{reader.currentTag}'";
                                    return false;
                                }
                                break;
                        }
                    }
                    break;

                case NodeType.Attribute:
                    if (elementRules.TryGetValue(reader.currentTag, out element))
                    {
                        if (!element.allowedAttributes.Contains(reader.currentKey))
                        {
                            errorMsg = $"Attribute '{reader.currentKey}' is not allowed in element '{reader.currentTag}'";
                            return false;
                        }
                    }
                    break;
            }

            return true;
        }
    }
}