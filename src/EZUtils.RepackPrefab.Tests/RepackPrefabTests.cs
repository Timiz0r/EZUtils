namespace EZUtils.RepackPrefab.Tests
{
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
            GameObject referenceObject = new ObjectBuilder("root")
                .GetObject();
            GameObject referencePrefab = new ObjectBuilder("root")
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(referenceObject, referencePrefab);
            Assert.That(PrefabUtility.IsPartOfVariantPrefab(newPrefab), Is.True);
        }

        [Test]
        public void AddsObject_WhenNotInPrefab()
        {
            GameObject referenceObject = new ObjectBuilder("root")
                .AddObject("child")
                .GetObject();
            GameObject referencePrefab = new ObjectBuilder("root")
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(referenceObject, referencePrefab);
            Assert.That(newPrefab.GetChildren(), Has.Exactly(1).With.Property("name").EqualTo("child"));
        }

        [Test]
        public void AddsComponent_WhenNotInPrefab()
        {
            GameObject referenceObject = new ObjectBuilder("root")
                .AddComponent<BoxCollider>()
                .GetObject();
            GameObject referencePrefab = new ObjectBuilder("root")
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(referenceObject, referencePrefab);
            Assert.That(newPrefab.GetComponent<BoxCollider>(), Is.Not.Null);
        }

        [Test]
        public void RemovesComponent_WhenOnlyInPrefab()
        {
            GameObject referenceObject = new ObjectBuilder("root")
                .GetObject();
            GameObject referencePrefab = new ObjectBuilder("root")
                .AddComponent<BoxCollider>()
                .CreatePrefab();

            GameObject newPrefab = RepackPrefab.Repack(referenceObject, referencePrefab);
            Assert.That(newPrefab.GetComponent<BoxCollider>(), Is.Null);
        }
    }
}
