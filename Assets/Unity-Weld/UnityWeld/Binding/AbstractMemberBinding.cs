using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityWeld.Binding.Exceptions;
using UnityWeld.Binding.Internal;

namespace UnityWeld.Binding
{
    /// <summary>
    /// Base class for binders to Unity MonoBehaviours.
    /// </summary>
    [HelpURL("https://github.com/Real-Serious-Games/Unity-Weld")]
    public abstract class AbstractMemberBinding : MonoBehaviour, IMemberBinding
    {
        /// <summary>
        /// Initialise this binding. Used when we first start the scene.
        /// Detaches any attached view models, finds available view models afresh and then connects the binding.
        /// </summary>
        public virtual void Init()
        {
            Disconnect();

            Connect();
        }

        protected void Reset()
        {
            InitViewModelsDic();
        }

        protected static Dictionary<string, object> viewModels;

        /// <summary>
        /// Store the viewModels in current scene
        /// </summary>
        public static Dictionary<string, object> ViewModels
        {
            get
            {
                if (viewModels != null)
                    return viewModels;

                viewModels = new Dictionary<string, object>();
                InitViewModelsDic();
                return viewModels;
            }
        }

        /// <summary>
        /// Find the viewModels in current scene
        /// </summary>
#if UNITY_EDITOR
        [MenuItem("Tools/InitViewModelsDic")]
#endif
        public static void InitViewModelsDic()
        {
            viewModels ??= new Dictionary<string, object>();
            viewModels.Clear();
            IEnumerable<Transform> transforms =
                GameObject.FindObjectsOfType<GameObject>(true).Select(obj => obj.transform);
            foreach (var trans in transforms)
            {
                var components = trans.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    if (component == null)
                    {
                        continue;
                    }

                    // Case where a ViewModelBinding is used to bind a non-MonoBehaviour class.
                    var viewModelBinding = component as IViewModelProvider;
                    if (viewModelBinding != null)
                    {
                        var viewModelTypeName = viewModelBinding.GetViewModelTypeName();
                        // Ignore view model bindings that haven't been set up yet.
                        if (string.IsNullOrEmpty(viewModelTypeName))
                        {
                            continue;
                        }

                        string name = viewModelTypeName;
                        if (!viewModels.ContainsKey(name))
                            viewModels.Add(name, viewModelBinding.GetViewModel());
                        // Debug.Log(name);
                    }
                    else if (component.GetType().GetCustomAttributes(typeof(BindingAttribute), false).Any())
                    {
                        // Case where we are binding to an existing MonoBehaviour.
                        string name = component.GetType().FullName;
                        if (!viewModels.ContainsKey(name))
                            viewModels.Add(name, component);
                        // Debug.Log(name);
                    }
                }
            }
        }

        /// <summary>
        /// find a view model that corresponds to the specified name.
        /// </summary>
        private object FindViewModel(string viewModelName)
        {
            if (ViewModels.TryGetValue(viewModelName, out var viewModel))
            {
                return viewModel;
            }

            throw new ViewModelNotFoundException(string.Format(
                "Tried to get view model {0} but it could not be found on "
                + "object {1}. Check that a ViewModelBinding for that view model exists further up in "
                + "the scene hierarchy. ", viewModelName, gameObject.name)
            );
        }

        /// <summary>
        /// Find the type of the adapter with the specified name and create it.
        /// </summary>
        protected static IAdapter CreateAdapter(string adapterTypeName)
        {
            if (string.IsNullOrEmpty(adapterTypeName))
            {
                return null;
            }

            var adapterType = TypeResolver.FindAdapterType(adapterTypeName);
            if (adapterType == null)
            {
                throw new NoSuchAdapterException(adapterTypeName);
            }

            if (!typeof(IAdapter).IsAssignableFrom(adapterType))
            {
                throw new InvalidAdapterException(string.Format(
                    "Type '{0}' does not implement IAdapter and cannot be used as an adapter.", adapterTypeName));
            }

            return (IAdapter)Activator.CreateInstance(adapterType);
        }

        /// <summary>
        /// Make a property end point for a property on the view model.
        /// </summary>
        protected PropertyEndPoint MakeViewModelEndPoint(string viewModelPropertyName, string adapterTypeName,
            AdapterOptions adapterOptions)
        {
            string propertyName;
            object viewModel;
            ParseViewModelEndPointReference(viewModelPropertyName, out propertyName, out viewModel);

            var adapter = CreateAdapter(adapterTypeName);

            return new PropertyEndPoint(viewModel, propertyName, adapter, adapterOptions, "view-model", this);
        }

        /// <summary>
        /// Parse an end-point reference including a type name and member name separated by a period.
        /// </summary>
        protected static void ParseEndPointReference(string endPointReference, out string memberName,
            out string typeName)
        {
            var lastPeriodIndex = endPointReference.LastIndexOf('.');
            if (lastPeriodIndex == -1)
            {
                throw new InvalidEndPointException(
                    "No period was found, expected end-point reference in the following format: <type-name>.<member-name>. " +
                    "Provided end-point reference: " + endPointReference
                );
            }

            typeName = endPointReference.Substring(0, lastPeriodIndex);
            memberName = endPointReference.Substring(lastPeriodIndex + 1);
            //Due to (undocumented) unity behaviour, some of their components do not work with the namespace when using GetComponent(""), and all of them work without the namespace
            //So to be safe, we remove all namespaces from any component that starts with UnityEngine
            if (typeName.StartsWith("UnityEngine."))
            {
                typeName = typeName.Substring(typeName.LastIndexOf('.') + 1);
            }

            if (typeName.Length == 0 || memberName.Length == 0)
            {
                throw new InvalidEndPointException(
                    "Bad format for end-point reference, expected the following format: <type-name>.<member-name>. " +
                    "Provided end-point reference: " + endPointReference
                );
            }
        }

        /// <summary>
        /// Parse an end-point reference and search up the hierarchy for the named view-model.
        /// </summary>
        protected void ParseViewModelEndPointReference(string endPointReference, out string memberName,
            out object viewModel)
        {
            string viewModelName;
            ParseEndPointReference(endPointReference, out memberName, out viewModelName);

            viewModel = FindViewModel(viewModelName);
            if (viewModel == null)
            {
                throw new ViewModelNotFoundException("Failed to find view-model in hierarchy: " + viewModelName);
            }
        }

        /// <summary>
        /// Parse an end-point reference and get the component for the view.
        /// </summary>
        protected void ParseViewEndPointReference(string endPointReference, out string memberName, out Component view)
        {
            string boundComponentType;
            ParseEndPointReference(endPointReference, out memberName, out boundComponentType);

            view = GetComponent(boundComponentType);
            if (view == null)
            {
                throw new ComponentNotFoundException("Failed to find component on current game object: " +
                                                     boundComponentType);
            }
        }

        /// <summary>
        /// Connect to all the attached view models
        /// </summary>
        public abstract void Connect();

        /// <summary>
        /// Disconnect from all attached view models.
        /// </summary>
        public abstract void Disconnect();

        protected void Awake()
        {
            Init();
        }

        /// <summary>
        /// Clean up when the game object is destroyed.
        /// </summary>
        public void OnDestroy()
        {
            Disconnect();
        }
    }
}