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

            public void ApplyContainers(string value)
            {
                string[] tokens = value.Split(',');

                allowedContainers = new HashSet<string>();
                foreach (string token in tokens)
                    allowedContainers.Add(token.Trim());
            }

            public void ApplyAttributes(string value)
            {
                string[] tokens = value.Split(',');

                allowedAttributes = new HashSet<string>();
                foreach (string token in tokens)
                    allowedAttributes.Add(token.Trim());
            }

            public void ApplyParameters(string value)
            {
                string[] tokens = value.Split(','), subtokens;
                string types;

                allowedParameters   = new ParameterRules[tokens.Length];
                optionalStart       = -1;

                for (int i = 0, s = tokens.Length; i < s; ++i)
                {
                    types                   = tokens[i].ToLower();
                    allowedParameters[i]    = new ParameterRules();

                    if (types.StartsWith("?"))
                    {
                        types = types.Substring(1);

                        if (optionalStart < 0)
                            optionalStart = i;
                    }

                    subtokens = types.Split('|');

                    foreach (string t in subtokens)
                    {
                        switch (t)
                        {
                            case "number": case "int": case "float":    allowedParameters[i].allowNumber    = true; break;
                            case "string": case "text":                 allowedParameters[i].allowString    = true; break;
                            case "group": case "array":                 allowedParameters[i].allowGroup     = true; break;
                        }
                    }
                }
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

        public void SetElementRules(string tag, string parameters, string containers, string attributes)
        {
            ElementRules rules;

            if (!elementRules.TryGetValue(tag, out rules))
            {
                rules = new ElementRules();
                elementRules.Add(tag, rules);
            }

            rules.ApplyContainers(containers);
            rules.ApplyAttributes(attributes);
            rules.ApplyParameters(parameters);
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