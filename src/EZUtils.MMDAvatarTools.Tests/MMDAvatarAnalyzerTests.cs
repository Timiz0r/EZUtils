namespace EZUtils.MMDAvatarTools.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.TestTools;
    using VRC.SDK3.Avatars.Components;
    using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

    /*
     * TODO analyzers
     * summary of blend shapes
     * when ready to do ui rendering, modify AssertResult to ensure there's always a renderer
     */
    //technically testing is a bit insufficient because we dont test sub state machines and only layers' state machines
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
            testSetup.AddedFXLayer.defaultState.motion = null;

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, EmptyStateAnalyzer.ResultCode.HasEmptyStates, AnalysisResultLevel.Warning);
        }

        [Test]
        public void Passes_WhenPlayableLayersHaveNoEmptyStates()
        {
            TestSetup testSetup = new TestSetup();

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, EmptyStateAnalyzer.ResultCode.HasNoEmptyStates, AnalysisResultLevel.Pass);
        }

        [Test]
        public void Passes_WhenFXLayerStatesHaveWriteDefaultsEnabled()
        {
            TestSetup testSetup = new TestSetup();
            foreach (AnimatorState state in testSetup.FX.layers.SelectMany(l => l.stateMachine.states).Select(s => s.state))
            {
                state.writeDefaultValues = true;
            }

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, WriteDefaultsAnalyzer.ResultCode.WriteDefaultsEnabled, AnalysisResultLevel.Pass);
        }

        [Test]
        public void Passes_WhenFXLayers1and2StatesHaveWriteDefaultsDisabled()
        {
            TestSetup testSetup = new TestSetup();
            //is the default, incidentally
            foreach (AnimatorState state in testSetup.FX.layers[1].stateMachine.states.Select(s => s.state))
            {
                state.writeDefaultValues = false;
            }
            foreach (AnimatorState state in testSetup.FX.layers[2].stateMachine.states.Select(s => s.state))
            {
                state.writeDefaultValues = false;
            }

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, WriteDefaultsAnalyzer.ResultCode.WriteDefaultsEnabled, AnalysisResultLevel.Pass);
        }

        [Test]
        public void Errors_WhenFXStatesHaveWriteDefaultsDisabled()
        {
            TestSetup testSetup = new TestSetup();
            foreach (AnimatorState state in testSetup.FX.layers.SelectMany(l => l.stateMachine.states).Select(s => s.state))
            {
                state.writeDefaultValues = false;
            }

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, WriteDefaultsAnalyzer.ResultCode.WriteDefaultsDisabled, AnalysisResultLevel.Error);
        }

        [Test]
        public void Warns_WhenFXStatesHaveWriteDefaultsDisabledButMayBecomeWeightZero()
        {
            TestSetup testSetup = new TestSetup();
            testSetup.AddedFXLayer.states[0].state.writeDefaultValues = false;
            VRCAnimatorLayerControl behaviour =
                testSetup.AddedFXLayer.states[0].state.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
            behaviour.playable = VRC.SDKBase.VRC_AnimatorLayerControl.BlendableLayer.FX;
            behaviour.layer = 3;
            behaviour.goalWeight = 0;

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, WriteDefaultsAnalyzer.ResultCode.WriteDefaultsPotentiallyDisabled, AnalysisResultLevel.Warning);
            AssertNoResult(results, WriteDefaultsAnalyzer.ResultCode.WriteDefaultsDisabled, AnalysisResultLevel.Error);
            LogAssert.Expect(LogType.Error, "AddAssetToSameFile failed because the other asset Default is not persistent");
        }

        [Test]
        public void WarnsAndErrors_WhenFXStatesHaveBothWriteDefaultsDisabledAndPotentiallyDisabledStates()
        {
            TestSetup testSetup = new TestSetup();
            testSetup.AddedFXLayer.states[0].state.writeDefaultValues = false;
            VRCAnimatorLayerControl behaviour =
                testSetup.AddedFXLayer.states[0].state.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
            behaviour.playable = VRC.SDKBase.VRC_AnimatorLayerControl.BlendableLayer.FX;
            behaviour.layer = 3;
            behaviour.goalWeight = 0;
            AnimatorStateMachine anotherLayer = testSetup.AddFXLayer("another layer");
            anotherLayer.states[0].state.writeDefaultValues = false;

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, WriteDefaultsAnalyzer.ResultCode.WriteDefaultsPotentiallyDisabled, AnalysisResultLevel.Warning);
            AssertResult(results, WriteDefaultsAnalyzer.ResultCode.WriteDefaultsDisabled, AnalysisResultLevel.Error);
            LogAssert.Expect(LogType.Error, "AddAssetToSameFile failed because the other asset Default is not persistent");
        }

        [Test]
        public void Passes_WhenAllLayer1And2TransitionsAreGestureTransitions()
        {
            TestSetup testSetup = new TestSetup();
            //default is as such

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, Layer1And2Analyzer.ResultCode.AreGestureLayers, AnalysisResultLevel.Pass);
        }

        [Test]
        public void Warns_WhenLayer1And2AnyStateTransitionsAreNonGesture()
        {
            TestSetup testSetup = new TestSetup();
            testSetup.FX.AddParameter("param", AnimatorControllerParameterType.Bool);
            testSetup.FX.layers[1].stateMachine.anyStateTransitions[2].conditions = new[]
            {
                new AnimatorCondition()
                {
                    mode = AnimatorConditionMode.If,
                    parameter = "param"
                }
            };

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, Layer1And2Analyzer.ResultCode.MayNotBeGestureLayers, AnalysisResultLevel.Warning);
        }

        [Test]
        public void Warns_WhenLayer1and2TransitionPathsHaveNoGestureParameterConditions()
        {
            //since this is the big test, will do a cursory check of both layers
            TestSetup testSetup = new TestSetup();
            testSetup.FX.AddParameter("param", AnimatorControllerParameterType.Bool);
            TestLayer(1);
            TestLayer(2);

            void TestLayer(int layer)
            {
                AnimatorStateMachine stateMachine = testSetup.FX.layers[layer].stateMachine;
                AnimatorStateTransition initialTransition = stateMachine.anyStateTransitions[3];
                AnimatorState state1 = initialTransition.destinationState;
                AnimatorState state2 = stateMachine.AddState("state2");
                AnimatorState state3 = stateMachine.AddState("state3");
                AnimatorState state4 = stateMachine.AddState("state4");
                SetNonGestureConditions(initialTransition);
                SetNonGestureConditions(state1.AddTransition(state2));
                SetNonGestureConditions(state2.AddTransition(state3));
                SetNonGestureConditions(state3.AddTransition(state4));
                SetNonGestureConditions(stateMachine.defaultState.AddTransition(state2));

                IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

                AssertResult(results, Layer1And2Analyzer.ResultCode.MayNotBeGestureLayers, AnalysisResultLevel.Warning);

                void SetNonGestureConditions(AnimatorStateTransition transition)
                    => transition.conditions = new[]
                    {
                        new AnimatorCondition()
                        {
                            mode = AnimatorConditionMode.If,
                            parameter = "param"
                        }
                    };
            }
        }

        //TODO: could kinda use more thorough testing on different kinds of paths
        [Test]
        public void Passes_WhenLayer1and2TransitionPathsHaveGestureParameterConditions()
        {
            //since this is the big test, will do a cursory check of both layers
            TestSetup testSetup = new TestSetup();
            testSetup.FX.AddParameter("param", AnimatorControllerParameterType.Bool);
            TestLayer(1, "GestureLeft");
            TestLayer(2, "GestureRight");

            void TestLayer(int layer, string gestureParameter)
            {
                AnimatorStateMachine stateMachine = testSetup.FX.layers[layer].stateMachine;
                AnimatorStateTransition initialTransition = stateMachine.anyStateTransitions[3];
                AnimatorState state1 = initialTransition.destinationState;
                AnimatorState state2 = stateMachine.AddState("state2");
                AnimatorState state3 = stateMachine.AddState("state3");
                AnimatorState state4 = stateMachine.AddState("state3");
                SetNonGestureConditions(initialTransition);
                SetNonGestureConditions(state1.AddTransition(state2));
                SetGestureConditions(state2.AddTransition(state3));
                SetNonGestureConditions(state3.AddTransition(state4));
                SetNonGestureConditions(stateMachine.defaultState.AddTransition(state2));

                IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

                AssertResult(results, Layer1And2Analyzer.ResultCode.AreGestureLayers, AnalysisResultLevel.Pass);

                void SetNonGestureConditions(AnimatorStateTransition transition)
                    => transition.conditions = new[]
                    {
                        new AnimatorCondition()
                        {
                            mode = AnimatorConditionMode.If,
                            parameter = "param"
                        }
                    };
                void SetGestureConditions(AnimatorStateTransition transition)
                    => transition.conditions = new[]
                    {
                        new AnimatorCondition()
                        {
                            mode = AnimatorConditionMode.Equals,
                            parameter = gestureParameter,
                            threshold = 0
                        }
                    };
            }
        }

        //would also then assume failures work even if circular
        [Test]
        public void Passes_WhenLayer1and2TransitionPathsHaveGestureParameterConditionsAndAreCircular()
        {
            //since this is the big test, will do a cursory check of both layers
            TestSetup testSetup = new TestSetup();
            testSetup.FX.AddParameter("param", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine stateMachine = testSetup.FX.layers[1].stateMachine;
            AnimatorStateTransition initialTransition = stateMachine.anyStateTransitions[3];
            AnimatorState state1 = initialTransition.destinationState;
            AnimatorState state2 = stateMachine.AddState("state2");
            AnimatorState state3 = stateMachine.AddState("state3");
            AnimatorState state4 = stateMachine.AddState("state3");
            SetNonGestureConditions(initialTransition);
            SetNonGestureConditions(state1.AddTransition(state2));
            SetGestureConditions(state2.AddTransition(state3));
            SetNonGestureConditions(state3.AddTransition(state4));
            SetNonGestureConditions(state4.AddTransition(state1));
            SetNonGestureConditions(stateMachine.defaultState.AddTransition(state2));

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, Layer1And2Analyzer.ResultCode.AreGestureLayers, AnalysisResultLevel.Pass);

            void SetNonGestureConditions(AnimatorStateTransition transition)
                => transition.conditions = new[]
                {
                    new AnimatorCondition()
                    {
                        mode = AnimatorConditionMode.If,
                        parameter = "param"
                    }
                };
            void SetGestureConditions(AnimatorStateTransition transition)
                => transition.conditions = new[]
                {
                    new AnimatorCondition()
                    {
                        mode = AnimatorConditionMode.Equals,
                        parameter = "GestureLeft",
                        threshold = 0
                    }
                };
        }

        private static void AssertResult(
            IEnumerable<AnalysisResult> results, string resultCode, AnalysisResultLevel level)
            => Assert.That(
                results,
                Has.Exactly(1).Matches<AnalysisResult>(r => r.ResultCode == resultCode && r.Level == level),
                $"Could not find result '{resultCode}' '{level}'. Results:\r\n\t{string.Join("\r\n\t", results.Select(r => $"'{r.ResultCode}' '{r.Level}'"))}");
        private static void AssertNoResult(
            IEnumerable<AnalysisResult> results, string resultCode, AnalysisResultLevel level)
            => Assert.That(
                results,
                Has.Exactly(0).Matches<AnalysisResult>(r => r.ResultCode == resultCode && r.Level == level),
                $"Found result '{resultCode}' '{level}' that shouldn't exist. Results:\r\n\t{string.Join("\r\n\t", results.Select(r => $"'{r.ResultCode}' '{r.Level}'"))}");

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

            public AnimatorController FX { get; }

            public AnimatorStateMachine AddedFXLayer { get; }

            public ObjectBuilder AvatarBuilder { get; }

            public TestSetup()
            {
                FX = new VrcDefaultAnimatorControllers().FX;
                AddedFXLayer = AddFXLayer("AddedLayer");

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
                                animatorController = FX,
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
            }

            public IReadOnlyList<AnalysisResult> Analyze() => analyzer.Analyze(Avatar);

            public AnimatorStateMachine AddFXLayer(string name)
            {
                AnimatorStateMachine result;
                FX.AddLayer(
                    new AnimatorControllerLayer()
                    {
                        name = name,
                        defaultWeight = 1.0f,
                        stateMachine = result = new AnimatorStateMachine()
                        {
                            name = name
                        }
                    });
                result.AddState(
                    new AnimatorState()
                    {
                        name = "Default",
                        writeDefaultValues = true,
                        motion = new AnimationClip()
                    }, Vector3.zero);
                return result;
            }
        }
    }
}
