﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityWeld.Binding.Exceptions;

namespace UnityWeld.Binding.Internal
{
    /// <summary>
    /// Helper class for setting up the factory for use in the editor.
    /// </summary>
    public static class TypeResolver
    {
        private static Type[] typesWithBindingAttribute;

        public static IEnumerable<Type> TypesWithBindingAttribute
        {
            get
            {
                if (typesWithBindingAttribute == null)
                {
                    typesWithBindingAttribute = FindTypesMarkedByAttribute(typeof(BindingAttribute));
                }

                return typesWithBindingAttribute;
            }
        }

        private static Type[] typesWithAdapterAttribute;

        public static IEnumerable<Type> TypesWithAdapterAttribute
        {
            get
            {
                if (typesWithAdapterAttribute == null)
                {
                    typesWithAdapterAttribute = FindTypesMarkedByAttribute(typeof(AdapterAttribute));
                }

                return typesWithAdapterAttribute;
            }
        }

        /// <summary>
        /// Find all types marked with the specified attribute.
        /// </summary>
        private static Type[] FindTypesMarkedByAttribute(Type attributeType)
        {
            var typesFound = new List<Type>();

            foreach (var type in GetAllTypes())
            {
                try
                {
                    if (type.GetCustomAttributes(attributeType, false).Any())
                    {
                        typesFound.Add(type);
                    }
                }
                catch (Exception)
                {
                    // Some types throw an exception when we try to use reflection on them.
                }
            }

            return typesFound.ToArray();
        }

        /// <summary>
        /// Returns an enumerable of all known types.
        /// </summary>
        private static IEnumerable<Type> GetAllTypes()
        {
            var assemblies =
                AppDomain.CurrentDomain.GetAssemblies()
                    // Automatically exclude the Unity assemblies, which throw exceptions when we try to access them.
                    .Where(a =>
                        !a.FullName.StartsWith("UnityEngine") &&
                        !a.FullName.StartsWith("UnityEditor"));

            foreach (var assembly in assemblies)
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (Exception)
                {
                    // Ignore assemblies that can't be loaded.
                    continue;
                }

                foreach (var type in types)
                {
                    yield return type;
                }
            }
        }

        /// <summary>
        /// Find a particular type by its short name.
        /// </summary>
        public static Type FindAdapterType(string typeName)
        {
            var matchingTypes = TypesWithAdapterAttribute
                .Where(type => type.ToString() == typeName)
                .ToList();

            if (!matchingTypes.Any())
            {
                return null;
            }

            if (matchingTypes.Skip(1).Any())
            {
                throw new AmbiguousTypeException("Multiple types match: " + typeName);
            }

            return matchingTypes.First();
        }

        /// <summary>
        /// Find the [Adapter] attribute for a particular type.
        /// Returns null if there is no such attribute.
        /// </summary>
        public static AdapterAttribute FindAdapterAttribute(Type adapterType)
        {
            return adapterType
                .GetCustomAttributes(typeof(AdapterAttribute), false)
                .Cast<AdapterAttribute>()
                .FirstOrDefault();
        }

        /// <summary>
        /// Return the type of a view model bound by an IViewModelBinding
        /// </summary>
        private static Type GetViewModelType(string viewModelTypeName)
        {
            var type = TypesWithBindingAttribute
                .FirstOrDefault(t => t.ToString() == viewModelTypeName);

            if (type == null)
            {
                throw new ViewModelNotFoundException("Could not find the specified view model \"" + viewModelTypeName + "\"");
            }

            return type;
        }

        /// <summary>
        /// Scan up the hierarchy and find all the types that can be bound to 
        /// a specified MemberBinding.
        /// </summary>
        private static IEnumerable<Type> FindAvailableViewModelTypes(AbstractMemberBinding memberBinding)
        {
            var foundAtLeastOneBinding = AbstractMemberBinding.ViewModels.Count!=0;
            if (foundAtLeastOneBinding)
            {
                foreach (var viewModel in AbstractMemberBinding.ViewModels)
                {
                    yield return viewModel.Value.GetType();
                }
            }

            if (!foundAtLeastOneBinding)
            {
                Debug.LogError("UI binding " + memberBinding.gameObject.name + " must be placed underneath at least one bindable component.", memberBinding);
            }
        }

        /// <summary>
        /// Find bindable properties in available view models.
        /// </summary>
        public static BindableMember<PropertyInfo>[] FindBindableProperties(AbstractMemberBinding target)
        {
            return FindAvailableViewModelTypes(target)
                .SelectMany(type => GetPublicProperties(type)
                    .Select(p => new BindableMember<PropertyInfo>(p, type))
                )
                .Where(p => p.Member
                    .GetCustomAttributes(typeof(BindingAttribute), false)
                    .Any() // Filter out properties that don't have [Binding].
                )
                .ToArray();
        }

        /// <summary>
        /// Get all the declared and inherited public properties from a class or interface.
        ///
        /// https://stackoverflow.com/questions/358835/getproperties-to-return-all-properties-for-an-interface-inheritance-hierarchy#answer-26766221
        /// </summary>
        private static IEnumerable<PropertyInfo> GetPublicProperties(Type type)
        {
            if (!type.IsInterface)
            {
                return type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            }

            return (new[] { type })
                .Concat(type.GetInterfaces())
                .SelectMany(i => i.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        }

        /// <summary>
        /// Get all the declared and inherited public methods from a class or interface.
        /// </summary>
        private static IEnumerable<MethodInfo> GetPublicMethods(Type type)
        {
            if (!type.IsInterface)
            {
                return type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            }

            return (new[] { type })
                .Concat(type.GetInterfaces())
                .SelectMany(i => i.GetMethods(BindingFlags.Public | BindingFlags.Instance));
        }

        /// <summary>
        /// Get a list of methods in the view model that we can bind to.
        /// </summary>
        public static BindableMember<MethodInfo>[] FindBindableMethods(EventBinding targetScript)
        {
            return FindAvailableViewModelTypes(targetScript)
                .SelectMany(type => GetPublicMethods(type)
                    .Select(m => new BindableMember<MethodInfo>(m, type))
                )
                .Where(m => m.Member.GetParameters().Length == 0)
                .Where(m => m.Member.GetCustomAttributes(typeof(BindingAttribute), false).Any() 
                    && !m.MemberName.StartsWith("get_")) // Exclude property getters, since we aren't doing anything with the return value of the bound method anyway.
                .ToArray();
        }

        /// <summary>
        /// Find collection properties that can be data-bound.
        /// </summary>
        public static BindableMember<PropertyInfo>[] FindBindableCollectionProperties(CollectionBinding target)
        {
            return FindBindableProperties(target)
                .Where(p => typeof(IEnumerable).IsAssignableFrom(p.Member.PropertyType))
                .Where(p => !typeof(string).IsAssignableFrom(p.Member.PropertyType))
                .ToArray();
        }
    }
}
