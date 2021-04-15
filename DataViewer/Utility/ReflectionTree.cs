using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using static ModMaker.Utility.ReflectionCache;
using ToggleState = ModMaker.Utility.ToggleState;

namespace DataViewer.Utility.ReflectionTree {
    public enum NodeType {
        Root,
        Component,
        Item,
        Field,
        Property
    }

    public class Tree : Tree<object> {
        public Tree(object root) : base(root) { }
    }

    public class Tree<TRoot> {
        private RootNode<TRoot> _root;

        public TRoot Root => _root.Value;

        public Node RootNode => _root;

        public Tree(TRoot root) {
            SetRoot(root);
        }

        public void SetRoot(TRoot root) {
            if (_root != null)
                _root.SetValue(root);
            else
                _root = new RootNode<TRoot>("<Root>", root);
        }
    }

    public abstract class Node {
        protected const BindingFlags ALL_FLAGS = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public readonly NodeType NodeType;
        public readonly Type Type;
        public readonly bool IsNullable;

        public readonly HashSet<Node> ChildrenContainingMatches = new HashSet<Node> { };
        public bool Matches = false;
        protected Node(Type type, NodeType nodeType) {
            NodeType = nodeType;
            Type = type;
            IsNullable = Type.IsGenericType && !Type.IsGenericTypeDefinition && Type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
        public ToggleState Expanded { get; set; }
        public bool hasChildren {
            get {
                if (IsBaseType) return false;
                return GetItemNodes().Count > 0 || GetComponentNodes().Count > 0 || GetFieldNodes().Count > 0 || GetPropertyNodes().Count > 0;
            }
        }
        public string Name { get; protected set; }
        public abstract string ValueText { get; }
        public abstract Type InstType { get; }
        public abstract bool IsBaseType { get; }
        public abstract bool IsEnumerable { get; }
        public abstract bool IsException { get; }
        public abstract bool IsGameObject { get; }
        public abstract bool IsNull { get; }
        public static IEnumerable<FieldInfo> GetFields(Type type) {
            HashSet<string> names = new HashSet<string>();
            foreach (FieldInfo field in (Nullable.GetUnderlyingType(type) ?? type).GetFields(ALL_FLAGS)) {
                if (!field.IsStatic &&
                    !field.IsDefined(typeof(CompilerGeneratedAttribute), false) &&  // ignore backing field
                    names.Add(field.Name)) {
                    yield return field;
                }
            }
        }
        public static IEnumerable<PropertyInfo> GetProperties(Type type) {
            HashSet<string> names = new HashSet<string>();
            foreach (PropertyInfo property in (Nullable.GetUnderlyingType(type) ?? type).GetProperties(ALL_FLAGS)) {
                if (property.GetMethod != null &&
                    !property.GetMethod.IsStatic &&
                    property.GetMethod.GetParameters().Length == 0 &&
                    names.Add(property.Name))
                    yield return property;
            }
        }
        public abstract IReadOnlyCollection<Node> GetItemNodes();
        public abstract IReadOnlyCollection<Node> GetComponentNodes();
        public abstract IReadOnlyCollection<Node> GetPropertyNodes();
        public abstract IReadOnlyCollection<Node> GetFieldNodes();
        public abstract Node GetParent();
        public abstract void SetDirty();
        public abstract bool IsDirty();
        internal abstract void SetValue(object value);
        protected abstract void UpdateValue();
    }

    internal abstract class GenericNode<TNode> : Node {
        // the tree will not show any child nodes of following types
        private static readonly HashSet<Type> BASE_TYPES = new HashSet<Type>()
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

        private Type _instType;
        private bool? _isBaseType;
        private bool? _isEnumerable;
        private bool? _isGameObject;

        private TNode _value;
        private bool _valueIsDirty = true;

