namespace EZUtils.MMDAvatarTools.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor.Animations;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;
    using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

    /*
     * TODO analyzers
     * error for write defaults off, downgraded to warnings if a potential weight change is detected
     * summary of blend shapes
     * warning for empty states
     * when ready to do ui rendering, modify AssertResult to ensure there's always a renderer
     * add an issue for suppressing things. gotta think about where to store them, probably in an asset. and the structure.
     * fx layers 1 and 2 (not 0) do non-hands stuff
     */
    //TODO: for both analyzer and tester, take a look at descriptor. there should be an extra bool or two for enabling customized layers.
    public class MMDAvatarAnalyzerTests
    {
        [Test]
        public void Errors_WhenNoSkinnedMeshRendererInBody()
        {
            TestSetup testSetup = new TestSetup();
            Object.DestroyImmediate(testSetup.Body.gameObject);

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshAnalyzer.ResultCode.NoBody, AnalysisResultLevel.Error);
        }

        [Test]
        public void Errors_WhenNoRendererInBody()
        {
            TestSetup testSetup = new TestSetup();
            Object.DestroyImmediate(testSetup.Body);

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshAnalyzer.ResultCode.NoRendererInBody, AnalysisResultLevel.Error);
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

            AssertResult(results, BodyMeshAnalyzer.ResultCode.NotSkinnedMeshRenderer, AnalysisResultLevel.Error);
        }

        [Test]
        public void Passes_WhenBodyHasMmdBlendShape()
        {
            TestSetup testSetup = new TestSetup();
            AddBlendShape(testSetup.Body, "あ");

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshAnalyzer.ResultCode.MmdBodyMeshFound, AnalysisResultLevel.Pass);
        }

        [Test]
        public void Errors_WhenBodyHasNoMmdBlendShapes()
        {
            TestSetup testSetup = new TestSetup();

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshAnalyzer.ResultCode.BodyHasNoMmdBlendShapes, AnalysisResultLevel.Error);
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
        public void Passes_WhenNonBodyMeshHasNoMMDBlendShapes()
        {
            TestSetup testSetup = new TestSetup();
            _ = testSetup.AvatarBuilder.AddObject("AnotherMesh", o => o
                .AddComponent<SkinnedMeshRenderer>(
                c => ConfigureMesh(c)));

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, NonBodyMeshAnalyzer.ResultCode.ClearOfMMDBlendShapes, AnalysisResultLevel.Pass);
        }

        [Test]
        public void Warns_WhenPlayableLayersHaveEmptyStates()
        {
            TestSetup testSetup = new TestSetup();
            testSetup.FXLayer.layers[2].stateMachine.defaultState.motion = null;

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, EmptyStateAnalyzer.ResultCode.HasEmptyStates, AnalysisResultLevel.Warning);
        }

        [Test]
        public void Passes_WhenPlayableLayersHaveNoEmptyStates()
        {
            TestSetup testSetup = new TestSetup();
            //the default should pass

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, EmptyStateAnalyzer.ResultCode.HasNoEmptyStates, AnalysisResultLevel.Pass);
        }

        private static void AssertResult(
            IEnumerable<AnalysisResult> results, string resultCode, AnalysisResultLevel level)
            => Assert.That(
                results,
                Has.Exactly(1).Matches<AnalysisResult>(r => r.ResultCode == resultCode && r.Level == level),
                $"Could not find result '{resultCode}' '{level}'. Result:\r\n\t{string.Join("\r\n\t", results.Select(r => $"'{r.ResultCode}' '{r.Level}'"))}");

        private static void ConfigureMesh(SkinnedMeshRenderer smr)
            => smr.sharedMesh =
                //not sure if copying is even necessary, but, just in case...
                Object.Instantiate(
                    GameObject
                        .CreatePrimitive(PrimitiveType.Cube)
                        .GetComponent<MeshFilter>().sharedMesh);

        private static void AddBlendShape(SkinnedMeshRenderer smr, string blendShapeName)
            => smr.sharedMesh.AddBlendShapeFrame(
                blendShapeName, 100f, smr.sharedMesh.vertices, null, null);

        private class TestSetup
        {
            private readonly MmdAvatarAnalyzer analyzer;

            public SkinnedMeshRenderer Body { get; }

            public VRCAvatarDescriptor Avatar { get; }

            public AnimatorController FXLayer { get; }

            public ObjectBuilder AvatarBuilder { get; }

            public TestSetup()
            {
                VrcDefaultAnimatorControllers controllers = new VrcDefaultAnimatorControllers();
                analyzer = new MmdAvatarAnalyzer();
                SkinnedMeshRenderer body = null;
                AvatarBuilder = new ObjectBuilder("avatar")
                    .AddComponent<Animator>()
                    .AddComponent(
                        out VRCAvatarDescriptor avatar,
                        c => c.baseAnimationLayers = new[]
                        {
                            new CustomAnimLayer
                            {
                                animatorController = controllers.FX,
                                isDefault = false,
                                isEnabled = true,
                                type = AnimLayerType.FX
                            }
                        })
                    .AddObject("Body", o => o
                        .AddComponent(
                            out body,
                            c => ConfigureMesh(c)));

                Body = body;
                Avatar = avatar;
                FXLayer = controllers.FX;
            }

            public IReadOnlyList<AnalysisResult> Analyze() => analyzer.Analyze(Avatar);
        }
    }
}
