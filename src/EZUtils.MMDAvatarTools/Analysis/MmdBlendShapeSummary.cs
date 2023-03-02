namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;

    public class MmdBlendShapeSummary
    {
        private static readonly MmdBlendShapeSummary Empty = new MmdBlendShapeSummary(Array.Empty<BlendShapeSummaryResult>());

        public IReadOnlyList<BlendShapeSummaryResult> Results { get; }

        public bool HasAnyMmdBlendShapes => Results.Any(r => r.ExistingBlendShapes.Any());

        private MmdBlendShapeSummary(IReadOnlyList<BlendShapeSummaryResult> results)
        {
            Results = results;
        }

        public static MmdBlendShapeSummary Generate(VRCAvatarDescriptor avatar)
        {
            Transform body = avatar.transform.Find("Body");
            if (body == null) return Empty;

            SkinnedMeshRenderer bodyMesh = body.GetComponent<SkinnedMeshRenderer>();
            if (bodyMesh == null) return Empty;

            return Generate(bodyMesh);
        }

        public static MmdBlendShapeSummary Generate(SkinnedMeshRenderer skinnedMeshRenderer)
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

            return new MmdBlendShapeSummary(results);
        }
    }
}
