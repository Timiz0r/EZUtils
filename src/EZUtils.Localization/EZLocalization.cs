namespace EZUtils.Localization
{
    using System;
    using System.Globalization;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine.UIElements;

    //ports-and-adapters-wise, EZLocalization is a driver adapter connecting unity editor to a catalog
    public class EZLocalization : IDisposable
    {
        private readonly string root;
        private readonly Locale nativeLocale;
        private readonly CatalogLocaleSynchronizer synchronizer;
        private readonly LocalizedMenuContainer localizedMenuContainer;

        private CatalogReference catalogReference;
        private bool disposedValue;

        private EZLocalization(string root, Locale nativeLocale, CatalogLocaleSynchronizer synchronizer)
        {
            this.root = root;
            this.nativeLocale = nativeLocale;
            this.synchronizer = synchronizer;
            localizedMenuContainer = new LocalizedMenuContainer(this);

            //at time of writing, the only reason to force initialization is for menus
            //we could instead limit to adding this to the first invocation of AddMenu,
            //but this perhaps vilates the principal of least surprise the least
            EditorApplication.delayCall += Initialize;
        }

        public static EZLocalization ForCatalogUnder(string root, string localeSynchronizationKey)
            => ForCatalogUnder(root, localeSynchronizationKey, Locale.English);

        public static EZLocalization ForCatalogUnder(string root, string localeSynchronizationKey, Locale nativeLocale)
        {
            CatalogLocaleSynchronizer synchronizer = CatalogLocaleSynchronizer.Get(localeSynchronizationKey, nativeLocale);
            EZLocalization result = new EZLocalization(root, nativeLocale, synchronizer);

            return result;
        }

        //in general, we can't use catalog reference or synchronizer in static contexts/invoked from cctors
        //since we usually instantiate EZLocalization in cctors, we need to delay
        private void Initialize()
        {
            if (catalogReference != null) return;

            catalogReference = new CatalogReference(root, nativeLocale);
            //for order of initialization reasons, this must come after synchronizer registration
            catalogReference.Initialize();
            synchronizer.Register(catalogReference);
            catalogReference.TrackRetranslatable(localizedMenuContainer);
        }

        public void SelectLocale(Locale locale)
        {
            Initialize();
            synchronizer.SelectLocale(locale);
        }

        public Locale SelectLocale(CultureInfo cultureInfo)
        {
            Initialize();
            return synchronizer.SelectLocale(cultureInfo);
        }

        public bool TrySelectLocale(Locale locale)
        {
            Initialize();
            return synchronizer.TrySelectLocale(locale);
        }

        public bool TrySelectLocale(CultureInfo cultureInfo, out Locale correspondingLocale)
        {
            Initialize();
            return synchronizer.TrySelectLocale(cultureInfo, out correspondingLocale);
        }

        public bool TrySelectLocale(CultureInfo cultureInfo)
        {
            Initialize();
            return TrySelectLocale(cultureInfo, out _);
        }

        public Locale SelectLocaleOrNative(params Locale[] locales)
        {
            Initialize();
            return synchronizer.SelectLocaleOrNative(locales);
        }

        public Locale SelectLocaleOrNative(params CultureInfo[] cultureInfos)
        {
            Initialize();
            return synchronizer.SelectLocaleOrNative(cultureInfos);
        }

        public Locale SelectLocaleOrNative() => SelectLocaleOrNative(Array.Empty<Locale>());

        //while we support custom retranslation via IRetranslatable, the most recommended way to support retranslation
        //for elements created in-code is to call EZLocalization.TranslateElementTree for the newly created element.
        //but for non-TextElements and non-BaseField`1s, we have IRetranslatable.
        //TODO: plural implementation? kinda hard to do since this is recursive.
        //leaving this implemention for when we decide how the numeric value gets updated
        //perhaps we need to exclude plural ones in the recursive one, and have the plural overload not be recursive
        //still gotta decide how to get the value inside. easiest is {0}
        //but ofc we have plural forms to consider, so...
        //locplural:one/id:type2:type3:type4
        //and which one each maps to depends on native locale
        //and will need to escape : with ::, ofc only if a localplural: prefix
        public void TranslateElementTree(VisualElement rootElement)
        {
            Initialize();
            //descendents includes element, as well
            rootElement.Query().Descendents<VisualElement>().ForEach(element =>
            {
                if (element is IRetranslatable retranslatable)
                {
                    //making this first so that we can override default behavior if necessary
                    catalogReference.TrackRetranslatable(retranslatable);
                }
                else if (element is TextElement textElement
                    && textElement.text is string teOriginalValue
                    && teOriginalValue.StartsWith("loc:", StringComparison.Ordinal))
                {
                    teOriginalValue = teOriginalValue.Substring(4);
                    catalogReference.TrackRetranslatable(textElement, () => textElement.text = T(teOriginalValue));
                }
                else if (element.GetType() is Type elementType
                    && InheritsFromGenericType(elementType.GetType(), typeof(BaseField<>))
                    && elementType.GetProperty("label", BindingFlags.Public | BindingFlags.Instance) is PropertyInfo labelProperty
                    && labelProperty.GetValue(element) is string lbOriginalValue
                    && lbOriginalValue.StartsWith("loc:", StringComparison.Ordinal))
                {
                    lbOriginalValue = lbOriginalValue.Substring(4);
                    catalogReference.TrackRetranslatable(element, () => labelProperty.SetValue(element, T(lbOriginalValue)));
                }
            });
        }
        public void TrackRetranslatable(IRetranslatable retranslatable)
            => catalogReference.TrackRetranslatable(retranslatable);

        /// <remarks>Window title translations should be added in CreateGUI due to Unity restrictions.</remarks>
        [LocalizationMethod]
        public void TranslateWindowTitle(
            EditorWindow window,
            [LocalizationParameter(LocalizationParameter.Id)] string titleText)
        {
            Initialize();
            catalogReference.TrackRetranslatable(window, () => window.titleContent.text = T(titleText));
        }
        //LocalizedMenuContainer supports adding menus before initialization is done
        //and initialization add them later
        //so no need to Initialize here
        [LocalizationMethod]
        public void AddMenu(
            [LocalizationParameter(LocalizationParameter.Id)] string name,
            int priority,
            Action action)
            => localizedMenuContainer.AddMenu(name, priority, action);
        [LocalizationMethod]
        public void AddMenu(
            [LocalizationParameter(LocalizationParameter.Id)] string name,
            int priority,
            Action action,
            Func<bool> validate)
            => localizedMenuContainer.AddMenu(name, priority, action, validate);
        [LocalizationMethod]
        public void AddComponentMenu<T>([LocalizationParameter(LocalizationParameter.Id)] string name, int priority)
            where T : UnityEngine.Component
            => localizedMenuContainer.AddMenu(
                "Component/" + name,
                priority,
                () => AddComponent<T>(),
                () => ValidateAddComponent());

        [LocalizationMethod]
        public string T(RawString id)
        {
            Initialize();
            return catalogReference.Catalog.T(id);
        }

        [LocalizationMethod]
        public string T(FormattableString id)
        {
            Initialize();
            return catalogReference.Catalog.T(id);
        }

        [LocalizationMethod]
        public string T(string context, RawString id)
        {
            Initialize();
            return catalogReference.Catalog.T(context, id);
        }

        [LocalizationMethod]
        public string T(string context, FormattableString id)
        {
            Initialize();
            return catalogReference.Catalog.T(context, id);
        }

        [LocalizationMethod]
        public string T(
            FormattableString id,
            decimal count,
            FormattableString other) => T(
                context: default,
                id: id,
                count: count,
                other: other,
                zero: default,
                two: default,
                few: default,
                many: default);

        [LocalizationMethod]
        public string T(
                    string context,
                    FormattableString id,
                    decimal count,
                    FormattableString other)
        {
            Initialize();
            return catalogReference.Catalog.T(
                        context: context,
                        id: id,
                        count: count,
                        other: other,
                        zero: default,
                        two: default,
                        few: default,
                        many: default);
        }

        [LocalizationMethod]
        public string T(
                    FormattableString id,
                    decimal count,
                    FormattableString other,
                    FormattableString zero = default,
                    FormattableString two = default,
                    FormattableString few = default,
                    FormattableString many = default)
        {
            Initialize();
            return catalogReference.Catalog.T(
                        id: id,
                        count: count,
                        other: other,
                        zero: zero,
                        two: two,
                        few: few,
                        many: many);
        }

        [LocalizationMethod]
        public string T(
                    string context,
                    FormattableString id,
                    decimal count,
                    FormattableString other,
                    FormattableString zero = default,
                    FormattableString two = default,
                    FormattableString few = default,
                    FormattableString many = default)
        {
            Initialize();
            return catalogReference.Catalog.T(
                        context: context,
                        id: id,
                        count: count,
                        other: other,
                        zero: zero,
                        two: two,
                        few: few,
                        many: many);
        }

        private static bool InheritsFromGenericType(Type typeToInspect, Type genericTypeDefinition)
        {
            if (typeToInspect == null) return false;
#pragma warning disable IDE0046 // Convert to conditional expression; prefer it here
            if (typeToInspect.IsGenericType
                && typeToInspect.GetGenericTypeDefinition() is Type genericType
                && genericType == genericTypeDefinition) return true;
#pragma warning restore IDE0046 // Convert to conditional expression
            return InheritsFromGenericType(typeToInspect.BaseType, genericTypeDefinition);
        }

        private static void AddComponent<T>() where T : UnityEngine.Component
        {
            foreach (UnityEngine.GameObject go in Selection.gameObjects)
            {
                _ = go.AddComponent<T>();
            }
        }
        private static bool ValidateAddComponent() => Selection.gameObjects.Length > 0;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                catalogReference.Dispose();

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
