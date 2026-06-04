namespace DevClient
{
    public static class StringExtensions
    {
        public static string PadBoth(this string str, int length, char character)
        {
            int spaces = length - str.Length;
            int padLeft = spaces / 2 + str.Length;
            return str.PadLeft(padLeft, character).PadRight(length, character);
        }

        public static string ToTitleCase(this string str)
        {
            return str[0].ToString().ToUpper() + str[1..];
        }
    }
}





