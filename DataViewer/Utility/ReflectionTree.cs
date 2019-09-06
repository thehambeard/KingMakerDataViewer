﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using static ModMaker.Utility.ReflectionCache;

namespace DataViewer.Utility.ReflectionTree
{
    public enum NodeType
    {
        Root,
        Component,
        Item,
        Field,
        Property
    }

    public class Tree : Tree<object>
    {
        public Tree(object target) : base(target) { }
    }

    public class Tree<TRoot>
    {
        private RootNode<TRoot> _root;

        public TRoot Root => _root.Value;

        public Node RootNode => _root;

        public Tree(TRoot root)
        {
            SetRoot(root);
        }

        public void SetRoot(TRoot root)
        {
            if (_root != null)
                _root.SetValue(root);
            else
                _root = new RootNode<TRoot>("<Root>", root);
        }
    }

    public abstract class Node
    {
        protected const BindingFlags ALL_FLAGS = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // the tree will not show any child nodes of following types
        protected static readonly HashSet<Type> BASE_TYPES = new HashSet<Type>()
        {
            typeof(object),
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

        protected List<Node> _componentNodes;
        protected List<Node> _enumNodes;
        protected List<Node> _fieldNodes;
        protected List<Node> _propertyNodes;

        protected bool _componentIsDirty = true;
        protected bool _enumIsDirty = true;
        protected bool _fieldIsDirty = true;
        protected bool _propertyIsDirty = true;

        public readonly Type Type;

        public readonly NodeType NodeType;

        public Type InstType { get; protected set; }

        public int CustomFlags { get; set; }

        public string Name { get; protected set; }

        public abstract string ValueText { get; }

        public bool IsBaseType => BASE_TYPES.Contains(Nullable.GetUnderlyingType(InstType ?? Type) ?? InstType ?? Type);

        public bool IsGameObject => typeof(GameObject).IsAssignableFrom(InstType ?? Type);

        public bool IsEnumerable => _isEnumerable ?? 
            (_isEnumerable = !IsBaseType && (InstType ?? Type).GetInterfaces().Contains(typeof(IEnumerable))).Value;

        public bool IsException { get; protected set; }

        public abstract bool IsNull { get; }

        public bool IsNullable => Type.IsGenericType && !Type.IsGenericTypeDefinition && Type.GetGenericTypeDefinition() == typeof(Nullable<>);

        protected Node(Type type, NodeType nodeType)
        {
            Type = type;
            if (type.IsValueType && !IsNullable)
                InstType = type;
            NodeType = nodeType;
        }

        public static IEnumerable<FieldInfo> GetFields(Type type)
        {
            HashSet<string> names = new HashSet<string>();
            foreach (FieldInfo field in (Nullable.GetUnderlyingType(type) ?? type).GetFields(ALL_FLAGS))
            {
                if (!field.IsStatic &&
                    !field.IsDefined(typeof(CompilerGeneratedAttribute), false) &&  // ignore backing field
                    names.Add(field.Name))
                {
                    yield return field;
                }
            }
        }

        public static IEnumerable<PropertyInfo> GetProperties(Type type)
        {
            HashSet<string> names = new HashSet<string>();
            foreach (PropertyInfo property in (Nullable.GetUnderlyingType(type) ?? type).GetProperties(ALL_FLAGS))
            {
                if (property.GetMethod != null &&
                    !property.GetMethod.IsStatic &&
                    property.GetMethod.GetParameters().Length == 0 &&
                    names.Add(property.Name))
                    yield return property;
            }
        }

        public abstract IReadOnlyCollection<Node> GetComponentNodes();

        public abstract IReadOnlyCollection<Node> GetEnumNodes();

        public abstract IReadOnlyCollection<Node> GetFieldNodes();

        public abstract IReadOnlyCollection<Node> GetPropertyNodes();

        internal abstract void SetValue(object value);

        public abstract void UpdateValue();
    }

    internal abstract class GenericNode<TNode> : Node
    {
        protected TNode _value;

        public TNode Value {
            get {
                return _value;
            }
            protected set {
                if (value == null)
                {
                    if (_value != null)
                    {
                        InstType = null;
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
                                _fieldIsDirty = true;
                                _propertyIsDirty = true;
                                _isEnumerable = null;
                            }
                        }
                    }

                    //if (InstType == null)
                    //    InstType = Type;
                }
                // component / enum elements 'may' changed
                _componentIsDirty = true;
                _enumIsDirty = true;
                _value = value;
            }
        }

        public override string ValueText => IsException ? "<exception>" : IsNull ? "<null>" : Value.ToString();

        public override bool IsNull => Value == null || ((Value is UnityEngine.Object unityObject) && !unityObject);

        protected GenericNode(NodeType nodeType) : base(typeof(TNode), nodeType) { }

