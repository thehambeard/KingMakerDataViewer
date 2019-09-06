using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static ModMaker.Utils.ReflectionCache;

namespace DataViewer.Utils.ReflectionTree
{
    public class Tree : Tree<object>
    {
        public Tree(object target) : base(target) { }
    }

    public class Tree<TTarget>
    {
        public RootNode<TTarget> Root { get; private set; }

        public Tree(TTarget target)
        {
            SetTarget(target);
        }

        public void SetTarget(TTarget target)
        {
            if (Root != null)
                Root.SetTarget(target);
            else
                Root = new RootNode<TTarget>("<Root>", target);
        }
    }

    public abstract class BaseNode
    {
        protected const BindingFlags ALL_FLAGS = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        protected static readonly HashSet<Type> BASE_TYPES = new HashSet<Type>()
        {
            //typeof(object),
            typeof(DBNull),
            typeof(bool),
            typeof(char),
            typeof(sbyte),
            typeof(byte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal),
            //typeof(DateTime),
            typeof(string),
            typeof(IntPtr),
            typeof(UIntPtr)
        };

        protected bool? _isEnumerable;

        protected Dictionary<string, Type> _fieldTypes;
        protected Dictionary<string, Type> _propertyTypes;

        protected bool _fieldTypesIsDirty = true;
        protected bool _propertyTypesIsDirty = true;

        protected List<BaseNode> _componentNodes;
        protected List<BaseNode> _enumNodes;
        protected List<BaseNode> _fieldNodes;
        protected List<BaseNode> _propertyNodes;

        protected bool _componentIsDirty = true;
        protected bool _enumIsDirty = true;
        protected bool _fieldIsDirty = true;
        protected bool _propertyIsDirty = true;

        public int CustomFlags { get; set; }

        public Type InstType { get; protected set; }

        public Type Type { get; }

        public string Name { get; protected set; }

        public abstract string ValueText { get; }

        public bool IsBaseType => InstType != null && BASE_TYPES.Contains(Nullable.GetUnderlyingType(InstType) ?? InstType);

        public bool IsGameObject => typeof(GameObject).IsAssignableFrom(Type) || (InstType != null && typeof(GameObject).IsAssignableFrom(InstType));

        public bool IsEnumerable {
            get {
                if (!_isEnumerable.HasValue)
                    _isEnumerable = InstType != null && !IsBaseType && InstType.GetInterfaces().Contains(typeof(IEnumerable));
                return _isEnumerable.Value;
            }
        }

        public bool IsException { get; protected set; }

        public abstract bool IsNull { get; }

        public virtual bool IsChildComponent => false;

        public virtual bool IsEnumItem => false;

        public virtual bool IsField => false;

        public virtual bool IsProperty => false;

        public bool IsNullable => Type.IsGenericType && !Type.IsGenericTypeDefinition && Type.GetGenericTypeDefinition() == typeof(Nullable<>);

        protected BaseNode(Type type)
        {
            Type = type;
            if (type.IsValueType && !IsNullable)
                InstType = type;
        }

        public abstract ReadOnlyDictionary<string, Type> GetFieldTypes();

        public abstract ReadOnlyDictionary<string, Type> GetPropertyTypes();

        public abstract ReadOnlyCollection<BaseNode> GetComponentNodes();

        public abstract ReadOnlyCollection<BaseNode> GetEnumNodes();

        public abstract ReadOnlyCollection<BaseNode> GetFieldNodes();

        public abstract ReadOnlyCollection<BaseNode> GetPropertyNodes();

        internal abstract void SetTarget(object target);

        public virtual void UpdateValue() { }
    }

    public abstract class GenericNode<TNode> : BaseNode
    {
        protected TNode _value;

        public virtual TNode Value {
            get {
                return _value;
            }
            protected set {
                if (value == null)
                {
                    if (_value != null)
                    {
                        InstType = null;
                        _fieldTypesIsDirty = true;
                        _propertyTypesIsDirty = true;
                        _fieldIsDirty = true;
                        _propertyIsDirty = true;
                        _isEnumerable = null;
                        _enumIsDirty = true;
                    }
                }
                else
                {
                    if (!value.Equals(_value))
                    {
                        // value changed
                        if (Type.IsSealed)
                        {
                            InstType = Type;
                            //InstType = value.GetType();
                        }
                        else
                        {
                            Type oldInstType = InstType;
                            InstType = value.GetType();
                            if (InstType != oldInstType)
                            {
                                // type changed
                                _fieldTypesIsDirty = true;
                                _propertyTypesIsDirty = true;
                                _fieldIsDirty = true;
                                _propertyIsDirty = true;
                                _isEnumerable = null;
                            }
                        }
                    }
                }
                // component / enum elements 'may' changed
                _componentIsDirty = true;
                _enumIsDirty = true;
                _value = value;
            }
        }