        private List<Node> _componentNodes;
        private List<Node> _itemNodes;
        private List<Node> _fieldNodes;
        private List<Node> _propertyNodes;
        private bool _componentIsDirty;
        private bool _itemIsDirty;
        private bool _fieldIsDirty;
        private bool _propertyIsDirty;
        protected GenericNode(NodeType nodeType) : base(typeof(TNode), nodeType) {
            if (Type.IsValueType && !IsNullable) {
                _instType = Type;
            }
        }
        public TNode Value {
            get {
                UpdateValue();
                return _value;
            }
            protected set {
                if (!value?.Equals(_value) ?? _value != null) {
                    _value = value;
                    if (!Type.IsValueType || IsNullable) {
                        Type oldType = _instType;
                        _instType = value?.GetType();
                        if (_instType != oldType) {
                            _isBaseType = null;
                            _isEnumerable = null;
                            _isGameObject = null;
                            _fieldIsDirty = true;
                            _propertyIsDirty = true;
                        }
                    }
                }
            }
        }
        public override string ValueText => IsException ? "<exception>" : IsNull ? "<null>" : Value.ToString();
        public override Type InstType {
            get {
                UpdateValue();
                return _instType;
            }
        }
        public override bool IsBaseType {
            get {
                UpdateValue();
                return _isBaseType ?? (_isBaseType = BASE_TYPES.Contains(Nullable.GetUnderlyingType(InstType ?? Type) ?? InstType ?? Type)).Value;
            }
        }
        public override bool IsEnumerable {
            get {
                UpdateValue();
                return _isEnumerable ?? (_isEnumerable = (InstType ?? Type).GetInterfaces().Contains(typeof(IEnumerable))).Value;
            }
        }
        public override bool IsGameObject {
            get {
                UpdateValue();
                return _isGameObject ?? (_isGameObject = typeof(GameObject).IsAssignableFrom(InstType ?? Type)).Value;
            }
        }
        public override bool IsNull => Value == null || ((Value is UnityEngine.Object unityObject) && !unityObject);
        public override IReadOnlyCollection<Node> GetComponentNodes() {
            UpdateComponentNodes();
            return _componentNodes.AsReadOnly();
        }
        public override IReadOnlyCollection<Node> GetItemNodes() {
            UpdateItemNodes();
            return _itemNodes.AsReadOnly();
        }
        public override IReadOnlyCollection<Node> GetFieldNodes() {
            UpdateFieldNodes();
            return _fieldNodes.AsReadOnly();
        }
        public override IReadOnlyCollection<Node> GetPropertyNodes() {
            UpdatePropertyNodes();
            return _propertyNodes.AsReadOnly();
        }
        public override Node GetParent() {
            return null;
        }
        public override void SetDirty() {
            _valueIsDirty = true;
            Matches = false;
            ChildrenContainingMatches.Clear();
        }
        public override bool IsDirty() {
            return _valueIsDirty;
        }

        private void UpdateComponentNodes() {
            UpdateValue();
            if (!_componentIsDirty && _componentNodes != null) {
                return;
            }

            _componentIsDirty = false;

            if (_componentNodes == null) {
                _componentNodes = new List<Node>();
            }

            if (IsException || IsNull || !IsGameObject) {
                _componentNodes.Clear();
                return;
            }

            Type nodeType = typeof(ComponentNode);
            int i = 0;
            int count = _componentNodes.Count;
            foreach (Component item in (Value as GameObject).GetComponents<Component>()) {
                if (i < count)
                    (_componentNodes[i] as ComponentNode).SetValue(item);
                else
                    _componentNodes.Add(Activator.CreateInstance(
                        nodeType, ALL_FLAGS, null, new object[] { "<component_" + i + ">", item }, null) as Node);
                i++;
            }

            if (i < count) {
                _componentNodes.RemoveRange(i, count - i);
            }
        }
        private void UpdateItemNodes() {
            UpdateValue();

            if (!_itemIsDirty && _itemNodes != null) {
                return;
            }

            _itemIsDirty = false;

            if (_itemNodes == null) {
                _itemNodes = new List<Node>();
            }

            if (IsException || IsNull || !IsEnumerable) {
                _itemNodes.Clear();
                return;
            }

            IEnumerable<Type> itemTypes = InstType.GetInterfaces()
                .Where(item => item.IsGenericType && item.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .Select(item => item.GetGenericArguments()[0]);
            Type itemType = itemTypes.Count() == 1 ? itemTypes.First() : typeof(object);
            Type nodeType = typeof(ItemNode<>).MakeGenericType(itemType);
            int i = 0;
            int count = _itemNodes.Count;
            foreach (object item in Value as IEnumerable) {
                if (i < count) {
                    if (_itemNodes[i].Type == itemType)
                        _itemNodes[i].SetValue(item);
                    else
                        _itemNodes[i] = Activator.CreateInstance(
                            nodeType, ALL_FLAGS, null, new object[] { "<item_" + i + ">", item }, null) as Node;
                }
                else {
                    _itemNodes.Add(Activator.CreateInstance(
                        nodeType, ALL_FLAGS, null, new object[] { "<item_" + i + ">", item }, null) as Node);
                }
                i++;
            }

            if (i < count) {
                _itemNodes.RemoveRange(i, count - i);
            }
        }
        private void UpdateFieldNodes() {
            UpdateValue();

            if (!_fieldIsDirty && _fieldNodes != null) {
                return;
            }

            _fieldIsDirty = false;

            if (IsException || IsNull) {
                if (_fieldNodes == null)
                    _fieldNodes = new List<Node>();
                else
                    _fieldNodes.Clear();
                return;
            }

            Type nodeType = InstType.IsValueType ? !IsNullable ?
                typeof(FieldOfStructNode<,,>) : typeof(FieldOfNullableNode<,,>) : typeof(FieldOfClassNode<,,>);

            _fieldNodes = GetFields(InstType).Select(child => Activator.CreateInstance(
                nodeType.MakeGenericType(Type, InstType, child.FieldType),
                ALL_FLAGS, null, new object[] { this, child.Name }, null) as Node).ToList();

            _fieldNodes.Sort((x, y) => x.Name.CompareTo(y.Name));
        }
        private void UpdatePropertyNodes() {
            UpdateValue();

            if (!_propertyIsDirty && _propertyNodes != null) {
                return;
            }

            _propertyIsDirty = false;

            if (IsException || IsNull) {
                if (_propertyNodes == null)
                    _propertyNodes = new List<Node>();
                else
                    _propertyNodes.Clear();
                return;
            }

            Type nodeType = InstType.IsValueType ? !IsNullable ?
                typeof(PropertyOfStructNode<,,>) : typeof(PropertyOfNullableNode<,,>) : typeof(PropertyOfClassNode<,,>);

            _propertyNodes = GetProperties(InstType).Select(child => Activator.CreateInstance(
                nodeType.MakeGenericType(Type, InstType, child.PropertyType),
                ALL_FLAGS, null, new object[] { this, child.Name }, null) as Node).ToList();

            _propertyNodes.Sort((x, y) => x.Name.CompareTo(y.Name));
        }
        protected override void UpdateValue() {
            if (_valueIsDirty) {
                _valueIsDirty = false;

                _componentIsDirty = true;
                _itemIsDirty = true;

                if (_fieldNodes != null)
                    foreach (Node child in _fieldNodes)
                        child.SetDirty();

                if (_propertyNodes != null)
                    foreach (Node child in _propertyNodes)
                        child.SetDirty();

                UpdateValueImpl();
            }
        }
        protected abstract void UpdateValueImpl();
    }

