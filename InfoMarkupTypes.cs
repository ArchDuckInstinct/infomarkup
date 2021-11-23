namespace InfoMarkup
{
    // ========================================================================================================================================
    // InfoType
    // ========================================================================================================================================

    public enum InfoType
    {
        None,
        ElementStart,   // start of an element after its tag and parameters are available
        ElementClose,   // before an element closes, so all attributes will be available
        Attribute,      //
        Option          // 
    }

    // ========================================================================================================================================
    // InfoValue (Base)
    // ========================================================================================================================================

    public class InfoValue
    {
        public virtual int GetCount()
        {
            return 0;
        }

        public virtual string GetString(string fallback = "")
        {
            return fallback;
        }

        public virtual string GetStringAt(int index, string fallback = "")
        {
            return fallback;
        }

        public virtual int GetInteger(int fallback = 0)
        {
            return fallback;
        }

        public virtual int GetIntegerAt(int index, int fallback = 0)
        {
            return fallback;
        }

        public virtual float GetFloat(float fallback = 0.0f)
        {
            return fallback;
        }

        public virtual float GetFloatAt(int index, float fallback = 0.0f)
        {
            return fallback;
        }
    }

    // ========================================================================================================================================
    // InfoString
    // ========================================================================================================================================

    public class InfoString : InfoValue
    {
        public string value;

        public InfoString(string str)
        {
            value = str;
        }

        public override string GetString(string fallback = "")
        {
            return value;
        }
    }

    // ========================================================================================================================================
    // InfoNumber
    // ========================================================================================================================================

    public class InfoNumber : InfoValue
    {
        public float value;

        public InfoNumber(float num)
        {
            value = num;
        }

        public override string GetString(string fallback = "")
        {
            return value.ToString();
        }

        public override int GetInteger(int fallback = 0)
        {
            return (int) value;
        }

        public override float GetFloat(float fallback = 0.0f)
        {
            return value;
        }
    }

    // ========================================================================================================================================
    // InfoGroup
    // ========================================================================================================================================

    public class InfoGroup : InfoValue
    {
        public InfoValue[] values;

        public InfoGroup(InfoValue[] arr)
        {
            values = arr;
        }

        public override int GetCount()
        {
            return values?.Length ?? 0;
        }

        public override string GetString(string fallback = "")
        {
            if (values == null || values.Length < 1)
                return fallback;

            string result = values[0].GetString();
            for (int i = 1, s = values.Length; i < s; ++i)
                result += ", " + values[i].GetString();

            return result;
        }

        public override string GetStringAt(int index, string fallback = "")
        {
            return (index >= 0 && index < values.Length) ? values[index].GetString(fallback) : fallback;
        }

        public override int GetInteger(int fallback = 0)
        {
            return values.Length > 0 ? values[0].GetInteger(fallback) : fallback;
        }

        public override int GetIntegerAt(int index, int fallback = 0)
        {
            return (index >= 0 && index < values.Length) ? values[index].GetInteger(fallback) : fallback;
        }

        public override float GetFloat(float fallback = 0.0f)
        {
            return values.Length > 0 ? values[0].GetFloat(fallback) : fallback;
        }

        public override float GetFloatAt(int index, float fallback = 0.0f)
        {
            return (index >= 0 && index < values.Length) ? values[index].GetFloat(fallback) : fallback;
        }
    }
}