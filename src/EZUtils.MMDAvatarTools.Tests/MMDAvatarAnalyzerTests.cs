namespace EZUtils.MMDAvatarTools.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.TestTools;
    using VRC.SDK3.Avatars.Components;
    using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
    using Object = UnityEngine.Object;

    /*
     * TODO analyzers
     * add instructions for fixing issues
     * double check that null renderers fail tests
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

            AssertResult(results, BodyMeshAnalyzer.Result.NoBody, AnalysisResultLevel.Error);
        }

        [Test]
        public void Errors_WhenNoRendererInBody()
        {
            TestSetup testSetup = new TestSetup();
            Object.DestroyImmediate(testSetup.Body);

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshAnalyzer.Result.NoRendererInBody, AnalysisResultLevel.Error);
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

            AssertResult(results, BodyMeshAnalyzer.Result.NotSkinnedMeshRenderer, AnalysisResultLevel.Error);
        }

        [Test]
        public void Passes_WhenBodyHasMmdBlendShape()
        {
            TestSetup testSetup = new TestSetup();
            AddBlendShape(testSetup.Body, "あ");

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshAnalyzer.Result.MmdBodyMeshFound, AnalysisResultLevel.Pass);
        }

        [Test]
        public void Errors_WhenBodyHasNoMmdBlendShapes()
        {
            TestSetup testSetup = new TestSetup();

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, BodyMeshAnalyzer.Result.BodyHasNoMmdBlendShapes, AnalysisResultLevel.Error);
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

            AssertResult(results, NonBodyMeshAnalyzer.Result.ContainsMMDBlendShapes, AnalysisResultLevel.Warning);
        }

        [Test]
        public void Passes_WhenNonBodyMeshHasNoMMDBlendShapes()
        {
            TestSetup testSetup = new TestSetup();
            _ = testSetup.AvatarBuilder.AddObject("AnotherMesh", o => o
                .AddComponent<SkinnedMeshRenderer>(
                c => ConfigureMesh(c)));

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, NonBodyMeshAnalyzer.Result.ClearOfMMDBlendShapes, AnalysisResultLevel.Pass);
        }

        [Test]
        public void Warns_WhenPlayableLayersHaveEmptyStates()
        {
            TestSetup testSetup = new TestSetup();
            testSetup.AddedFXLayer.defaultState.motion = null;

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, EmptyStateAnalyzer.Result.FXLayerHasEmptyStates, AnalysisResultLevel.Warning);
        }

        [Test]
        public void Passes_WhenPlayableLayersHaveNoEmptyStates()
        {
            TestSetup testSetup = new TestSetup();

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, EmptyStateAnalyzer.Result.FXLayerHasNoEmptyStates, AnalysisResultLevel.Pass);
        }

        [Test]
        public void Passes_WhenPlayableLayersHaveEmptyStatesButTheLayerIsAlwaysDisabled()
        {
            TestSetup testSetup = new TestSetup();
            testSetup.AddedFXLayer.defaultState.motion = null;
            AnimatorControllerLayer[] layers = testSetup.FX.layers;
            layers.Last().defaultWeight = 0;
            testSetup.FX.layers = layers;

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, EmptyStateAnalyzer.Result.FXLayerHasNoEmptyStates, AnalysisResultLevel.Pass);
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

            AssertResult(results, WriteDefaultsAnalyzer.Result.WriteDefaultsEnabled, AnalysisResultLevel.Pass);
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

            AssertResult(results, WriteDefaultsAnalyzer.Result.WriteDefaultsEnabled, AnalysisResultLevel.Pass);
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

            AssertResult(results, WriteDefaultsAnalyzer.Result.WriteDefaultsDisabled, AnalysisResultLevel.Error);
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

            AssertResult(results, WriteDefaultsAnalyzer.Result.WriteDefaultsPotentiallyDisabled, AnalysisResultLevel.Warning);
            AssertNoResult(results, WriteDefaultsAnalyzer.Result.WriteDefaultsDisabled, AnalysisResultLevel.Error);
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

            AssertResult(results, WriteDefaultsAnalyzer.Result.WriteDefaultsPotentiallyDisabled, AnalysisResultLevel.Warning);
            AssertResult(results, WriteDefaultsAnalyzer.Result.WriteDefaultsDisabled, AnalysisResultLevel.Error);
            LogAssert.Expect(LogType.Error, "AddAssetToSameFile failed because the other asset Default is not persistent");
        }

        [Test]
        public void Passes_WhenFXStateHasWriteDefaultsDisabledButLayerIsAlwaysDisabled()
        {
            TestSetup testSetup = new TestSetup();
            testSetup.AddedFXLayer.states[0].state.writeDefaultValues = false;
            AnimatorControllerLayer[] layers = testSetup.FX.layers;
            layers.Last().defaultWeight = 0;
            testSetup.FX.layers = layers;

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, WriteDefaultsAnalyzer.Result.WriteDefaultsEnabled, AnalysisResultLevel.Pass);
        }

        [Test]
        public void Passes_WhenAllLayer1And2TransitionsAreGestureTransitions()
        {
            TestSetup testSetup = new TestSetup();
            //default is as such

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, Layer1And2Analyzer.Result.Layer1_IsGestureLayer, AnalysisResultLevel.Pass);
            AssertResult(results, Layer1And2Analyzer.Result.Layer2_IsGestureLayer, AnalysisResultLevel.Pass);
        }

        [Test]
        public void Passes_WhenNoCustomFXLayer()
        {
            TestSetup testSetup = new TestSetup();
            testSetup.Avatar.baseAnimationLayers = Array.Empty<CustomAnimLayer>();

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(results, Layer1And2Analyzer.Result.Layer1_IsGestureLayer, AnalysisResultLevel.Pass);
            AssertResult(results, Layer1And2Analyzer.Result.Layer2_IsGestureLayer, AnalysisResultLevel.Pass);
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

            AssertResult(results, Layer1And2Analyzer.Result.Layer1_MayNotBeGestureLayer, AnalysisResultLevel.Warning);
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

                AssertResult(
                    results,
                    layer == 1
                        ? Layer1And2Analyzer.Result.Layer1_MayNotBeGestureLayer
                        : Layer1And2Analyzer.Result.Layer2_MayNotBeGestureLayer,
                    AnalysisResultLevel.Warning);

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

        //TODO: could kinda use more thorough testing on different kinds of paths, like exits
        //will take care of that when adding support for sub state machines
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

                AssertResult(
                    results,
                    layer == 1
                        ? Layer1And2Analyzer.Result.Layer1_IsGestureLayer
                        : Layer1And2Analyzer.Result.Layer2_IsGestureLayer,
                    AnalysisResultLevel.Pass);

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

            AssertResult(results, Layer1And2Analyzer.Result.Layer1_IsGestureLayer, AnalysisResultLevel.Pass);

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

        [Test]
        public void AnalyzerErrors_WhenAnalyzerThrowsException()
        {
            TestSetup testSetup = new TestSetup();
            MmdAvatarAnalyzer analyzer = new MmdAvatarAnalyzer(new IAnalyzer[]
            {
                //we go with two to ensure others after a failure get run
                new AlwaysFailsAnalyzer(),
                new AlwaysFailsAnalyzer()
            });

            //dont really care about the other configuration
            IReadOnlyList<AnalysisResult> results = analyzer.Analyze(testSetup.Avatar);

            Assert.That(
                results,
                Has.Exactly(2).Matches<AnalysisResult>(r => r.Level == AnalysisResultLevel.AnalyzerError));
        }

        [Test]
        public void Passes_WhenFXLayerHasNoHumanoidAnimations()
        {
            TestSetup testSetup = new TestSetup();

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(
                results, HumanoidAnimationAnalyzer.Result.NoActiveHumanoidAnimationsFound, AnalysisResultLevel.Pass);
        }

        [Test]
        public void Passes_WhenFXLayerHasNoUserDefinedMaskRegardlessOfHumanoidAnimations()
        {
            TestSetup testSetup = new TestSetup();
            testSetup.SetFXMask(null);
            AnimationClip clip = new AnimationClip();
            clip.SetCurve("", typeof(Animator), "Head Turn Left-Right", AnimationCurve.Constant(0, 1, 0));
            _ = testSetup.AddedFXLayer.AddState("state").motion = clip;

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            //hoping to establish both are true to make life easier for me
            Assert.That(clip.humanMotion, Is.True);
            Assert.That(clip.isHumanMotion, Is.True);
            AssertResult(
                results, HumanoidAnimationAnalyzer.Result.NoActiveHumanoidAnimationsFound, AnalysisResultLevel.Pass);
        }

        [Test]
        public void WarnsAndErrors_WhenFXLayerHasUnmaskedOutHumanoidAnimations()
        {
            TestSetup testSetup = new TestSetup();
            testSetup.SetFXMask(VrcAvatarMasks.MuscleOnly);
            AnimationClip clip = new AnimationClip();
            clip.SetCurve("", typeof(Animator), "Head Turn Left-Right", AnimationCurve.Constant(0, 1, 0));

            AnimatorControllerLayer activeLayer = new AnimatorControllerLayer()
            {
                name = "active",
                stateMachine = new AnimatorStateMachine(),
                defaultWeight = 1,
            };
            testSetup.FX.AddLayer(activeLayer);
            activeLayer.stateMachine.AddState("active").motion = clip;

            AnimatorControllerLayer possiblyActiveLayer = new AnimatorControllerLayer()
            {
                name = "possibly active",
                stateMachine = new AnimatorStateMachine(),
                defaultWeight = 1,
            };
            testSetup.FX.AddLayer(possiblyActiveLayer);
            possiblyActiveLayer.stateMachine.AddState("active").motion = clip;
            VRCAnimatorLayerControl behavior = possiblyActiveLayer.stateMachine.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
            behavior.goalWeight = 0;
            behavior.layer = testSetup.FX.layers.Length - 1;
            behavior.playable = VRC.SDKBase.VRC_AnimatorLayerControl.BlendableLayer.FX;

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(
                results, HumanoidAnimationAnalyzer.Result.PossiblyActiveHumanoidAnimationsFound, AnalysisResultLevel.Warning);
            AssertResult(
                results, HumanoidAnimationAnalyzer.Result.ActiveHumanoidAnimationsFound, AnalysisResultLevel.Error);
            LogAssert.Expect(LogType.Error, "AddAssetToSameFile failed because the other asset  is not persistent");
        }

        [Test]
        public void Passes_WhenFXLayerHasHumanoidAnimationButLayerIsAlwaysDisabled()
        {
            TestSetup testSetup = new TestSetup();
            AnimationClip clip = new AnimationClip();
            clip.SetCurve("", typeof(Animator), "Head Turn Left-Right", AnimationCurve.Constant(0, 1, 0));
            _ = testSetup.AddedFXLayer.AddState("state").motion = clip;
            AnimatorControllerLayer[] layers = testSetup.FX.layers;
            layers.Last().defaultWeight = 0;
            testSetup.FX.layers = layers;

            IReadOnlyList<AnalysisResult> results = testSetup.Analyze();

            AssertResult(
                results, HumanoidAnimationAnalyzer.Result.NoActiveHumanoidAnimationsFound, AnalysisResultLevel.Pass);
        }

        private static void AssertResult(
            IEnumerable<AnalysisResult> results, AnalysisResultIdentifier result, AnalysisResultLevel level)
        {
            Assert.That(
                results,
                Has.Exactly(1).Matches<AnalysisResult>(r => r.Result == result && r.Level == level),
                $"Could not find result '{result.Code}' '{level}'. Results:\r\n\t{string.Join("\r\n\t", results.Select(r => $"'{r.Result.Code}' '{r.Level}'"))}");
            AssertRenderersNonNull(results);
        }

        private static void AssertNoResult(
            IEnumerable<AnalysisResult> results, AnalysisResultIdentifier result, AnalysisResultLevel level)
        {
            Assert.That(
                results,
                Has.Exactly(0).Matches<AnalysisResult>(r => r.Result == result && r.Level == level),
                $"Found result '{result.Code}' '{level}' that shouldn't exist. Results:\r\n\t{string.Join("\r\n\t", results.Select(r => $"'{r.Result.Code}' '{r.Level}'"))}");
            AssertRenderersNonNull(results);
        }

        private static void AssertRenderersNonNull(IEnumerable<AnalysisResult> results)
            => Assert.That(
                results.Select(r => r.Renderer),
                Has.All.Not.Null,
                $"Found null renderers. Results:\r\n\t{string.Join("\r\n\t", results.Select(r => r.Result.Code))}");

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
                                type = AnimLayerType.FX,
                                mask = FX.layers[0].avatarMask
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

            public void SetFXMask(AvatarMask mask)
            {
                AnimatorControllerLayer[] animatorLayers = FX.layers;
                animatorLayers[0].avatarMask = mask;
                FX.layers = animatorLayers;

                int fxLayerIndex = Array.FindIndex(Avatar.baseAnimationLayers, l => l.type == AnimLayerType.FX);
                CustomAnimLayer existingLayer = Avatar.baseAnimationLayers[fxLayerIndex];
                Avatar.baseAnimationLayers[fxLayerIndex] = new CustomAnimLayer
                {
                    animatorController = existingLayer.animatorController,
                    isDefault = existingLayer.isDefault,
                    isEnabled = existingLayer.isEnabled,
                    type = AnimLayerType.FX,
                    mask = mask
                };
            }
        }
    }
}