        public override string ValueText => IsException ? "<exception>" : IsNull ? "<null>" : Value.ToString();

        public override bool IsNull =>  Value == null;

        protected GenericNode() : base(typeof(TNode)) { }

        public override ReadOnlyDictionary<string, Type> GetFieldTypes()
        {
            UpdateFieldTypes();
            return new ReadOnlyDictionary<string, Type>(_fieldTypes);
        }

        public override ReadOnlyDictionary<string, Type> GetPropertyTypes()
        {
            UpdatePropertyTypes();
            return new ReadOnlyDictionary<string, Type>(_propertyTypes);
        }

        public override ReadOnlyCollection<BaseNode> GetComponentNodes()
        {
            UpdateComponentNodes();
            return _componentNodes.AsReadOnly();
        }

        public override ReadOnlyCollection<BaseNode> GetEnumNodes()
        {
            UpdateEnumNodes();
            return _enumNodes.AsReadOnly();
        }

        public override ReadOnlyCollection<BaseNode> GetFieldNodes()
        {
            UpdateFieldNodes();
            return _fieldNodes.AsReadOnly();
        }

        public override ReadOnlyCollection<BaseNode> GetPropertyNodes()
        {
            UpdatePropertyNodes();
            return _propertyNodes.AsReadOnly();
        }

        internal override void SetTarget(object target)
        {
            throw new NotImplementedException();
        }

        private void UpdateFieldTypes(bool rebuild = false)
        {
            if (!(rebuild || _fieldTypesIsDirty))
                return;

            _fieldTypesIsDirty = false;

            if (_fieldTypes == null)
                _fieldTypes = new Dictionary<string, Type>();
            else
                _fieldTypes.Clear();

            if (IsException || IsNull || IsBaseType)
                return;

            foreach (FieldInfo field in (Nullable.GetUnderlyingType(InstType) ?? InstType).GetFields(ALL_FLAGS))
            {
                if (!field.IsStatic &&
                    !field.Name.StartsWith("<") &&
                    !_fieldTypes.ContainsKey(field.Name))
                    _fieldTypes[field.Name] = field.FieldType;
            }
        }

        private void UpdatePropertyTypes(bool rebuild = false)
        {
            if (!(rebuild || _propertyTypesIsDirty))
                return;

            _propertyTypesIsDirty = false;

            if (_propertyTypes == null)
                _propertyTypes = new Dictionary<string, Type>();
            else
                _propertyTypes.Clear();

            if (IsException || IsNull || IsBaseType)
                return;

            foreach (PropertyInfo property in (Nullable.GetUnderlyingType(InstType) ?? InstType).GetProperties(ALL_FLAGS))
            {
                if (property.GetMethod != null &&
                    !property.GetMethod.IsStatic &&
                    property.GetMethod.GetParameters().Length == 0 &&
                    !_propertyTypes.ContainsKey(property.Name))
                    _propertyTypes[property.Name] = property.PropertyType;
            }
        }

        private void UpdateComponentNodes()
        {
            if (!_componentIsDirty)
                return;

            _componentIsDirty = false;

            if (_componentNodes == null)
                _componentNodes = new List<BaseNode>();

            if (IsException || IsNull || !IsGameObject)
            {
                _componentNodes.Clear();
                return;
            }

            Type nodeType = typeof(ChildComponentNode<Component>);
            int i = 0;
            foreach (Component item in (Value as GameObject).GetComponents<Component>())
            {
                if (i < _componentNodes.Count)
                {
                    _componentNodes[i].SetTarget(item);
                }
                else
                {
                    _componentNodes.Add(Activator.CreateInstance(
                        nodeType, ALL_FLAGS, null, new object[] { "<component_" + i + ">", item }, null) as BaseNode);
                }
                i++;
            }
            if (i < _componentNodes.Count)
                _componentNodes.RemoveRange(i, _componentNodes.Count - i);
        }

