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

    //TODO: add support for vrc behaviors that blend stuff out. gotta test a bit more, but it appears all 0 weight
    //things -- animatorlayers, playables -- can help with supporting mmd worlds even with write defaults off.
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
        public IEnumerator Start_DoesNotChangeShapeKeys_WhenFXLayerHasAStateWithWriteDefaultsOff()
        {
            TestSetup testSetup = new TestSetup();
            VrcDefaultAnimatorControllers animatorControllers = new VrcDefaultAnimatorControllers();
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                defaultWeight = 1,
                stateMachine = new AnimatorStateMachine()
                {
                }
            };
            layer.stateMachine.AddState(new AnimatorState()
            {
                writeDefaultValues = false,
                motion = new AnimationClip()
            }, Vector3.zero);
            animatorControllers.FX.AddLayer(layer);
            testSetup.Avatar.baseAnimationLayers = new[]
            {
                new CustomAnimLayer
                {
                    isEnabled = true,
                    isDefault = false,
                    type = AnimLayerType.FX,
                    animatorController = animatorControllers.FX
                }
            };

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

        private class TestSetup
        {
            private static readonly AnimationClip animation =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Packages/com.timiz0r.ezutils.mmdavatartools.tests/mmdsample_vrcrobot.anim");
            private readonly MMDAvatarTester mmdAvatarTester = new MMDAvatarTester();

            public VRCAvatarDescriptor Avatar { get; }

            public TestSetup()
            {
                //the robot avatar that comes with the sdk
                GameObject avatarObject = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Packages/com.vrchat.avatars/Samples/Dynamics/Robot Avatar/Tutorial_Robot_Avatar_Dynamics_Demo_v1.fbx"));
                Avatar = avatarObject.AddComponent<VRCAvatarDescriptor>();

                //to make things more easily viewable in the game view
                Camera camera = new GameObject("cam").AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(0, 1.5f, 1), Quaternion.Euler(0, 180, 0));

                Light light = new GameObject("light").AddComponent<Light>();
                light.type = LightType.Directional;
            }

            public void StartMMDTester() => mmdAvatarTester.Start(Avatar, animation);

            public void StopMMDTester() => mmdAvatarTester.Stop();

            public float GetBlendShapeWeight(string blendShapeName)
            {
                SkinnedMeshRenderer body = Avatar.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
                float result = body.GetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex(blendShapeName));
                return result;
            }
        }
    }
}
