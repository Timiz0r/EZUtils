

namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;

    public class BodyMeshAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            Transform body = avatar.transform.Find("Body");
            //will want to output these eventually, so not a bool
            //while the nonbodymesh analyzer includes inactive, we don't here because we're trying to detect the main
            //meshes, which are likely always on.
            ObjectSelectionRenderer existingMmdMeshes = ObjectSelectionRenderer.Create(
                listTitle: "MMD対応メッシュ",
                emptyMessage: "MMD対応のメッシュがこのアバターに存在しません。",
                objects: avatar
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Where(smr => MmdBlendShapeSummary.Generate(smr).HasAnyMmdBlendShapes)
                    .ToArray()
            );

            if (body == null) return AnalysisResult.Create(
                Result.NoBody,
                AnalysisResultLevel.Error,
                new GeneralRenderer(
                    "Bodyというメッシュが存在しないと、表情は変化することができません。",
                    instructions:
                        "アバターのメインメッシュ名がBodyということを確認してください。" +
                        "また、MMD対応のメッシュが存在しない場合、このアバターがもともとMMD対応ができてない可能性があります。",
                    detailRenderer: existingMmdMeshes
                ));
            if (body.TryGetComponent(out SkinnedMeshRenderer bodyMesh))
            {
                return MmdBlendShapeSummary.Generate(bodyMesh).HasAnyMmdBlendShapes
                    ? AnalysisResult.Create(
                        Result.MmdBodyMeshFound,
                        AnalysisResultLevel.Pass,
                        new GeneralRenderer(
                            "MMD対応のBodyというメッシュが発見できました。表情は普通に変化することができます。",
                            detailRenderer: existingMmdMeshes
                        ))
                    : AnalysisResult.Create(
                        Result.BodyHasNoMmdBlendShapes,
                        AnalysisResultLevel.Error, //users probably want their face to move, so an error seems more appropriate
                        new GeneralRenderer(
                            "Bodyというメッシュがありますが、ブレンドシェープが存在しません。存在しないと表情が変化することができません。",
                            instructions:
                                "アバターのメインメッシュが本当にBodyのメッシュということを確認してください。" +
                                "また、MMD対応のメッシュが存在しない場合、このアバターがもともとMMD対応ができてない可能性があります。",
                            detailRenderer: existingMmdMeshes
                        ));
            }

            if (body.GetComponent<MeshFilter>() != null) return AnalysisResult.Create(
                Result.NotSkinnedMeshRenderer,
                AnalysisResultLevel.Error,
                new GeneralRenderer(
                    "Bodyというメッシュが存在しますが、Skinned Mesh Rendererではなくて、Mesh Rendererになっています。" +
                    "Mesh Renderでのメッシュは表情が変化することができません。",
                    instructions:
                        "アバターのメインメッシュが本当にBodyのメッシュということを確認してください。" +
                        "また、MMD対応のメッシュが存在しない場合、このアバターがもともとMMD対応ができてない可能性があります。",
                    detailRenderer: existingMmdMeshes
                ));

            //so we have a body but no renderer
            return AnalysisResult.Create(
                Result.NoRendererInBody,
                AnalysisResultLevel.Error,
                new GeneralRenderer(
                    "Bodyというゲームオブジェクトがありますが、Rendererが存在していません。" +
                    "もしかして、他のメッシュが本体のメッシュになっているのでしょうか。" +
                    "そのメッシュの名前がBodyにならないと、表情が変化することができません。",
                    instructions:
                        "BodyというゲームオブジェクトのSkinned Mesh Rendererが消された可能性があります。" +
                        "オリジナルプレハブ（FBXなど）を確認してください。",
                    detailRenderer: existingMmdMeshes
                ));
        }

        public static class Result
        {
            public static readonly AnalysisResultIdentifier MmdBodyMeshFound =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>("MMD対応のBodyというメッシュを発見");
            public static readonly AnalysisResultIdentifier NoBody =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>("Bodyというゲームオブジェクトが未発見");
            public static readonly AnalysisResultIdentifier NotSkinnedMeshRenderer =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>("BodyというメッシュはSkinned Mesh Rendererではありません");
            public static readonly AnalysisResultIdentifier NoRendererInBody =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>("BodyというゲームオブジェクトにはRendererがありません");
            public static readonly AnalysisResultIdentifier BodyHasNoMmdBlendShapes =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>("BodyというメッシュにはMMD対応のブレンドシェープがありません");
        }
    }
}