        public override IReadOnlyCollection<Node> GetComponentNodes()
        {
            UpdateComponentNodes();
            return _componentNodes.AsReadOnly();
        }

        public override IReadOnlyCollection<Node> GetEnumNodes()
        {
            UpdateEnumNodes();
            return _enumNodes.AsReadOnly();
        }

        public override IReadOnlyCollection<Node> GetFieldNodes()
        {
            UpdateFieldNodes();
            return _fieldNodes.AsReadOnly();
        }

        public override IReadOnlyCollection<Node> GetPropertyNodes()
        {
            UpdatePropertyNodes();
            return _propertyNodes.AsReadOnly();
        }

        private void BuildChildNodes(ref List<Node> nodes, IEnumerable<Tuple<string, Type>> nameAndTypeOfNodes,
            Type structNode, Type nullableNode, Type classNode)
        {
            if (nodes == null)
                nodes = new List<Node>();
            else
                nodes.Clear();

            if (IsException || IsNull)
                return;

            if (InstType.IsValueType)
            {
                if (!IsNullable)
                {
                    nodes = nameAndTypeOfNodes.Select(child => Activator.CreateInstance(
                            structNode.MakeGenericType(Type, InstType, child.Item2),
                            ALL_FLAGS, null, new object[] { this, child.Item1 }, null) as Node).ToList();
                }
                else
                {
                    Type underlying = Nullable.GetUnderlyingType(InstType) ?? InstType;
                    nodes = nameAndTypeOfNodes.Select(child => Activator.CreateInstance(
                            nullableNode.MakeGenericType(Type, underlying, child.Item2),
                            ALL_FLAGS, null, new object[] { this, child.Item1 }, null) as Node).ToList();
                }
            }
            else
            {
                nodes = nameAndTypeOfNodes.Select(child => Activator.CreateInstance(
                        classNode.MakeGenericType(Type, InstType, child.Item2),
                        ALL_FLAGS, null, new object[] { this, child.Item1 }, null) as Node).ToList();
            }

            nodes.Sort((x, y) => x.Name.CompareTo(y.Name));
        }

        private IEnumerable<Tuple<string, Type>> GetNameAndTypeOfFields()
        {
            foreach (FieldInfo field in GetFields(InstType))
            {
                yield return new Tuple<string, Type>(field.Name, field.FieldType);
            }
        }

        private IEnumerable<Tuple<string, Type>> GetNameAndTypeOfProperties()
        {
            foreach (PropertyInfo property in GetProperties(InstType))
            {
                yield return new Tuple<string, Type>(property.Name, property.PropertyType);
            }
        }

        private void UpdateComponentNodes()
        {
            if (!_componentIsDirty)
                return;

            _componentIsDirty = false;

            if (_componentNodes == null)
                _componentNodes = new List<Node>();

            if (IsException || IsNull || !IsGameObject)
            {
                _componentNodes.Clear();
                return;
            }

            Type nodeType = typeof(ComponentNode<Component>);
            int i = 0;
            int count = _componentNodes.Count;
            foreach (Component item in (Value as GameObject).GetComponents<Component>())
            {
                if (i < count)
                {
                    _componentNodes[i].SetValue(item);
                }
                else
                {
                    _componentNodes.Add(Activator.CreateInstance(
                        nodeType, ALL_FLAGS, null, new object[] { "<component_" + i + ">", item }, null) as Node);
                }
                i++;
            }
            if (i < count)
                _componentNodes.RemoveRange(i, count - i);
        }

        private void UpdateEnumNodes()
        {
            if (!_enumIsDirty)
                return;

            _enumIsDirty = false;

            if (_enumNodes == null)
                _enumNodes = new List<Node>();

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

            Type nodeType = typeof(ItemNode<>).MakeGenericType(itemType);
            int i = 0;
            int count = _enumNodes.Count;
            foreach (object item in Value as IEnumerable)
            {
                if (i < count)
                {
                    if (_enumNodes[i].Type == itemType)
                        _enumNodes[i].SetValue(item);
                    else
                        _enumNodes[i] = Activator.CreateInstance(
                            nodeType, ALL_FLAGS, null, new object[] { "<item_" + i + ">", item }, null) as Node;
                }
                else
                {
                    _enumNodes.Add(Activator.CreateInstance(
                        nodeType, ALL_FLAGS, null, new object[] { "<item_" + i + ">", item }, null) as Node);
                }
                i++;
            }

            if (i < count)
                _enumNodes.RemoveRange(i, count - i);
        }

        private void UpdateFieldNodes()
        {
            if ( _fieldIsDirty)
            {
                _fieldIsDirty = false;
                BuildChildNodes(ref _fieldNodes, GetNameAndTypeOfFields(),
                    typeof(FieldOfStructNode<,,>), typeof(FieldOfNullableNode<,,>), typeof(FieldOfClassNode<,,>));
            }
        }

