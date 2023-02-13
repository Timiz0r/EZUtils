namespace EZUtils.RepackPrefab.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    //of course, unit tests should avoid side-effects such as asset creation
    //so consider these integration tests
    public class RepackPrefabTests
    {
        [TearDown]
        public void TearDown() => TestUtils.StandardTearDown();

        [Test]
        public void ReturnsVariantPrefab()
        {
            GameObject sourceObject = new ObjectBuilder("sourceObject")
                .GetObject();
            GameObject basePrefab = new ObjectBuilder("basePrefab")
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(sourceObject, basePrefab);

            Assert.That(PrefabUtility.IsPartOfVariantPrefab(newPrefab), Is.True);
        }

        [Test]
        public void Throws_WhenBasePrefabNotActuallyPrefab()
        {
            GameObject sourceObject = new ObjectBuilder("sourceObject")
                .GetObject();
            GameObject basePrefab = new ObjectBuilder("basePrefab")
                .GetObject();

            Assert.That(() => RepackPrefab.Repack(sourceObject, basePrefab), Throws.InvalidOperationException);
        }

        [Test]
        public void AddsObject_WhenNotInBasePrefab()
        {
            GameObject sourceObject = new ObjectBuilder("sourceObject")
                .AddObject("child", co => co.AddObject("child of child"))
                .GetObject();
            GameObject basePrefab = new ObjectBuilder("basePrefab")
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(sourceObject, basePrefab);

            GameObject child = newPrefab.GetChildren().Single();
            Assert.That(child, Has.Property("name").EqualTo("child"));
            Assert.That(child.transform.parent.gameObject, Is.Not.EqualTo(sourceObject.GetChildren().Single()));

            GameObject childOfChild = child.GetChildren().Single();
            Assert.That(childOfChild, Has.Property("name").EqualTo("child of child"));
            Assert.That(
                childOfChild.transform.parent.gameObject,
                Is.EqualTo(child));

            //from a normal scenve view, there are no additions, since they are applied to the prefab
            //from an internal perspective, there would indeed be two, though
            Assert.That(PrefabUtility.GetAddedGameObjects(newPrefab).Count, Is.EqualTo(0));
        }

        [Test]
        public void AddsPrefabInstance_WhenNotInBasePrefab()
        {
            //so one of the child objects of sourceObject is a prefab instance
            GameObject childObject = new GameObject("child");
            GameObject childObjectPrefab = TestUtils.CreatePrefab(childObject);
            GameObject sourceObject = new ObjectBuilder("sourceObject")
                .AddObject(childObject)
                .GetObject();
            GameObject basePrefab = new ObjectBuilder("basePrefab")
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(sourceObject, basePrefab);
            GameObject newPrefabChildObject = newPrefab.GetChildren().Single();

            Assert.That(
                PrefabUtility.GetCorrespondingObjectFromOriginalSource(newPrefabChildObject), Is.EqualTo(childObjectPrefab));
            Assert.That(PrefabUtility.GetAddedGameObjects(newPrefab).Count, Is.EqualTo(0));
        }

        [Test]
        public void AddsComponent_WhenNotInBasePrefab()
        {
            GameObject sourceObject = new ObjectBuilder("sourceObject")
                .AddComponent<BoxCollider>()
                .GetObject();
            GameObject basePrefab = new ObjectBuilder("basePrefab")
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(sourceObject, basePrefab);

            Assert.That(
                newPrefab.GetComponent<BoxCollider>(),
                Is.Not.Null.And.Not.EqualTo(sourceObject.GetComponent<BoxCollider>()));
            Assert.That(PrefabUtility.GetAddedComponents(newPrefab).Count, Is.EqualTo(0));
        }

        [Test]
        public void ChangesComponentValue_WhenInBoth()
        {
            GameObject sourceObject = new ObjectBuilder("sourceObject")
                .AddComponent<BoxCollider>(bc => bc.isTrigger = true)
                .GetObject();
            GameObject basePrefab = new ObjectBuilder("basePrefab")
                .AddComponent<BoxCollider>(bc => bc.isTrigger = false)
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(sourceObject, basePrefab);

            Assert.That(newPrefab.GetComponent<BoxCollider>().isTrigger, Is.True);
        }

        [Test]
        public void RemovesComponent_WhenOnlyInBasePrefab()
        {
            //aka source object has a removed child object, which cant be done in unity 2019 prefabs
            GameObject sourceObject = new ObjectBuilder("sourceObject")
                .GetObject();
            GameObject basePrefab = new ObjectBuilder("basePrefab")
                .AddObject("child", co => co
                    .AddComponent<BoxCollider>()
                    .AddObject("child of child", co2 => co2.AddComponent<CapsuleCollider>()))
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(sourceObject, basePrefab);

            GameObject child = newPrefab.GetChildren().Single();
            Assert.That(child.GetComponent<BoxCollider>(), Is.Null);

            Assert.That(child.GetChildren().Single().GetComponent<CapsuleCollider>(), Is.Null);
            Assert.That(PrefabUtility.GetRemovedComponents(newPrefab).Count, Is.EqualTo(0));
        }

        [Test]
        public void DoesNotReplaceObjectReferences_WhenOutsideBasePrefabObjectHierarchy()
        {
            GameObject externalProbeAnchor = new GameObject("obj outside reference obj");
            GameObject sourceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sourceObject.name = "sourceObject";
            sourceObject.GetComponent<MeshRenderer>().probeAnchor = externalProbeAnchor.transform;

            GameObject basePrefabObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePrefabObject.name = "basePrefab";
            GameObject basePrefab = TestUtils.CreatePrefab(basePrefabObject);

            GameObject newPrefab = RepackPrefab.Repack(sourceObject, basePrefab);

            Assert.That(
                newPrefab.GetComponent<MeshRenderer>().probeAnchor,
                Is.EqualTo(externalProbeAnchor.transform));
        }

        [Test]
        public void ReplacesObjectReferences_WhenInsideBasePrefabObjectHierarchy()
        {
            GameObject sourceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sourceObject.name = "sourceObject";
            GameObject sourceObjectProbeAnchor = new GameObject("probe anchor");
            sourceObjectProbeAnchor.transform.SetParent(sourceObject.transform);
            sourceObject.GetComponent<MeshRenderer>().probeAnchor = sourceObjectProbeAnchor.transform;

            GameObject basePrefabObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePrefabObject.name = "basePrefab";
            GameObject basePrefab = TestUtils.CreatePrefab(basePrefabObject);

            GameObject newPrefab = RepackPrefab.Repack(sourceObject, basePrefab);

            //a note that comparing the components themselves produced weird results
            //where two clearly unequal components were said to be equal
            Assert.That(
                newPrefab.GetComponent<MeshRenderer>().probeAnchor.gameObject,
                Is.Not.EqualTo(sourceObjectProbeAnchor));
            Assert.That(
                newPrefab.GetComponent<MeshRenderer>().probeAnchor.gameObject,
                Is.EqualTo(newPrefab.GetChildren().Single()));
        }

        [Test]
        public void CopiesOverTransformChanges()
        {
            //a current/previous implementation has/had special handling of transform component
            GameObject sourceObject = new ObjectBuilder("sourceObject")
                .GetObject();
            sourceObject.transform.position = new Vector3(100, 200, 300);
            GameObject basePrefab = new ObjectBuilder("basePrefab")
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(sourceObject, basePrefab);

            Assert.That(newPrefab.transform.position, Is.EqualTo(sourceObject.transform.position));
        }
    }
}
