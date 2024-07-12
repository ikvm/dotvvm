using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Compilation.Parser.Dothtml.Parser;

namespace DotVVM.Framework.Compilation.ControlTree
{
    public static class ControlTreeHelper
    {
        public static bool HasEmptyContent(this IAbstractControl control)
            => control.Content.All(c => DothtmlNodeHelper.IsEmpty(c.DothtmlNode)); // allow only whitespace literals

        public static bool HasProperty(this IAbstractControl control, IPropertyDescriptor property)
        {
            return control.TryGetProperty(property, out _);
        }

        public static bool HasPropertyValue(this IAbstractControl control, IPropertyDescriptor property)
        {
            return control.TryGetProperty(property, out var setter) && setter is IAbstractPropertyValue;
        }

        public static IAbstractPropertySetter? GetHtmlAttribute(this IAbstractControl control, string memberName) =>
            GetPropertyGroupMember(control, "", memberName);
        public static IAbstractPropertySetter? GetPropertyGroupMember(this IAbstractControl control, string prefix, string memberName)
        {
            control.TryGetProperty(control.Metadata.GetPropertyGroupMember(prefix, memberName), out var value);
            return value;
        }

        public static Dictionary<string, IAbstractPropertySetter> GetPropertyGroup(this IAbstractControl control, IPropertyGroupDescriptor group)
        {
            var result = new Dictionary<string, IAbstractPropertySetter>();
            foreach (var prop in control.Properties)
            {
                if (prop.Key is IGroupedPropertyDescriptor member && member.PropertyGroup == group)
                {
                    result.Add(member.GroupMemberName, prop.Value);
                }
            }
            return result;
        }

        public static IPropertyDescriptor GetHtmlAttributeDescriptor(this IControlResolverMetadata metadata, string name)
            => metadata.GetPropertyGroupMember("", name);
        public static IPropertyDescriptor GetPropertyGroupMember(this IControlResolverMetadata metadata, string prefix, string name)
        {
            var group = metadata.PropertyGroups.FirstOrDefault(f => f.Prefix == prefix).PropertyGroup;
            if (group == null) throw new NotSupportedException($"Control { metadata.Type.CSharpName } does not support property group with prefix '{prefix}'.");
            return group.GetDotvvmProperty(name);
        }
    }
}
