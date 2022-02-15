
namespace Mongo.Repository
{
    public static class StringExtensions
    {
        public static string LowerFirst(this string str)
        {
            if (string.IsNullOrEmpty(str) || char.IsLower(str, 0))
                return str;
            return char.ToLowerInvariant(str[0]) + str[1..];
        }
    }
}
