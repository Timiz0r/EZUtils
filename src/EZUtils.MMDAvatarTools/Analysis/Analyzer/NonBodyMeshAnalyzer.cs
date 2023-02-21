namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;

    public class NonBodyMeshAnalyzer : IAnalyzer
    {
        public IEnumerable<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            //include inactive since toggling meshes is a thing
            foreach (SkinnedMeshRenderer smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
            {
                if (smr.name.Equals("Body", StringComparison.OrdinalIgnoreCase)) continue;

                //TODO: for rendering, of course we wont early exit
                BlendShapeSummary blendShapeSummary = BlendShapeSummary.Generate(smr);
                if (blendShapeSummary.Results.Any(r => r.ExistingBlendShapes.Any()))
                {
                    return AnalysisResult.Generate(
                        ResultCode.ContainsMMDBlendShapes, AnalysisResultLevel.Warning, null);
                }
            }

            return AnalysisResult.Generate(
                ResultCode.ClearOfMMDBlendShapes, AnalysisResultLevel.Pass, null);
        }


        public static class ResultCode
        {
            public static readonly string ContainsMMDBlendShapes = Code();
            public static readonly string ClearOfMMDBlendShapes = Code();

            private static string Code([CallerMemberName] string caller = "")
                => $"{nameof(NonBodyMeshAnalyzer)}.{caller}";
        }
    }

    public class BlendShapeSummary
    {
        private static readonly BlendShapeSummary Empty = new BlendShapeSummary(Array.Empty<BlendShapeSummaryResult>());

        //could consider making class static and just returning results, but, since we have a class anyway...
        public IReadOnlyList<BlendShapeSummaryResult> Results { get; }

        private BlendShapeSummary(IReadOnlyList<BlendShapeSummaryResult> results)
        {
            Results = results;
        }

        public static BlendShapeSummary Generate(VRCAvatarDescriptor avatar)
        {
            Transform body = avatar.transform.Find("Body");
            if (body == null) return Empty;

            SkinnedMeshRenderer bodyMesh = body.GetComponent<SkinnedMeshRenderer>();
            if (bodyMesh == null) return Empty;

            return Generate(bodyMesh);
        }

        public static BlendShapeSummary Generate(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            List<BlendShapeSummaryResult> results =
                new List<BlendShapeSummaryResult>(BlendShapeDefinition.Definitions.Count);
            HashSet<string> existingBlendShapes = new HashSet<string>(
                Enumerable
                    .Range(0, skinnedMeshRenderer.sharedMesh.blendShapeCount)
                    .Select(i => skinnedMeshRenderer.sharedMesh.GetBlendShapeName(i)),
                StringComparer.OrdinalIgnoreCase);


            foreach (BlendShapeDefinition definition in BlendShapeDefinition.Definitions)
            {
                results.Add(new BlendShapeSummaryResult(
                    definition,
                    definition.PossibleNames.Where(n => existingBlendShapes.Contains(n)).ToArray()));
            }

            return new BlendShapeSummary(results);
        }
    }

    public class BlendShapeSummaryResult
    {
        public BlendShapeDefinition Definition { get; }
        //of set of name and synonyms
        public IReadOnlyList<string> ExistingBlendShapes { get; }

        public BlendShapeSummaryResult(BlendShapeDefinition definition, IReadOnlyList<string> existingBlendShapes)
        {
            Definition = definition;
            ExistingBlendShapes = existingBlendShapes;
        }
    }

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

    public enum BlendShapeTarget
    {
        Mouth,
        Eye,
        Eyebrow,
        Other
    }
}
