

namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;
    using static Localization;

    public class BodyMeshAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            Transform body = avatar.transform.Find("Body");
            ObjectSelectionRenderer existingMmdMeshes = ObjectSelectionRenderer.Create(
                listTitle: T("MMD-compatible mesh"),
                emptyMessage: T("There are no MMD-compatible meshes on this avatar."),
                objects: avatar
                    //while the nonbodymesh analyzer includes inactive, we don't here because we're trying to detect the main
                    //meshes, which are likely always on.
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Where(smr => MmdBlendShapeSummary.Generate(smr).HasAnyMmdBlendShapes)
                    .ToArray()
            );

            if (body == null) return AnalysisResult.Create(
                Result.NoBody,
                AnalysisResultLevel.Error,
                new GeneralRenderer(
                    T("If there are no meshes named 'Body', it is not possible to change facial expressions."),
                    instructions: T(
                        "Ensure the main mesh of the avatar is called 'Body'. " +
                        "Be aware that, if there is no such mesh, the avatar may simply not be meant to be MMD-compatible."),
                    detailRenderer: existingMmdMeshes
                ));
            if (body.TryGetComponent(out SkinnedMeshRenderer bodyMesh))
            {
                return MmdBlendShapeSummary.Generate(bodyMesh).HasAnyMmdBlendShapes
                    ? AnalysisResult.Create(
                        Result.MmdBodyMeshFound,
                        AnalysisResultLevel.Pass,
                        new GeneralRenderer(
                            T("An MMD-compatible mesh called 'Body' was found. Facial expressions should work fine."),
                            detailRenderer: existingMmdMeshes
                        ))
                    : AnalysisResult.Create(
                        Result.BodyHasNoMmdBlendShapes,
                        AnalysisResultLevel.Error, //users probably want their face to move, so an error seems more appropriate
                        new GeneralRenderer(
                            T("A mesh called 'Body' was found, but there are no MMD-compatible blendshapes. " +
                            "Without MMD-compatible blendshapes, facial expressions will not change."),
                            instructions: T(
                                "Confirm that the main mesh of the avatar is actually called 'Body'. " +
                                "Be aware that, if there is no MMD-compatible mesh, the avatar may simply not be meant to be MMD-compatible."),
                            detailRenderer: existingMmdMeshes
                        ));
            }

            if (body.GetComponent<MeshFilter>() != null) return AnalysisResult.Create(
                Result.NotSkinnedMeshRenderer,
                AnalysisResultLevel.Error,
                new GeneralRenderer(
                    T("A mesh named 'Body' was found, but, instead of being a 'Skinned Mesh Renderer', it is a 'Mesh Renderer'. " +
                    "It is not possible to change facial expressions if the mesh is a 'Mesh Renderer'."),
                    instructions: T(
                        "Confirm that the main mesh of the avatar is actually called 'Body'. " +
                        "Be aware that, if there is no MMD-compatible mesh, the avatar may simply not be meant to be MMD-compatible."),
                    detailRenderer: existingMmdMeshes
                ));

            //so we have a body but no renderer
            return AnalysisResult.Create(
                Result.NoRendererInBody,
                AnalysisResultLevel.Error,
                new GeneralRenderer(
                    T("An object named 'Body' was found, but it has no renderer. " +
                    "In the event the mesh of the avatar is in another object, " +
                    "note that it must be named 'Body' for facial expressions to change."),
                    instructions: T(
                        "There is a possibility the 'Skinned Mesh Renderer' component was deleted accidentally. " +
                        "Confirm what the 'Body' object should be in the original prefab (such as the '.fbx' file). " +
                        "Otherwise, the main mesh may be in another object altogether."),
                    detailRenderer: existingMmdMeshes
                ));
        }

        public static class Result
        {
            public static readonly AnalysisResultIdentifier MmdBodyMeshFound =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>(T("MMD-compatible mesh found"));
            public static readonly AnalysisResultIdentifier NoBody =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>(T("Object named 'Body' not found"));
            public static readonly AnalysisResultIdentifier NotSkinnedMeshRenderer =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>(T("Mesh named 'Body' is not a 'Skinned Mesh Renderer'"));
            public static readonly AnalysisResultIdentifier NoRendererInBody =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>(T("Object named 'Body' has no renderer"));
            public static readonly AnalysisResultIdentifier BodyHasNoMmdBlendShapes =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>(T("Mesh named 'Body' has no MMD-compatible blendshapes"));
        }
    }
}
