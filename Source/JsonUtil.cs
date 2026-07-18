using System.Text;

namespace TestRunner
{
    internal static class JsonUtil
    {
        public static string Escape(string s)
        {
            if (s == null)
            {
                return "\"\"";
            }

            StringBuilder b = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default: b.Append(c); break;
                }
            }

            b.Append('"');
            return b.ToString();
        }
    }
}
