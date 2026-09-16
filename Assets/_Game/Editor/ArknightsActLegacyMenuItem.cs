#if UNITY_EDITOR
using System;

// The project accumulated many temporary editor MenuItem attributes while the prototype was being
// built. Keep those utility methods callable from code, but make their old attributes inert so Unity
// does not register dozens of obsolete ArknightsACT menu commands after every domain reload.
//
// The production menu uses explicitly-qualified [UnityEditor.MenuItem] attributes in
// ArknightsActMenuCleanup.cs, so only the small supported workflow remains visible.
namespace ArknightsACT.Editor
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    internal sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
    }
}

namespace ArknightsACT.Editor.PRTS
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    internal sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
    }
}
#endif