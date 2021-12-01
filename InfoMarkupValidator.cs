using System;
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
            public bool isOptional;
        }

        private class ElementRules
        {
            public ParameterRules[] allowedParameters;
            public int minParameters;
            public HashSet<string> allowedContainers;
            public HashSet<string> allowedAttributes;
            public bool optionsContainer;

            public ElementRules()
            {
                allowedParameters   = null;
                minParameters       = 0;
                allowedContainers   = null;
                allowedAttributes   = null;
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
            int i;

            if (!elementRules.TryGetValue(tag, out rules))
            {
                rules = new ElementRules();
                elementRules.Add(tag, rules);
            }

            // Add allowed parameters
            tokens                  = allowedParameters.Split(',');
            rules.allowedParameters = new ParameterRules[tokens.Length];
            i                       = 0;

            foreach (string token in tokens)
            {
                string p = token.ToLower();

                rules.allowedParameters[i] = new ParameterRules();

                if (p.EndsWith("?"))
                {
                    p = p.Substring(0, p.Length - 1);
                    rules.allowedParameters[i].isOptional = true;
                }

                subtokens = p.Split('|');

                foreach (string t in subtokens)
                {
                    switch (t)
                    {
                        case "number":
                        case "int":
                        case "float":
                            rules.allowedParameters[i].allowNumber = true;
                            break;

                        case "string":
                        case "text":
                            rules.allowedParameters[i].allowString = true;
                            break;

                        case "group":
                        case "array":
                            rules.allowedParameters[i].allowGroup = true;
                            break;
                    }
                }

                ++i;
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
                        errorMsg = string.Format("Element '{0}' does not exist in ruleset.", reader.currentTag);
                        return false;
                    }

                    if (reader.scope.parent != null)
                    {
                        if (!element.allowedContainers.Contains(reader.scope.parent.tag))
                        {
                            errorMsg = string.Format("Element '{0}' cannot be within element '{1}'.", reader.currentTag, reader.scope.parent.tag);
                            return false;
                        }
                    }
                    else if (!element.allowedContainers.Contains("/"))
                    {
                        errorMsg = string.Format("Element '{0}' cannot be a top level element.", reader.currentTag);
                        return false;
                    }

                    InfoValue parameters    = reader.scope.parameters;
                    int pCount              = parameters.GetCount();

                    if (pCount < element.minParameters || pCount > element.allowedParameters.Length)
                    {
                        errorMsg = string.Format("Element '{0}' should have {1} to {2} parameters, but was given {3}", reader.currentTag, element.minParameters, element.allowedParameters.Length, pCount);
                        return false;
                    }

                    for (int p = 0; p < pCount; ++p)
                    {
                        switch (parameters.GetSubValue(p).GetValueType())
                        {
                            case ValueType.Number:
                                if (!element.allowedParameters[p].allowNumber)
                                {
                                    errorMsg = string.Format("Parameter type mismatch for Element '{0}'", reader.currentTag);
                                    return false;
                                }
                                break;

                            case ValueType.Text:
                                if (!element.allowedParameters[p].allowString)
                                {
                                    errorMsg = string.Format("Parameter type mismatch for Element '{0}'", reader.currentTag);
                                    return false;
                                }
                                break;

                            case ValueType.Group:
                                if (!element.allowedParameters[p].allowGroup)
                                {
                                    errorMsg = string.Format("Parameter type mismatch for Element '{0}'", reader.currentTag);
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
                            errorMsg = string.Format("Attribute '{0}' is not allowed in element '{1}'", reader.currentKey, reader.currentTag);
                            return false;
                        }
                    }
                    break;
            }

            return true;
        }
    }
}