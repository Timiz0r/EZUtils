namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;

    public class BlendShapeDefinition
    {
        public string Name { get; }
        public bool IsCommon { get; }
        public IReadOnlyList<string> PossibleNames { get; }
        public BlendShapeTarget Target { get; }

        private BlendShapeDefinition(string name, bool isCommon, IEnumerable<string> synonyms, BlendShapeTarget target)
        {
            Name = name;
            IsCommon = isCommon;
            PossibleNames = Enumerable.Repeat(name, 1).Concat(synonyms).ToArray();
            Target = target;
        }

        public static IReadOnlyList<BlendShapeDefinition> Definitions { get; } = new []
        {
            new BlendShapeDefinition(
                name: "あ",
                isCommon: true,
                synonyms: new[] { "Ah" },
                target: BlendShapeTarget.Mouth
            ),
            new BlendShapeDefinition(
                name: "い",
                isCommon: true,
                synonyms: new[] { "Ch" },
                target: BlendShapeTarget.Mouth
            ),
            new BlendShapeDefinition(
                name: "う",
                isCommon: true,
                synonyms: new[] { "U" },
                target: BlendShapeTarget.Mouth
            ),
            new BlendShapeDefinition(
                name: "え",
                isCommon: true,
                synonyms: new[] { "E" },
                target: BlendShapeTarget.Mouth
            ),
            new BlendShapeDefinition(
                name: "お",
                isCommon: true,
                synonyms: new[] { "Oh" },
                target: BlendShapeTarget.Mouth
            ),
            new BlendShapeDefinition(
                name: "にやり",
                isCommon: true,
                synonyms: new[] { "Grin" },
                target: BlendShapeTarget.Mouth
            ),
            new BlendShapeDefinition(
                name: "∧",
                isCommon: true,
                synonyms: new[] { "∧" },
                target: BlendShapeTarget.Mouth
            ),
            new BlendShapeDefinition(
                name: "ワ",
                isCommon: true,
                synonyms: new[] { "Wa" },
                target: BlendShapeTarget.Mouth
            ),
            new BlendShapeDefinition(
                name: "ω",
                isCommon: true,
                synonyms: new[] { "ω" },
                target: BlendShapeTarget.Mouth
            ),
            new BlendShapeDefinition(
                name: "▲",
                isCommon: true,
                synonyms: new[] { "▲" },
                target: BlendShapeTarget.Mouth
            ),
            new BlendShapeDefinition(
                name: "口角上げ",
                isCommon: true,
                synonyms: new[] { "Mouth Horn Raise" },
                target: BlendShapeTarget.Mouth
            ),
            new BlendShapeDefinition(
                name: "口角下げ",
                isCommon: true,
                synonyms: new[] { "Mouth Horn Lower" },
                target: BlendShapeTarget.Mouth
            ),
            new BlendShapeDefinition(
                name: "口横広げ",
                isCommon: true,
                synonyms: new[] { "Mouth Side Widen" },
                target: BlendShapeTarget.Mouth
            ),

            new BlendShapeDefinition(
                name: "まばたき",
                isCommon: true,
                synonyms: new[] { "Blink" },
                target: BlendShapeTarget.Eye
            ),
            new BlendShapeDefinition(
                name: "笑い",
                isCommon: true,
                synonyms: new[] { "Blink Happy" },
                target: BlendShapeTarget.Eye
            ),
            new BlendShapeDefinition(
                name: "はぅ",
                isCommon: true,
                synonyms: new[] { "Close><" },
                target: BlendShapeTarget.Eye
            ),
            new BlendShapeDefinition(
                name: "瞳小",
                isCommon: true,
                synonyms: new[] { "Pupil" },
                target: BlendShapeTarget.Eye
            ),
            new BlendShapeDefinition(
                name: "ｳｨﾝｸ２右",
                isCommon: true,
                synonyms: new[] { "Wink 2 Right" },
                target: BlendShapeTarget.Eye
            ),
            new BlendShapeDefinition(
                name: "ウィンク２",
                isCommon: true,
                synonyms: new[] { "Wink 2" },
                target: BlendShapeTarget.Eye
            ),
            new BlendShapeDefinition(
                name: "ウィンク",
                isCommon: true,
                synonyms: new[] { "Wink" },
                target: BlendShapeTarget.Eye
            ),
            new BlendShapeDefinition(
                name: "ウィンク右",
                isCommon: true,
                synonyms: new[] { "Wink Right" },
                target: BlendShapeTarget.Eye
            ),
            new BlendShapeDefinition(
                name: "なごみ",
                isCommon: true,
                synonyms: new[] { "Calm" },
                target: BlendShapeTarget.Eye
            ),
            new BlendShapeDefinition(
                name: "じと目",
                isCommon: true,
                synonyms: new[] { "Stare" },
                target: BlendShapeTarget.Eye
            ),
            new BlendShapeDefinition(
                name: "びっくり",
                isCommon: true,
                synonyms: new[] { "Surprised" },
                target: BlendShapeTarget.Eye
            ),
            new BlendShapeDefinition(
                name: "ｷﾘｯ",
                isCommon: true,
                synonyms: new[] { "Slant" },
                target: BlendShapeTarget.Eye
            ),
            new BlendShapeDefinition(
                name: "はぁと",
                isCommon: true,
                synonyms: new[] { "Heart" },
                target: BlendShapeTarget.Eye
            ),
            new BlendShapeDefinition(
                name: "星目",
                isCommon: true,
                synonyms: new[] { "Star Eye" },
                target: BlendShapeTarget.Eye
            ),

            new BlendShapeDefinition(
                name: "にこり",
                isCommon: true,
                synonyms: new[] { "Cheerful" },
                target: BlendShapeTarget.Eyebrow
            ),
            new BlendShapeDefinition(
                name: "上",
                isCommon: true,
                synonyms: new[] { "Upper" },
                target: BlendShapeTarget.Eyebrow
            ),
            new BlendShapeDefinition(
                name: "下",
                isCommon: true,
                synonyms: new[] { "Lower" },
                target: BlendShapeTarget.Eyebrow
            ),
            new BlendShapeDefinition(
                name: "真面目",
                isCommon: true,
                synonyms: new[] { "Serious" },
                target: BlendShapeTarget.Eyebrow
            ),
            new BlendShapeDefinition(
                name: "困る",
                isCommon: true,
                synonyms: new[] { "Sadness" },
                target: BlendShapeTarget.Eyebrow
            ),
            new BlendShapeDefinition(
                name: "怒り",
                isCommon: true,
                synonyms: new[] { "Anger" },
                target: BlendShapeTarget.Eyebrow
            ),
            new BlendShapeDefinition(
                name: "前",
                isCommon: true,
                synonyms: new[] { "Front" },
                target: BlendShapeTarget.Eyebrow
            ),

            new BlendShapeDefinition(
                name: "照れ",
                isCommon: true,
                synonyms: new[] { "Blush" },
                target: BlendShapeTarget.Other
            ),
            new BlendShapeDefinition(
                name: "にやり２",
                isCommon: true,
                synonyms: new[] { "" },
                target: BlendShapeTarget.Other
            ),
            new BlendShapeDefinition(
                name: "ん",
                isCommon: true,
                synonyms: new[] { "" },
                target: BlendShapeTarget.Other
            ),
            new BlendShapeDefinition(
                name: "あ2",
                isCommon: true,
                synonyms: new[] { "" },
                target: BlendShapeTarget.Other
            ),
            new BlendShapeDefinition(
                name: "恐ろしい子！",
                isCommon: true,
                synonyms: new[] { "" },
                target: BlendShapeTarget.Other
            ),
            new BlendShapeDefinition(
                name: "歯無し下",
                isCommon: true,
                synonyms: new[] { "" },
                target: BlendShapeTarget.Other
            ),
            new BlendShapeDefinition(
                name: "涙",
                isCommon: true,
                synonyms: new[] { "" },
                target: BlendShapeTarget.Other
            ),
        };
    }
}
