namespace System
{
    public static class ByteArrayExtensions
    {
        public static string ToBase64String(this byte[] value)
        {
            return Convert.ToBase64String(value);
        }

        public static string ToS3ETagString(this byte[] value)
        {
            return BitConverter.ToString(value).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
