namespace EZUtils.MMDAvatarTools.Tests
{
    using System.Collections;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.TestTools;
    using VRC.SDK3.Avatars.Components;
    using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

    public class MMDAvatarTesterTests
    {
        [UnityTest]
        [RequiresPlayMode]
        public IEnumerator Start_ChangesShapeKeys_WhenAvatarDescriptorIsDefault()
        {
            //the robot avatar that comes with the sdk
            //using guid to b
            GameObject avatarObject =
                (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Packages/com.vrchat.avatars/Samples/Dynamics/Robot Avatar/Tutorial_Robot_Avatar_Dynamics_Demo_v1.fbx"));
            VRCAvatarDescriptor avatar = avatarObject.AddComponent<VRCAvatarDescriptor>();
            MMDAvatarTester mmdAvatarTester = new MMDAvatarTester();
            AnimationClip animation =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Packages/com.timiz0r.ezutils.mmdavatartools.tests/mmdsample_vrcrobot.anim");
            Camera camera = new GameObject("cam").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0, 1.5f, 1), Quaternion.Euler(0, 180, 0));

            Light light = new GameObject("light").AddComponent<Light>();
            light.type = LightType.Directional;

            mmdAvatarTester.Start(avatar, animation);
            //wanted to wait certain amounts of frames to match what's in the clip, but it appears animations don't work that way
            //waiting for a period of time seems the most reliable, at least on my machine, but perhaps we'll need to
            //change it if flaky. probably the best way would be to control time to either the underlying graph or via
            //Time class. or maybe limiting to 60fps (but then what happens in the unlikely event that we're under?).
            yield return new WaitForSeconds(0.5f);

            SkinnedMeshRenderer body = avatarObject.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
            float blinkWeight = body.GetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex("blink_both"));
            Assert.That(blinkWeight, Is.InRange(90f, 110f));
        }
        [UnityTest]
        [RequiresPlayMode]
        public IEnumerator Start_DoesNotChangeShapeKeys_WhenFXLayerHasAStateWithWriteDefaultsOff()
        {
            //the robot avatar that comes with the sdk
            //using guid to b
            GameObject avatarObject =
                (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Packages/com.vrchat.avatars/Samples/Dynamics/Robot Avatar/Tutorial_Robot_Avatar_Dynamics_Demo_v1.fbx"));
            VRCAvatarDescriptor avatar = avatarObject.AddComponent<VRCAvatarDescriptor>();
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
            avatar.baseAnimationLayers = new[]
            {
                new CustomAnimLayer
                {
                    isEnabled = true,
                    isDefault = false,
                    type = AnimLayerType.FX,
                    animatorController = animatorControllers.FX
                }
            };
            MMDAvatarTester mmdAvatarTester = new MMDAvatarTester();
            AnimationClip animation =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Packages/com.timiz0r.ezutils.mmdavatartools.tests/mmdsample_vrcrobot.anim");
            Camera camera = new GameObject("cam").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0, 1.5f, 1), Quaternion.Euler(0, 180, 0));

            Light light = new GameObject("light").AddComponent<Light>();
            light.type = LightType.Directional;

            mmdAvatarTester.Start(avatar, animation);
            //wanted to wait certain amounts of frames to match what's in the clip, but it appears animations don't work that way
            //waiting for a period of time seems the most reliable, at least on my machine, but perhaps we'll need to
            //change it if flaky. probably the best way would be to control time to either the underlying graph or via
            //Time class. or maybe limiting to 60fps (but then what happens in the unlikely event that we're under?).
            yield return new WaitForSeconds(0.5f);

            SkinnedMeshRenderer body = avatarObject.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
            float blinkWeight = body.GetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex("blink_both"));
            Assert.That(blinkWeight, Is.EqualTo(0f));
        }
    }
}
