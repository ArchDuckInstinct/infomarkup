namespace InfoMarkup
{
    // ========================================================================================================================================
    // ValueType
    // ========================================================================================================================================

    public enum ValueType
    {
        Empty,
        Text,
        Number,
        Group
    }

    // ========================================================================================================================================
    // InfoValue (Base)
    // ========================================================================================================================================

    public class InfoValue
    {
        public static InfoValue empty = new InfoValue();

        public virtual ValueType GetValueType()
        {
            return ValueType.Empty;
        }

        public virtual int GetCount()
        {
            return 0;
        }

        public virtual InfoValue GetSubValue(int i)
        {
            return null;
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

        public override ValueType GetValueType()
        {
            return ValueType.Text;
        }

        public override int GetCount()
        {
            return 1;
        }

        public override string GetString(string fallback = "")
        {
            return value;
        }

        public override string GetStringAt(int index, string fallback = "")
        {
            return index == 0 ? value : fallback;
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

        public override ValueType GetValueType()
        {
            return ValueType.Number;
        }

        public override int GetCount()
        {
            return 1;
        }

        public override string GetString(string fallback = "")
        {
            return value.ToString();
        }

        public override string GetStringAt(int index, string fallback = "")
        {
            return index == 0 ? value.ToString() : fallback;
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
        private readonly int count;

        public InfoGroup(InfoValue[] arr)
        {
            values  = arr;
            count   = arr != null ? arr.Length : 0;
        }

        public override ValueType GetValueType()
        {
            return ValueType.Group;
        }

        public override int GetCount()
        {
            return count;
        }

        public override InfoValue GetSubValue(int index)
        {
            return (index >= 0 && index < count) ? values[index] : null;
        }

        public override string GetString(string fallback = "")
        {
            if (values == null)
                return fallback;

            string result = values[0].GetString();
            for (int i = 1; i < count; ++i)
                result += ", " + values[i].GetString();

            return result;
        }

        public override string GetStringAt(int index, string fallback = "")
        {
            return (index >= 0 && index < count) ? values[index].GetString(fallback) : fallback;
        }

        public override int GetInteger(int fallback = 0)
        {
            return count > 0 ? values[0].GetInteger(fallback) : fallback;
        }

        public override int GetIntegerAt(int index, int fallback = 0)
        {
            return (index >= 0 && index < count) ? values[index].GetInteger(fallback) : fallback;
        }

        public override float GetFloat(float fallback = 0.0f)
        {
            return count > 0 ? values[0].GetFloat(fallback) : fallback;
        }

        public override float GetFloatAt(int index, float fallback = 0.0f)
        {
            return (index >= 0 && index < count) ? values[index].GetFloat(fallback) : fallback;
        }
    }
}