    internal abstract class PassiveNode<TNode> : GenericNode<TNode> {
        public override bool IsException => false;

        public PassiveNode(string name, TNode value, NodeType nodeType) : base(nodeType) {
            Name = name;
            Value = value;
        }
        internal override void SetValue(object value) {
            SetDirty();
            Value = (TNode)value;
        }
        internal void SetValue(TNode value) {
            SetDirty();
            Value = value;
        }
        protected override void UpdateValueImpl() { }
    }

    internal class RootNode<TNode> : PassiveNode<TNode> {
        public RootNode(string name, TNode value) : base(name, value, NodeType.Root) { }
    }

    internal class ComponentNode : PassiveNode<Component> {
        protected ComponentNode(string name, Component value) : base(name, value, NodeType.Component) { }
    }

    internal class ItemNode<TNode> : PassiveNode<TNode> {
        protected ItemNode(string name, TNode value) : base(name, value, NodeType.Item) { }
    }

    internal abstract class ChildNode<TParent, TNode> : GenericNode<TNode> {
        protected bool _isException;
        protected readonly WeakReference<GenericNode<TParent>> _parentNode;
        public override bool IsException {
            get {
                UpdateValue();
                return _isException;
            }
        }
        protected ChildNode(GenericNode<TParent> parentNode, string name, NodeType nodeType) : base(nodeType) {
            _parentNode = new WeakReference<GenericNode<TParent>>(parentNode);
            Name = name;
        }
        internal override void SetValue(object value) {
            throw new NotImplementedException();
        }
        public override Node GetParent() {
            if (_parentNode.TryGetTarget(out GenericNode<TParent> parent)) {
                return parent;
            }
            return null;
        }
    }

    internal abstract class ChildOfStructNode<TParent, TParentInst, TNode> : ChildNode<TParent, TNode>
        where TParentInst : struct {
        private readonly Func<TParent, TParentInst> _forceCast = UnsafeForceCast.GetDelegate<TParent, TParentInst>();
        protected ChildOfStructNode(GenericNode<TParent> parentNode, string name, NodeType nodeType) : base(parentNode, name, nodeType) { }
        protected bool TryGetParentValue(out TParentInst value) {
            if (_parentNode.TryGetTarget(out GenericNode<TParent> parent) && parent.InstType == typeof(TParentInst)) {
                value = _forceCast(parent.Value);
                return true;
            }
            value = default;
            return false;
        }
    }

