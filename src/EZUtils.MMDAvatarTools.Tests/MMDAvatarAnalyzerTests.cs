namespace EZUtils.MMDAvatarTools.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;

    /*
     * TODO analyzers
     * warning for non-body meshes that contain mmd shapekeys
     * error for write defaults off, downgraded to warnings if a potential weight change is detected
     * summary of blend shapes
     * warning for empty states
     */
    public class MMDAvatarAnalyzerTests
    {
        [Test]
        public void Fails_WhenNoMeshNamedBodyExists()
        {
            throw new System.NotImplementedException();
        }

        [Test]
        public void Fails_WhenBodyMeshNotSkinnedMeshRenderer()
        {
            throw new System.NotImplementedException();
        }

        [Test]
        public void Passes_WhenBodySkinnedMeshRendererExists()
        {
            MMDAvatarAnalyzer analyzer = new MMDAvatarAnalyzer();
            GameObject dummyCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ObjectBuilder avatarObjectBuilder = new ObjectBuilder("avatar")
                .AddComponent<Animator>()
                .AddComponent(out VRCAvatarDescriptor avatar)
                .AddObject("Body", o => o
                    .AddComponent<SkinnedMeshRenderer>(
                        c => c.sharedMesh = UnityEngine.Object.Instantiate(dummyCube.GetComponent<MeshFilter>().sharedMesh)));
            //body.sharedMesh.AddBlendShapeFrame(
            //    "あ", 1f, body.sharedMesh.vertices, null, null);

            IReadOnlyList<AnalysisResult> results = analyzer.Analyze(avatar);

            Assert.That(
                results, Has.Exactly(1).Matches<AnalysisResult>(
                    r => r.AnalyzerType == typeof(BodyMeshExistsAnalyzer) && r.Level == AnalysisResultLevel.Pass));
        }
    }
}
