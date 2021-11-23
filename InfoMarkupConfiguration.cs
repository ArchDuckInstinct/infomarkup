using System;
using System.Collections.Generic;

namespace InfoMarkup
{
    // ========================================================================================================================================
    // InfoMarkupConfiguration
    // ========================================================================================================================================

    public class InfoMarkupConfiguration
    {
        // ====================================================================================================================================
        // Defaults
        // ====================================================================================================================================

        private const int BUFFER_SIZE_MIN       = 1024;
        private const int BUILDER_INITIAL_MIN   = 256;      // TODO: consider removal of builder initial size field
        private const int BUILDER_SIZE_MIN      = 1024;
        private const int GROUP_LIMIT_MIN       = 16;

        // ====================================================================================================================================
        // Data
        // ====================================================================================================================================

        private Dictionary<string, float> unitScaleMap;
        public readonly int bufferSize;
        public readonly int builderInitial;
        public readonly int builderSize;
        public readonly int groupLimit;

        // ====================================================================================================================================
        // Constructor
        // ====================================================================================================================================

        public InfoMarkupConfiguration(int pBufferSize, int pBuilderSize, int pGroupLimit)
        {
            unitScaleMap    = new Dictionary<string, float>();
            bufferSize      = Math.Max(pBufferSize, BUFFER_SIZE_MIN);
            builderInitial  = BUILDER_INITIAL_MIN;
            builderSize     = Math.Max(pBuilderSize, BUILDER_SIZE_MIN);
            groupLimit      = Math.Max(pGroupLimit, GROUP_LIMIT_MIN);
        }

        public InfoMarkupConfiguration() : this(BUFFER_SIZE_MIN, BUILDER_SIZE_MIN, GROUP_LIMIT_MIN)
        {

        }

        // ====================================================================================================================================
        // Unit Scaling
        // ====================================================================================================================================

        public bool AddUnitScale(string postfix, float scale)
        {
            if (postfix[0] >= '0' && postfix[0] <= '9') // postfix cannot start with a number because it immediately follows an integer or float
                return false;

            postfix = postfix.ToLower();

            if (unitScaleMap.ContainsKey(postfix))
                unitScaleMap[postfix] = scale;
            else
                unitScaleMap.Add(postfix, scale);

            return true;
        }

        public bool RemoveUnitScale(string postfix)
        {
            return unitScaleMap.Remove(postfix.ToLower());
        }

        public bool ApplyUnitScale(string postfix, ref float value)
        {
            float scale;

            if (unitScaleMap.TryGetValue(postfix.ToLower(), out scale))
            {
                value = value * scale;
                return true;
            }

            return false;
        }
    }
}
