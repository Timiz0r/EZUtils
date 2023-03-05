namespace EZUtils.MMDAvatarTools.Tests
{
    using System.Collections;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.TestTools;
    using VRC.SDK3.Avatars.Components;
    using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

    public class MMDAvatarTesterTests
    {
        //note for waiting:
        //wanted to wait certain amounts of frames to match what's in the clip, but it appears animations don't work that way
        //waiting for a period of time seems the most reliable, at least on my machine, but perhaps we'll need to
        //change it if flaky. probably the best way would be to control time to either the underlying graph or via
        //Time class. or maybe limiting to 60fps (but then what happens in the unlikely event that we're under?).
        [TearDown]
        public void TearDown() => TestUtils.ClearScene();

        [UnityTest]
        [RequiresPlayMode]
        public IEnumerator Start_ChangesShapeKeys_WhenAvatarDescriptorIsDefault()
        {
            TestSetup testSetup = new TestSetup();

            testSetup.StartMMDTester();
            yield return new WaitForSeconds(0.5f);

            Assert.That(testSetup.GetBlendShapeWeight("blink_both"), Is.InRange(90f, 110f));
        }

        [UnityTest]
        [RequiresPlayMode]
        public IEnumerator Start_ChangesShapeKeys_WhenLayer1and2StatesHaveWriteDefaultsOff()
        {
            TestSetup testSetup = new TestSetup();
            AnimatorControllerLayer[] layers = testSetup.FXLayer.layers;
            layers[1].stateMachine.defaultState.writeDefaultValues = false;
            layers[2].stateMachine.defaultState.writeDefaultValues = false;
            testSetup.FXLayer.layers = layers;

            testSetup.StartMMDTester();
            yield return new WaitForSeconds(0.5f);

            Assert.That(testSetup.GetBlendShapeWeight("blink_both"), Is.InRange(90f, 110f));
        }

        [UnityTest]
        [RequiresPlayMode]
        public IEnumerator Start_DoesNotChangeShapeKeys_WhenFXLayerHasAStateWithWriteDefaultsOff()
        {
            TestSetup testSetup = new TestSetup();
            _ = testSetup.AddWriteDefaultsOffState();

            testSetup.StartMMDTester();
            yield return new WaitForSeconds(0.5f);

            Assert.That(testSetup.GetBlendShapeWeight("blink_both"), Is.EqualTo(0f));
        }

        [UnityTest]
        [RequiresPlayMode]
        public IEnumerator Stop_ResetsAvatar()
        {
            TestSetup testSetup = new TestSetup();
            float initalBlendShapeWeight = testSetup.GetBlendShapeWeight("blink_both");
            Vector3 initialLeftHandPosition =
                testSetup.Avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftHand).position;

            testSetup.StartMMDTester();
            yield return new WaitForSeconds(0.5f);
            testSetup.StopMMDTester();

            Assert.That(testSetup.GetBlendShapeWeight("blink_both"), Is.EqualTo(initalBlendShapeWeight));
            Assert.That(testSetup.Avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftHand).position,
                Is.EqualTo(initialLeftHandPosition));
        }

        [UnityTest]
        [RequiresPlayMode]
        public IEnumerator Start_LetsBlendShapesAnimate_WhenFXLayerHasWriteDefaultsOffButFXIsBlendedOut()
        {
            TestSetup testSetup = new TestSetup();
            AnimatorState state = testSetup.AddWriteDefaultsOffState();
            VRCPlayableLayerControl behavior = state.AddStateMachineBehaviour<VRCPlayableLayerControl>();
            behavior.layer = VRC.SDKBase.VRC_PlayableLayerControl.BlendableLayer.FX;
            behavior.blendDuration = 0.2f;
            behavior.goalWeight = 0;

            testSetup.StartMMDTester();

            LogAssert.Expect(LogType.Error, "AddAssetToSameFile failed because the other asset  is not persistent");
            yield return new WaitForSeconds(0.5f);
            Assert.That(testSetup.GetBlendShapeWeight("blink_both"), Is.InRange(90f, 110f));
        }

        [UnityTest]
        [RequiresPlayMode]
        public IEnumerator Start_LetsBlendShapesAnimate_WhenFXLayerHasWriteDefaultsOffButFXLayerIsBlendedOut()
        {
            TestSetup testSetup = new TestSetup();
            AnimatorState state = testSetup.AddWriteDefaultsOffState();
            VRCAnimatorLayerControl behavior = state.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
            behavior.playable = VRC.SDKBase.VRC_AnimatorLayerControl.BlendableLayer.FX;
            behavior.layer = 3;
            behavior.blendDuration = 0.2f;
            behavior.goalWeight = 0;

            testSetup.StartMMDTester();

            LogAssert.Expect(LogType.Error, "AddAssetToSameFile failed because the other asset  is not persistent");
            yield return new WaitForSeconds(0.5f);
            Assert.That(testSetup.GetBlendShapeWeight("blink_both"), Is.InRange(90f, 110f));
        }

        private class TestSetup
        {
            private static readonly AnimationClip animation =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Packages/com.timiz0r.ezutils.mmdavatartools.tests/mmdsample_vrcrobot.anim");
            private readonly MmdAvatarTester mmdAvatarTester;

            public VRCAvatarDescriptor Avatar { get; }

            public AnimatorController FXLayer { get; }

            public TestSetup()
            {
                //the robot avatar that comes with the sdk
                GameObject avatarObject = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Packages/com.vrchat.avatars/Samples/Dynamics/Robot Avatar/Tutorial_Robot_Avatar_Dynamics_Demo_v1.fbx"));
                Avatar = avatarObject.AddComponent<VRCAvatarDescriptor>();
                mmdAvatarTester = avatarObject.AddComponent<MmdAvatarTester>();

                VrcDefaultAnimatorControllers animatorControllers = new VrcDefaultAnimatorControllers();
                Avatar.baseAnimationLayers = new[]
                {
                    new CustomAnimLayer
                    {
                        isDefault = false,
                        type = AnimLayerType.FX,
                        animatorController = FXLayer = animatorControllers.FX
                    }
                };

                //to make things more easily viewable in the game view
                Camera camera = new GameObject("cam").AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(0, 1.5f, 1), Quaternion.Euler(0, 180, 0));

                Light light = new GameObject("light").AddComponent<Light>();
                light.type = LightType.Directional;
            }

            public AnimatorState AddWriteDefaultsOffState()
            {
                AnimatorControllerLayer layer = new AnimatorControllerLayer
                {
                    defaultWeight = 1,
                    name = "new layer",
                    stateMachine = new AnimatorStateMachine()
                    {
                    }
                };
                AnimatorState state = new AnimatorState()
                {
                    writeDefaultValues = false,
                    motion = new AnimationClip()
                };
                layer.stateMachine.AddState(state, Vector3.zero);
                FXLayer.AddLayer(layer);

                return state;
            }

            public void StartMMDTester() => mmdAvatarTester.StartTesting(animation);

            public void StopMMDTester() => mmdAvatarTester.enabled = false;

            public float GetBlendShapeWeight(string blendShapeName)
            {
                SkinnedMeshRenderer body = Avatar.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
                float result = body.GetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex(blendShapeName));
                return result;
            }
        }
    }
}