    internal abstract class ChildOfNullableNode<TParent, TUnderlying, TNode> : ChildNode<TParent, TNode>
        where TUnderlying : struct {
        private readonly Func<TParent, TUnderlying?> _forceCast = UnsafeForceCast.GetDelegate<TParent, TUnderlying?>();
        protected ChildOfNullableNode(GenericNode<TParent> parentNode, string name, NodeType nodeType) : base(parentNode, name, nodeType) { }
        protected bool TryGetParentValue(out TUnderlying value) {
            if (_parentNode.TryGetTarget(out GenericNode<TParent> parent)) {
                TUnderlying? parentValue = _forceCast(parent.Value);
                if (parentValue.HasValue) {
                    value = parentValue.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }
    }

    internal abstract class ChildOfClassNode<TParent, TParentInst, TNode> : ChildNode<TParent, TNode>
        where TParentInst : class {
        protected ChildOfClassNode(GenericNode<TParent> parentNode, string name, NodeType nodeType) : base(parentNode, name, nodeType) { }
        protected bool TryGetParentValue(out TParentInst value) {
            if (_parentNode.TryGetTarget(out GenericNode<TParent> parent) && (value = parent.Value as TParentInst) != null) {
                return true;
            }
            value = null;
            return false;
        }
    }

    internal class FieldOfStructNode<TParent, TParentInst, TNode> : ChildOfStructNode<TParent, TParentInst, TNode>
        where TParentInst : struct {
        protected FieldOfStructNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name, NodeType.Field) { }
        protected override void UpdateValueImpl() {
            if (TryGetParentValue(out TParentInst parentValue)) {
                _isException = false;
                Value = parentValue.GetFieldValue<TParentInst, TNode>(Name);
            }
            else {
                _isException = true;
                Value = default;
            }
        }
    }

    internal class PropertyOfStructNode<TParent, TParentInst, TNode> : ChildOfStructNode<TParent, TParentInst, TNode>
        where TParentInst : struct {
        protected PropertyOfStructNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name, NodeType.Property) { }
        protected override void UpdateValueImpl() {
            if (TryGetParentValue(out TParentInst parentValue)) {
                try {
                    _isException = false;
                    Value = parentValue.GetPropertyValue<TParentInst, TNode>(Name);
                }
                catch {
                    _isException = true;
                    Value = default;
                }
            }
            else {
                _isException = true;
                Value = default;
            }
        }
    }

    internal class FieldOfNullableNode<TParent, TUnderlying, TNode> : ChildOfNullableNode<TParent, TUnderlying, TNode>
        where TUnderlying : struct {
        protected FieldOfNullableNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name, NodeType.Field) { }
        protected override void UpdateValueImpl() {
            if (TryGetParentValue(out TUnderlying parentValue)) {
                _isException = false;
                Value = parentValue.GetFieldValue<TUnderlying, TNode>(Name);
            }
            else {
                _isException = true;
                Value = default;
            }
        }
    }

    internal class PropertyOfNullableNode<TParent, TUnderlying, TNode> : ChildOfNullableNode<TParent, TUnderlying, TNode>
        where TUnderlying : struct {
        protected PropertyOfNullableNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name, NodeType.Property) { }
        protected override void UpdateValueImpl() {
            if (TryGetParentValue(out TUnderlying parentValue)) {
                try {
                    _isException = false;
                    Value = parentValue.GetPropertyValue<TUnderlying, TNode>(Name);
                }
                catch {
                    _isException = true;
                    Value = default;
                }
            }
            else {
                _isException = true;
                Value = default;
            }
        }
    }

    internal class FieldOfClassNode<TParent, TParentInst, TNode> : ChildOfClassNode<TParent, TParentInst, TNode>
        where TParentInst : class {
        protected FieldOfClassNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name, NodeType.Field) { }
        protected override void UpdateValueImpl() {
            if (TryGetParentValue(out TParentInst parentValue)) {
                _isException = false;
                Value = parentValue.GetFieldValue<TParentInst, TNode>(Name);
            }
            else {
                _isException = true;
                Value = default;
            }
        }
    }

    internal class PropertyOfClassNode<TParent, TParentInst, TNode> : ChildOfClassNode<TParent, TParentInst, TNode>
        where TParentInst : class {
        protected PropertyOfClassNode(GenericNode<TParent> parentNode, string name) : base(parentNode, name, NodeType.Property) { }
        protected override void UpdateValueImpl() {
            if (TryGetParentValue(out TParentInst parentValue)) {
                try {
                    _isException = false;
                    Value = parentValue.GetPropertyValue<TParentInst, TNode>(Name);
                }
                catch {
                    _isException = true;
                    Value = default;
                }
            }
            else {
                _isException = true;
                Value = default;
            }
        }
    }
}
