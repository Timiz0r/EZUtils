namespace EZUtils.MMDAvatarTools.Tests
{
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
        public void Fails_WhenNoSkinnedMeshRendererInBody()
        {
            TestSetup testSetup = new TestSetup();
            Object.DestroyImmediate(testSetup.Body.gameObject);

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshExistsAnalyzer.ResultCode.NoBody, AnalysisResultLevel.Error);
        }
        [Test]
        public void Fails_WhenNoRendererInBody()
        {
            TestSetup testSetup = new TestSetup();
            Object.DestroyImmediate(testSetup.Body);

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshExistsAnalyzer.ResultCode.NoRendererInBody, AnalysisResultLevel.Error);
        }

        [Test]
        public void Fails_WhenBodyMeshNotSkinnedMeshRenderer()
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
            //was an experiment that will get used later. keeping here for now
            //body.sharedMesh.AddBlendShapeFrame(
            //    "あ", 1f, body.sharedMesh.vertices, null, null);

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshExistsAnalyzer.ResultCode.Pass, AnalysisResultLevel.Pass);
        }

        private static void AssertResult(
            IEnumerable<AnalysisResult> results, string resultCode, AnalysisResultLevel level)
            => Assert.That(
                results,
                Has.Exactly(1).Matches<AnalysisResult>(r => r.ResultCode == resultCode && r.Level == level));

        private class TestSetup
        {
            private readonly MMDAvatarAnalyzer analyzer;

            public SkinnedMeshRenderer Body { get; }

            public VRCAvatarDescriptor Avatar { get; }

            public TestSetup()
            {
                analyzer = new MMDAvatarAnalyzer();
                GameObject dummyCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SkinnedMeshRenderer body = null;
                ObjectBuilder avatarObjectBuilder = new ObjectBuilder("avatar")
                    .AddComponent<Animator>()
                    .AddComponent(out VRCAvatarDescriptor avatar)
                    .AddObject("Body", o => o
                        .AddComponent(
                        out body,
                            c => c.sharedMesh = UnityEngine.Object.Instantiate(dummyCube.GetComponent<MeshFilter>().sharedMesh)));

                Body = body;
                Avatar = avatar;
            }

            public IReadOnlyList<AnalysisResult> Analyze() => analyzer.Analyze(Avatar);
        }
    }
}
