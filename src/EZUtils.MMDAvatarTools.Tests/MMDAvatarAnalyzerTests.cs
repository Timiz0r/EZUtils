namespace EZUtils.MMDAvatarTools.Tests
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;

    /*
     * TODO analyzers
     * for body mesh analyzer (rename it also), not really an error if no meshes have compatible shape keys
     * error for write defaults off, downgraded to warnings if a potential weight change is detected
     * summary of blend shapes
     * warning for empty states
     * when ready to do ui rendering, modify AssertResult to ensure there's always a renderer
     * add an issue for suppressing things. gotta think about where to store them, probably in an asset. and the structure.
     */
    public class MMDAvatarAnalyzerTests
    {
        [Test]
        public void Errors_WhenNoSkinnedMeshRendererInBody()
        {
            TestSetup testSetup = new TestSetup();
            Object.DestroyImmediate(testSetup.Body.gameObject);

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshExistsAnalyzer.ResultCode.NoBody, AnalysisResultLevel.Error);
        }

        [Test]
        public void Errors_WhenNoRendererInBody()
        {
            TestSetup testSetup = new TestSetup();
            Object.DestroyImmediate(testSetup.Body);

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshExistsAnalyzer.ResultCode.NoRendererInBody, AnalysisResultLevel.Error);
        }

        [Test]
        public void Errors_WhenBodyMeshNotSkinnedMeshRenderer()
        {
            TestSetup testSetup = new TestSetup();
            GameObject bodyObject = testSetup.Body.gameObject;
            Object.DestroyImmediate(testSetup.Body);
            bodyObject.AddComponent<MeshFilter>().sharedMesh =
                GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<MeshFilter>().sharedMesh;
            _ = bodyObject.AddComponent<MeshRenderer>();

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshExistsAnalyzer.ResultCode.NotSkinnedMeshRenderer, AnalysisResultLevel.Error);
        }

        [Test]
        public void Passes_WhenBodySkinnedMeshRendererExists()
        {
            TestSetup testSetup = new TestSetup();

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshExistsAnalyzer.ResultCode.BodySkinnedMeshExists, AnalysisResultLevel.Pass);
        }

        [Test]
        public void Warns_WhenNonBodyMeshHasMMDBlendShapes()
        {
            TestSetup testSetup = new TestSetup();
            _ = testSetup.AvatarBuilder.AddObject("AnotherMesh", o => o
                .AddComponent<SkinnedMeshRenderer>(
                c => ConfigureMesh(c),
                c => AddBlendShape(c, "あ")));

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, NonBodyMeshAnalyzer.ResultCode.ContainsMMDBlendShapes, AnalysisResultLevel.Warning);
        }

        [Test]
        public void Pass_WhenNonBodyMeshHasNoMMDBlendShapes()
        {
            TestSetup testSetup = new TestSetup();
            _ = testSetup.AvatarBuilder.AddObject("AnotherMesh", o => o
                .AddComponent<SkinnedMeshRenderer>(
                c => ConfigureMesh(c)));

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, NonBodyMeshAnalyzer.ResultCode.ClearOfMMDBlendShapes, AnalysisResultLevel.Pass);
        }

        private static void AssertResult(
            IEnumerable<AnalysisResult> results, string resultCode, AnalysisResultLevel level)
            => Assert.That(
                results,
                Has.Exactly(1).Matches<AnalysisResult>(r => r.ResultCode == resultCode && r.Level == level));

        private static void ConfigureMesh(SkinnedMeshRenderer smr)
            => smr.sharedMesh =
                Object.Instantiate(
                    GameObject
                        .CreatePrimitive(PrimitiveType.Cube)
                        .GetComponent<MeshFilter>().sharedMesh);

        private static void AddBlendShape(SkinnedMeshRenderer smr, string blendShapeName)
            => smr.sharedMesh.AddBlendShapeFrame(
                blendShapeName, 100f, smr.sharedMesh.vertices, null, null);

        private class TestSetup
        {
            private readonly MMDAvatarAnalyzer analyzer;

            public SkinnedMeshRenderer Body { get; }

            public VRCAvatarDescriptor Avatar { get; }

            public ObjectBuilder AvatarBuilder { get; }

            public TestSetup()
            {
                analyzer = new MMDAvatarAnalyzer();
                SkinnedMeshRenderer body = null;
                AvatarBuilder = new ObjectBuilder("avatar")
                    .AddComponent<Animator>()
                    .AddComponent(out VRCAvatarDescriptor avatar)
                    .AddObject("Body", o => o
                        .AddComponent(
                            out body,
                            c => ConfigureMesh(c)));

                Body = body;
                Avatar = avatar;
            }

            public IReadOnlyList<AnalysisResult> Analyze() => analyzer.Analyze(Avatar);
        }
    }
}