        private void UpdateEnumNodes()
        {
            if (!_enumIsDirty)
                return;

            _enumIsDirty = false;

            if (_enumNodes == null)
                _enumNodes = new List<BaseNode>();

            if (IsException || IsNull || !IsEnumerable)
            {
                _enumNodes.Clear();
                return;
            }

            IEnumerable<Type> enumGenericArg = InstType.GetInterfaces()
                .Where(item => item.IsGenericType && item.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .Select(item => item.GetGenericArguments()[0]);

            Type itemType;
            if (enumGenericArg.Count() != 1)
                itemType = typeof(object);
            else
                itemType = enumGenericArg.First();

            Type nodeType = typeof(ItemOfEnumNode<>).MakeGenericType(itemType);
            int i = 0;
            foreach (object item in Value as IEnumerable)
            {
                if (i < _enumNodes.Count)
                {
                    if (_enumNodes[i].Type == itemType)
                        _enumNodes[i].SetTarget(item);
                    else
                        _enumNodes[i] = Activator.CreateInstance(
                            nodeType, ALL_FLAGS, null, new object[] { "<item_" + i + ">", item }, null) as BaseNode;
                }
                else
                {
                    _enumNodes.Add(Activator.CreateInstance(
                        nodeType, ALL_FLAGS, null, new object[] { "<item_" + i + ">", item }, null) as BaseNode);
                }
                i++;
            }
            if (i < _enumNodes.Count)
                _enumNodes.RemoveRange(i, _enumNodes.Count - i);
        }

        private void UpdateFieldNodes(bool rebuild = false)
        {
            if (rebuild || _fieldIsDirty)
            {
                _fieldIsDirty = false;
                BuildChildNodes(ref _fieldNodes, GetFieldTypes(),
                    typeof(FieldOfStructNode<,,>), typeof(FieldOfNullableNode<,,>), typeof(FieldOfClassNode<,,>));
            }
        }

        private void UpdatePropertyNodes(bool rebuild = false)
        {
            if (rebuild || _propertyIsDirty)
            {
                _propertyIsDirty = false;
                BuildChildNodes(ref _propertyNodes, GetPropertyTypes(),
                        typeof(PropertyOfStructNode<,,>), typeof(PropertyOfNullableNode<,,>), typeof(PropertyOfClassNode<,,>));
            }
        }

        private void BuildChildNodes(ref List<BaseNode> nodes, IReadOnlyDictionary<string, Type> info,
            Type structNode, Type nullableNode, Type classNode)
        {
            if (nodes == null)
                nodes = new List<BaseNode>();
            else
                nodes.Clear();

            if (IsException || IsNull)
                return;

            if (InstType.IsValueType)
            {

                if (!IsNullable)
                {
                    foreach (KeyValuePair<string, Type> child in info)
                    {
                        nodes.Add(Activator.CreateInstance(
                            structNode.MakeGenericType(Type, InstType, child.Value),
                            ALL_FLAGS, null,
                            new object[] { this, child.Key }, null) as BaseNode);
                    }
                }
                else
                {
                    Type underlying = Nullable.GetUnderlyingType(InstType) ?? InstType;
                    foreach (KeyValuePair<string, Type> child in info)
                    {
                        nodes.Add(Activator.CreateInstance(
                            nullableNode.MakeGenericType(Type, underlying, child.Value),
                            ALL_FLAGS, null,
                            new object[] { this, child.Key }, null) as BaseNode);
                    }
                }
            }
            else
            {
                foreach (KeyValuePair<string, Type> child in info)
                {
                    nodes.Add(Activator.CreateInstance(
                        classNode.MakeGenericType(Type, InstType, child.Value),
                        ALL_FLAGS, null, 
                        new object[] { this, child.Key }, null) as BaseNode);
                }
            }

            nodes.Sort((x, y) => x.Name.CompareTo(y.Name));
        }
    }

    public class RootNode<TNode> : GenericNode<TNode>
    {
        public RootNode(string name, TNode target) : base()
        {
            Name = name;
            Value = target;
        }

        internal override void SetTarget(object target)
        {
            Value = (TNode)target;
        }
    }

    public class ChildComponentNode<TNode> : GenericNode<TNode>
        where TNode : Component
    {
        protected ChildComponentNode(string name, TNode target) : base()
        {
            Name = name;
            Value = target;
        }

        public override bool IsChildComponent => true;

        internal override void SetTarget(object target)
        {
            Value = (TNode)target;
        }
    }

    public class ItemOfEnumNode<TNode> : GenericNode<TNode>
    {
        protected ItemOfEnumNode(string name, TNode target) : base()
        {
            Name = name;
            Value = target;
        }

        public override bool IsEnumItem => true;

        internal override void SetTarget(object target)
        {
            Value = (TNode)target;
        }
    }

    public abstract class ChildNode<TParent, TNode> : GenericNode<TNode>
    {
        protected WeakReference<GenericNode<TParent>> _parentNode;

        protected ChildNode(GenericNode<TParent> parentNode, string name) : base()
        {
            _parentNode = new WeakReference<GenericNode<TParent>>(parentNode);
            Name = name;
            UpdateValue();
        }
    }

    public abstract class ChildOfStructNode<TParent, TParentInst, TNode> : ChildNode<TParent, TNode>
        where TParentInst : struct
    {
        private readonly Func<TParent, TParentInst> _forceCast = UnsafeForceCast.GetDelegate<TParent, TParentInst>();

        protected ChildOfStructNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name) { }

        protected bool TryGetParentValue(out TParentInst value)
        {
            if (_parentNode.TryGetTarget(out GenericNode<TParent> parent) && parent.InstType == typeof(TParentInst))
            {
                value = _forceCast(parent.Value);
                return true;
            }
            value = default;
            return false;
        }
    }

