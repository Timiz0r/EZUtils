namespace EZUtils
{
    using System;

    public static class ExceptionUtil
    {
        public static bool Record(Action recorder)
        {
            recorder();
            return true;
        }
    }
}
