namespace EZUtils.Localization
{
    public class GetTextKeyword
    {
        public string Keyword { get; }
        public int? Index { get; }

        public GetTextKeyword(string keyword, int? index = null)
        {
            Keyword = keyword;
            Index = index;
        }
    }
}
