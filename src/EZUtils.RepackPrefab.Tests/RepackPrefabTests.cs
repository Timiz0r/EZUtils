namespace EZUtils.RepackPrefab.Tests
{
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    public class RepackPrefabTests
    {
        [TearDown]
        public void TearDown() => TestUtils.StandardTearDown();


        [Test]
        public void ReturnsVariantPrefab()
        {
            GameObject referenceObject = new ObjectBuilder("refObject")
                .GetObject();
            GameObject referencePrefab = new ObjectBuilder("refPrefab")
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(referenceObject, referencePrefab);

            Assert.That(PrefabUtility.IsPartOfVariantPrefab(newPrefab), Is.True);
        }

        [Test]
        public void Throws_WhenReferencePrefabNoReferencetPrefab()
        {
            GameObject referenceObject = new ObjectBuilder("refObject")
                .GetObject();
            GameObject referencePrefab = new ObjectBuilder("refPrefab")
                .GetObject();

            Assert.That(() => RepackPrefab.Repack(referenceObject, referencePrefab), Throws.InvalidOperationException);
        }

        [Test]
        public void AddsObject_WhenNotInReferencePrefab()
        {
            GameObject referenceObject = new ObjectBuilder("refObject")
                .AddObject("child")
                .GetObject();
            GameObject referencePrefab = new ObjectBuilder("refPrefab")
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(referenceObject, referencePrefab);

            Assert.That(newPrefab.GetChildren(), Has.Exactly(1).With.Property("name").EqualTo("child"));
        }

        [Test]
        public void AddsPrefabInstance_WhenNotInReferencePrefab()
        {
            //so one of the child objects of referenceObject is a prefab instance
            GameObject childObject = new GameObject("child");
            GameObject childObjectPrefab = TestUtils.CreatePrefab(childObject);
            GameObject referenceObject = new ObjectBuilder("refObject")
                .AddObject(childObject)
                .GetObject();
            GameObject referencePrefab = new ObjectBuilder("refPrefab")
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(referenceObject, referencePrefab);
            GameObject newPrefabChildObject = newPrefab.GetChildren().Single();

            Assert.That(
                PrefabUtility.GetCorrespondingObjectFromOriginalSource(newPrefabChildObject), Is.EqualTo(childObjectPrefab));
        }

        [Test]
        public void AddsComponent_WhenNotInReferencePrefab()
        {
            GameObject referenceObject = new ObjectBuilder("refObject")
                .AddComponent<BoxCollider>()
                .GetObject();
            GameObject referencePrefab = new ObjectBuilder("refPrefab")
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(referenceObject, referencePrefab);

            Assert.That(newPrefab.GetComponent<BoxCollider>(), Is.Not.Null);
        }

        [Test]
        public void ChangesComponentValue_WhenInBoth()
        {
            GameObject referenceObject = new ObjectBuilder("refObject")
                .AddComponent<BoxCollider>(bc => bc.isTrigger = true)
                .GetObject();
            GameObject referencePrefab = new ObjectBuilder("refPrefab")
                .AddComponent<BoxCollider>(bc => bc.isTrigger = false)
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(referenceObject, referencePrefab);

            Assert.That(newPrefab.GetComponent<BoxCollider>().isTrigger, Is.True);
        }

        [Test]
        public void RemovesComponent_WhenOnlyInReferencePrefab()
        {
            GameObject referenceObject = new ObjectBuilder("refObject")
                .GetObject();
            GameObject referencePrefab = new ObjectBuilder("refPrefab")
                .AddComponent<BoxCollider>()
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(referenceObject, referencePrefab);
            Assert.That(newPrefab.GetComponent<BoxCollider>(), Is.Null);
        }

        [Test]
        public void DoesNotReplaceObjectReferences_WhenOutsideReferencePrefabObjectHierarchy()
        {
            GameObject externalProbeAnchor = new GameObject("obj outside reference obj");
            GameObject referenceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            referenceObject.name = "refObject";
            referenceObject.GetComponent<MeshRenderer>().probeAnchor = externalProbeAnchor.transform;

            GameObject referencePrefabObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            referencePrefabObject.name = "refPrefab";
            GameObject referencePrefab = TestUtils.CreatePrefab(referencePrefabObject);

            GameObject newPrefab = RepackPrefab.Repack(referenceObject, referencePrefab);

            Assert.That(newPrefab.GetComponent<MeshRenderer>().probeAnchor, Is.EqualTo(externalProbeAnchor.transform));
        }

        [Test]
        public void ReplacesObjectReferences_WhenInsideReferencePrefabObjectHierarchy()
        {
            GameObject referenceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            referenceObject.name = "refObject";
            GameObject referenceObjectProbeAnchor = new GameObject("probe anchor");
            referenceObjectProbeAnchor.transform.SetParent(referenceObject.transform);
            referenceObject.GetComponent<MeshRenderer>().probeAnchor = referenceObjectProbeAnchor.transform;

            GameObject referencePrefabObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            referencePrefabObject.name = "refPrefab";
            GameObject referencePrefab = TestUtils.CreatePrefab(referencePrefabObject);

            GameObject newPrefab = RepackPrefab.Repack(referenceObject, referencePrefab);

            //a note that comparing the components themselves produced weird results
            //where two clearly unequal components were said to be equal
            Assert.That(
                newPrefab.GetComponent<MeshRenderer>().probeAnchor.gameObject,
                Is.Not.EqualTo(referenceObjectProbeAnchor));
            Assert.That(
                newPrefab.GetComponent<MeshRenderer>().probeAnchor.gameObject,
                Is.EqualTo(newPrefab.GetChildren().Single()));
        }

        //TODO: test object chains
    }
}
