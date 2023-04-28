namespace EZUtils.Localization
{
    using System;

    internal readonly struct Operands
    {
        private static readonly decimal[] Factors = new decimal[29]
        {
            1e00m, 1e01m, 1e02m, 1e03m, 1e04m, 1e05m, 1e06m, 1e07m, 1e08m, 1e09m,
            1e10m, 1e11m, 1e12m, 1e13m, 1e14m, 1e15m, 1e16m, 1e17m, 1e18m, 1e19m,
            1e20m, 1e21m, 1e22m, 1e23m, 1e24m, 1e25m, 1e26m, 1e27m, 1e28m
        };

        public readonly decimal n; //abs(number)
        public readonly decimal i; //integer part of number
        //decimal digit counts are decimal type for use in expressions, though would ideally otherwise be int, since we index arrays with them
        public readonly decimal v; //count of fractional digits, including trailing zeroes
        public readonly decimal w; //count of fractional digits, excluding trailing zeroes
        public readonly decimal f; //fractional digits, including trailing zeroes, expressed as integer
        public readonly decimal t; //fractional digits, excluding trailing zeroes, expressed as integer
        //since the language doesnt distinguish compact exponential form, we dont have `c` and `e` here

        public Operands(decimal number)
        {
            //as ripped from source. Scale property not available at time of writing
            const int ScaleShift = 16;

            n = Math.Abs(number);
            i = Math.Truncate(number);
            v = (decimal.GetBits(number)[3] >> ScaleShift) & 31;
            w = (decimal.GetBits(number / 1.00000000000000000000000000000m)[3] >> ScaleShift) & 31;
            //don't really need to set the scale to zero, but since we definitely have a 0 scale, might as well
            //though a probably better way to do it is by constructing a new decimal
            f = (number - i) * Factors[(int)v] / 1.00000000000000000000000000000m;
            t = (number - i) * Factors[(int)w] / 1.00000000000000000000000000000m;
        }
    }
}
