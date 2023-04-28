
namespace EZUtils.Localization.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using NUnit.Framework;

    public partial class IntegrationTestAction
    {
        //integration tests will generate the other portion of this class
        //in future c# versions, we can generate partial methods (i believe unity 2021 would do the trick)
        //until then, we need reflection
        //
        //design-wise, we could make this a void method and add asserts to the generated ExecuteImpl
        //we currently return a list so that we can more easily write Asserts in a place we get intellisense
        //one interesting alternative design would be to hide cs file from unity in a folder, then
        //copy them to the gen folder as needed. currently opting to write all code here, at this risk of build errors.
        public IReadOnlyList<string> Execute()
        {
            MethodInfo implMethod = GetType().GetMethod("ExecuteImpl", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(implMethod, Is.Not.Null);
            Assert.That(implMethod.ReturnType, Is.EqualTo(typeof(IReadOnlyList<string>)));

            IReadOnlyList<string> result = (IReadOnlyList<string>)implMethod.Invoke(this, Array.Empty<object>());
            return result;
        }
    }
}