        private void UpdatePropertyNodes()
        {
            if (_propertyIsDirty)
            {
                _propertyIsDirty = false;
                BuildChildNodes(ref _propertyNodes, GetNameAndTypeOfProperties(),
                        typeof(PropertyOfStructNode<,,>), typeof(PropertyOfNullableNode<,,>), typeof(PropertyOfClassNode<,,>));
            }
        }
    }

    internal abstract class PassiveNode<TNode> : GenericNode<TNode>
    {
        public PassiveNode(string name, TNode value, NodeType nodeType) : base(nodeType)
        {
            Name = name;
            Value = value;
        }

        internal override void SetValue(object value)
        {
            Value = (TNode)value;
        }

        public override void UpdateValue()
        {
            Value = Value;
        }
    }

    internal class RootNode<TNode> : PassiveNode<TNode>
    {
        public RootNode(string name, TNode value) : base(name, value, NodeType.Root) { }
    }

    internal class ComponentNode<TNode> : PassiveNode<TNode> 
        where TNode : Component
    {
        protected ComponentNode(string name, TNode value) : base(name, value, NodeType.Component) { }
    }

    internal class ItemNode<TNode> : PassiveNode<TNode>
    {
        protected ItemNode(string name, TNode value) : base(name, value, NodeType.Item) { }
    }

    internal abstract class ChildNode<TParent, TNode> : GenericNode<TNode>
    {
        protected WeakReference<GenericNode<TParent>> _parentNode;

        protected ChildNode(GenericNode<TParent> parentNode, string name, NodeType nodeType) : base(nodeType)
        {
            _parentNode = new WeakReference<GenericNode<TParent>>(parentNode);
            Name = name;
            UpdateValue();
        }

        internal override void SetValue(object value)
        {
            throw new NotImplementedException();
        }
    }

    internal abstract class ChildOfStructNode<TParent, TParentInst, TNode> : ChildNode<TParent, TNode>
        where TParentInst : struct
    {
        private readonly Func<TParent, TParentInst> _forceCast = UnsafeForceCast.GetDelegate<TParent, TParentInst>();

        protected ChildOfStructNode(GenericNode<TParent> parentNode, string name, NodeType nodeType) : base(parentNode, name, nodeType) { }

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

    internal abstract class ChildOfNullableNode<TParent, TUnderlying, TNode> : ChildNode<TParent, TNode>
        where TUnderlying : struct
    {
        private readonly Func<TParent, TUnderlying?> _forceCast = UnsafeForceCast.GetDelegate<TParent, TUnderlying?>();

        protected ChildOfNullableNode(GenericNode<TParent> parentNode, string name, NodeType nodeType) : base(parentNode, name, nodeType) { }

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

    internal abstract class ChildOfClassNode<TParent, TParentInst, TNode> : ChildNode<TParent, TNode>
        where TParentInst : class
    {
        protected ChildOfClassNode(GenericNode<TParent> parentNode, string name, NodeType nodeType) : base(parentNode, name, nodeType) { }

        protected bool TryGetParentValue(out TParentInst value)
        {
            if (_parentNode.TryGetTarget(out GenericNode<TParent> parent) && (value = parent.Value as TParentInst) != null)
            {
                return true;
            }
            value = null;
            return false;
        }
    }

    internal class FieldOfStructNode<TParent, TParentInst, TNode> : ChildOfStructNode<TParent, TParentInst, TNode>
        where TParentInst : struct
    {
        protected FieldOfStructNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name, NodeType.Field) { }

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

    internal class PropertyOfStructNode<TParent, TParentInst, TNode> : ChildOfStructNode<TParent, TParentInst, TNode>
        where TParentInst : struct
    {
        protected PropertyOfStructNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name, NodeType.Property) { }

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

    internal class FieldOfNullableNode<TParent, TUnderlying, TNode> : ChildOfNullableNode<TParent, TUnderlying, TNode>
        where TUnderlying : struct
    {
        protected FieldOfNullableNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name, NodeType.Field) { }

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

    internal class PropertyOfNullableNode<TParent, TUnderlying, TNode> : ChildOfNullableNode<TParent, TUnderlying, TNode>
        where TUnderlying : struct
    {
        protected PropertyOfNullableNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name, NodeType.Property) { }

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

    internal class FieldOfClassNode<TParent, TParentInst, TNode> : ChildOfClassNode<TParent, TParentInst, TNode>
        where TParentInst : class
    {
        protected FieldOfClassNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name, NodeType.Field) { }

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

    internal class PropertyOfClassNode<TParent, TParentInst, TNode> : ChildOfClassNode<TParent, TParentInst, TNode>
        where TParentInst : class
    {
        protected PropertyOfClassNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name, NodeType.Property) { }

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