    public abstract class ChildOfNullableNode<TParent, TUnderlying, TNode> : ChildNode<TParent, TNode>
        where TUnderlying : struct
    {
        private readonly Func<TParent, TUnderlying?> _forceCast = UnsafeForceCast.GetDelegate<TParent, TUnderlying?>();

        protected ChildOfNullableNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name) { }

        protected bool TryGetParentValue(out TUnderlying value)
        {
            if (_parentNode.TryGetTarget(out GenericNode<TParent> parent))
            {
                TUnderlying? parentValue = _forceCast(parent.Value);
                if (parentValue.HasValue)
                {
                    value = parentValue.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }
    }

    public abstract class ChildOfClassNode<TParent, TParentInst, TNode> : ChildNode<TParent, TNode>
        where TParent : class
        where TParentInst : class
    {
        protected ChildOfClassNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name) { }

        protected bool TryGetParentValue(out TParentInst value)
        {
            if (_parentNode.TryGetTarget(out GenericNode<TParent> parent))
            {
                value = parent.Value as TParentInst;
                if (value != null)
                    return true;
            }
            value = null;
            return false;
        }
    }

    public class FieldOfStructNode<TParent, TParentInst, TNode> : ChildOfStructNode<TParent, TParentInst, TNode>
        where TParentInst : struct
    {
        protected FieldOfStructNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name) { }

        public override bool IsField => true;

        public override void UpdateValue()
        {
            if (TryGetParentValue(out TParentInst parentValue))
            {
                IsException = false;
                Value = parentValue.GetFieldValue<TParentInst, TNode>(Name);
            }
            else
            {
                IsException = true;
                Value = default;
            }
        }
    }

    public class PropertyOfStructNode<TParent, TParentInst, TNode> : ChildOfStructNode<TParent, TParentInst, TNode>
        where TParentInst : struct
    {
        protected PropertyOfStructNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name) { }

        public override bool IsProperty => true;

        public override void UpdateValue()
        {
            if (TryGetParentValue(out TParentInst parentValue))
            {
                try
                {
                    IsException = false;
                    Value = parentValue.GetPropertyValue<TParentInst, TNode>(Name);
                }
                catch
                {
                    IsException = true;
                    Value = default;
                }
            }
            else
            {
                IsException = true;
                Value = default;
            }
        }
    }

    public class FieldOfNullableNode<TParent, TUnderlying, TNode> : ChildOfNullableNode<TParent, TUnderlying, TNode>
        where TUnderlying : struct
    {
        protected FieldOfNullableNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name) { }

        public override bool IsField => true;

        public override void UpdateValue()
        {
            if (TryGetParentValue(out TUnderlying parentValue))
            {
                IsException = false;
                Value = parentValue.GetFieldValue<TUnderlying, TNode>(Name);
            }
            else
            {
                IsException = true;
                Value = default;
            }
        }
    }

    public class PropertyOfNullableNode<TParent, TUnderlying, TNode> : ChildOfNullableNode<TParent, TUnderlying, TNode>
        where TUnderlying : struct
    {
        protected PropertyOfNullableNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name) { }

        public override bool IsProperty => true;

        public override void UpdateValue()
        {
            if (TryGetParentValue(out TUnderlying parentValue))
            {
                try
                {
                    IsException = false;
                    Value = parentValue.GetPropertyValue<TUnderlying, TNode>(Name);
                }
                catch
                {
                    IsException = true;
                    Value = default;
                }
            }
            else
            {
                IsException = true;
                Value = default;
            }
        }
    }

    public class FieldOfClassNode<TParent, TParentInst, TNode> : ChildOfClassNode<TParent, TParentInst, TNode>
        where TParent : class
        where TParentInst : class
    {
        protected FieldOfClassNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name) { }

        public override bool IsField => true;

        public override void UpdateValue()
        {
            if (TryGetParentValue(out TParentInst parentValue))
            {
                IsException = false;
                Value = parentValue.GetFieldValue<TParentInst, TNode>(Name);
            }
            else
            {
                IsException = true;
                Value = default;
            }
        }
    }

    public class PropertyOfClassNode<TParent, TParentInst, TNode> : ChildOfClassNode<TParent, TParentInst, TNode>
        where TParent : class
        where TParentInst : class
    {
        protected PropertyOfClassNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name) { }

        public override bool IsProperty => true;

        public override void UpdateValue()
        {
            if (TryGetParentValue(out TParentInst parentValue))
            {
                try
                {
                    IsException = false;
                    Value = parentValue.GetPropertyValue<TParentInst, TNode>(Name);
                }
                catch
                {
                    IsException = true;
                    Value = default;
                }
            }
            else
            {
                IsException = true;
                Value = default;
            }
        }
    }
}
